import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AxiosError, AxiosHeaders } from "axios";
import type { AxiosResponse, InternalAxiosRequestConfig } from "axios";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import * as branchesApi from "../branchesApi";
import { BranchFormPage } from "../BranchFormPage";
import type { Branch } from "../types";

const EXISTING: Branch = {
  id: "branch-1",
  name: "Cairo",
  code: "CAI",
  isActive: true,
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: "2026-01-01T00:00:00Z",
};

function axiosErrorWith(status: number, errorCode = ""): AxiosError {
  const config = { url: "/branches", headers: new AxiosHeaders() } as InternalAxiosRequestConfig;
  const error = new AxiosError("failed", String(status), config);
  error.response = { status, statusText: "", data: { error: errorCode }, headers: {}, config } as AxiosResponse;
  return error;
}

function renderCreate() {
  return render(
    <MemoryRouter initialEntries={["/branches/new"]}>
      <Routes>
        <Route path="/branches/new" element={<BranchFormPage />} />
        <Route path="/branches" element={<div>landed on list</div>} />
      </Routes>
    </MemoryRouter>,
  );
}

function renderEdit(id: string) {
  return render(
    <MemoryRouter initialEntries={[`/branches/${id}/edit`]}>
      <Routes>
        <Route path="/branches/:id/edit" element={<BranchFormPage />} />
        <Route path="/branches" element={<div>landed on list</div>} />
      </Routes>
    </MemoryRouter>,
  );
}

describe("BranchFormPage — create mode", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("shows a validation error when the name is empty", async () => {
    renderCreate();

    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    expect(await screen.findByText("Name is required.")).toBeInTheDocument();
  });

  it("submits and navigates to the list", async () => {
    const createBranch = vi.spyOn(branchesApi, "createBranch").mockResolvedValue(EXISTING);

    renderCreate();
    await userEvent.type(screen.getByLabelText("Name"), "Cairo");
    await userEvent.type(screen.getByLabelText("Code"), "CAI");
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    expect(createBranch).toHaveBeenCalledWith({ name: "Cairo", code: "CAI" });
    expect(await screen.findByText("landed on list")).toBeInTheDocument();
  });

  it("sends a null code when the field is left blank", async () => {
    const createBranch = vi.spyOn(branchesApi, "createBranch").mockResolvedValue(EXISTING);

    renderCreate();
    await userEvent.type(screen.getByLabelText("Name"), "Dubai");
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    expect(createBranch).toHaveBeenCalledWith({ name: "Dubai", code: null });
  });

  it("surfaces a duplicate name as a field-level error", async () => {
    vi.spyOn(branchesApi, "createBranch").mockRejectedValue(axiosErrorWith(409, "duplicate_branch_name"));

    renderCreate();
    await userEvent.type(screen.getByLabelText("Name"), "Cairo");
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    expect(await screen.findByText("A branch with this name already exists.")).toBeInTheDocument();
  });

  it("surfaces a duplicate code as a field-level error", async () => {
    vi.spyOn(branchesApi, "createBranch").mockRejectedValue(axiosErrorWith(409, "duplicate_branch_code"));

    renderCreate();
    await userEvent.type(screen.getByLabelText("Name"), "Cairo");
    await userEvent.type(screen.getByLabelText("Code"), "CAI");
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    expect(await screen.findByText("A branch with this code already exists.")).toBeInTheDocument();
  });
});

describe("BranchFormPage — edit mode", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("prefills the form and shows an Active toggle", async () => {
    vi.spyOn(branchesApi, "getBranch").mockResolvedValue(EXISTING);

    renderEdit("branch-1");

    expect(await screen.findByDisplayValue("Cairo")).toBeInTheDocument();
    expect(screen.getByDisplayValue("CAI")).toBeInTheDocument();
    expect(screen.getByRole("switch", { name: "Active" })).toBeChecked();
  });

  it("deactivating and saving includes isActive: false in the payload", async () => {
    vi.spyOn(branchesApi, "getBranch").mockResolvedValue(EXISTING);
    const updateBranch = vi.spyOn(branchesApi, "updateBranch").mockResolvedValue({ ...EXISTING, isActive: false });

    renderEdit("branch-1");
    await screen.findByDisplayValue("Cairo");

    await userEvent.click(screen.getByRole("switch", { name: "Active" }));
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() =>
      expect(updateBranch).toHaveBeenCalledWith("branch-1", { name: "Cairo", code: "CAI", isActive: false }),
    );
  });
});
