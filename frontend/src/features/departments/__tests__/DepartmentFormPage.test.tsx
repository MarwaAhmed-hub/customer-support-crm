import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AxiosError, AxiosHeaders } from "axios";
import type { AxiosResponse, InternalAxiosRequestConfig } from "axios";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import * as departmentsApi from "../departmentsApi";
import { DepartmentFormPage } from "../DepartmentFormPage";
import type { Department } from "../types";

const EXISTING: Department = {
  id: "dept-1",
  name: "Support",
  code: "SUP",
  isActive: true,
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: "2026-01-01T00:00:00Z",
};

function axiosErrorWith(status: number, errorCode = ""): AxiosError {
  const config = { url: "/departments", headers: new AxiosHeaders() } as InternalAxiosRequestConfig;
  const error = new AxiosError("failed", String(status), config);
  error.response = { status, statusText: "", data: { error: errorCode }, headers: {}, config } as AxiosResponse;
  return error;
}

function renderCreate() {
  return render(
    <MemoryRouter initialEntries={["/departments/new"]}>
      <Routes>
        <Route path="/departments/new" element={<DepartmentFormPage />} />
        <Route path="/departments" element={<div>landed on list</div>} />
      </Routes>
    </MemoryRouter>,
  );
}

function renderEdit(id: string) {
  return render(
    <MemoryRouter initialEntries={[`/departments/${id}/edit`]}>
      <Routes>
        <Route path="/departments/:id/edit" element={<DepartmentFormPage />} />
        <Route path="/departments" element={<div>landed on list</div>} />
      </Routes>
    </MemoryRouter>,
  );
}

describe("DepartmentFormPage — create mode", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("shows a validation error when the name is empty", async () => {
    renderCreate();

    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    expect(await screen.findByText("Name is required.")).toBeInTheDocument();
  });

  it("submits and navigates to the list", async () => {
    const createDepartment = vi.spyOn(departmentsApi, "createDepartment").mockResolvedValue(EXISTING);

    renderCreate();
    await userEvent.type(screen.getByLabelText("Name"), "Support");
    await userEvent.type(screen.getByLabelText("Code"), "SUP");
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    expect(createDepartment).toHaveBeenCalledWith({ name: "Support", code: "SUP" });
    expect(await screen.findByText("landed on list")).toBeInTheDocument();
  });

  it("sends a null code when the field is left blank", async () => {
    const createDepartment = vi.spyOn(departmentsApi, "createDepartment").mockResolvedValue(EXISTING);

    renderCreate();
    await userEvent.type(screen.getByLabelText("Name"), "Sales");
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    expect(createDepartment).toHaveBeenCalledWith({ name: "Sales", code: null });
  });

  it("surfaces a duplicate name as a field-level error", async () => {
    vi.spyOn(departmentsApi, "createDepartment").mockRejectedValue(axiosErrorWith(409, "duplicate_department_name"));

    renderCreate();
    await userEvent.type(screen.getByLabelText("Name"), "Support");
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    expect(await screen.findByText("A department with this name already exists.")).toBeInTheDocument();
  });

  it("surfaces a duplicate code as a field-level error", async () => {
    vi.spyOn(departmentsApi, "createDepartment").mockRejectedValue(axiosErrorWith(409, "duplicate_department_code"));

    renderCreate();
    await userEvent.type(screen.getByLabelText("Name"), "Support");
    await userEvent.type(screen.getByLabelText("Code"), "SUP");
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    expect(await screen.findByText("A department with this code already exists.")).toBeInTheDocument();
  });
});

describe("DepartmentFormPage — edit mode", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("prefills the form and shows an Active toggle", async () => {
    vi.spyOn(departmentsApi, "getDepartment").mockResolvedValue(EXISTING);

    renderEdit("dept-1");

    expect(await screen.findByDisplayValue("Support")).toBeInTheDocument();
    expect(screen.getByDisplayValue("SUP")).toBeInTheDocument();
    expect(screen.getByRole("switch", { name: "Active" })).toBeChecked();
  });

  it("deactivating and saving includes isActive: false in the payload", async () => {
    vi.spyOn(departmentsApi, "getDepartment").mockResolvedValue(EXISTING);
    const updateDepartment = vi.spyOn(departmentsApi, "updateDepartment").mockResolvedValue({ ...EXISTING, isActive: false });

    renderEdit("dept-1");
    await screen.findByDisplayValue("Support");

    await userEvent.click(screen.getByRole("switch", { name: "Active" }));
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() =>
      expect(updateDepartment).toHaveBeenCalledWith("dept-1", { name: "Support", code: "SUP", isActive: false }),
    );
  });
});
