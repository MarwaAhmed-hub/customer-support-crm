import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import * as departmentsApi from "../../../departments/departmentsApi";
import type { Department } from "../../../departments/types";
import * as categoriesApi from "../categoriesApi";
import { TicketCategoryFormPage } from "../TicketCategoryFormPage";
import type { TicketCategory } from "../types";

const BILLING_DEPARTMENT: Department = {
  id: "dept-billing",
  name: "Billing",
  code: null,
  isActive: true,
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: "2026-01-01T00:00:00Z",
};

const EXISTING: TicketCategory = {
  id: "cat-1",
  name: "Complaints",
  description: null,
  isActive: true,
  departmentId: null,
  departmentName: null,
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: "2026-01-01T00:00:00Z",
};

function LandingProbe() {
  const location = useLocation();
  return <div data-testid="landed">landed on {location.pathname}</div>;
}

function renderCreate() {
  return render(
    <MemoryRouter initialEntries={["/tickets/categories/new"]}>
      <Routes>
        <Route path="/tickets/categories/new" element={<TicketCategoryFormPage />} />
        <Route path="/tickets/categories" element={<LandingProbe />} />
      </Routes>
    </MemoryRouter>,
  );
}

function renderEdit(id: string) {
  return render(
    <MemoryRouter initialEntries={[`/tickets/categories/${id}/edit`]}>
      <Routes>
        <Route path="/tickets/categories/:id/edit" element={<TicketCategoryFormPage />} />
        <Route path="/tickets/categories" element={<LandingProbe />} />
      </Routes>
    </MemoryRouter>,
  );
}

describe("TicketCategoryFormPage — department link", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("submits with no department by default", async () => {
    vi.spyOn(departmentsApi, "listDepartments").mockResolvedValue([]);
    const createTicketCategory = vi.spyOn(categoriesApi, "createTicketCategory").mockResolvedValue(EXISTING);

    renderCreate();
    await userEvent.type(screen.getByLabelText("Name"), "Complaints");
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    await screen.findByTestId("landed");
    expect(createTicketCategory).toHaveBeenCalledWith({ name: "Complaints", description: null, departmentId: null });
  });

  it("lets the user pick a department and includes it in the create payload", async () => {
    vi.spyOn(departmentsApi, "listDepartments").mockResolvedValue([BILLING_DEPARTMENT]);
    const createTicketCategory = vi.spyOn(categoriesApi, "createTicketCategory").mockResolvedValue(EXISTING);

    renderCreate();
    await userEvent.type(screen.getByLabelText("Name"), "Billing Issue");
    await userEvent.click(screen.getByLabelText("Department"));
    await userEvent.click(await screen.findByRole("option", { name: "Billing" }));
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    await screen.findByTestId("landed");
    expect(createTicketCategory).toHaveBeenCalledWith({ name: "Billing Issue", description: null, departmentId: "dept-billing" });
  });

  it("preloads the existing department on edit and can clear it back to none", async () => {
    vi.spyOn(departmentsApi, "listDepartments").mockResolvedValue([BILLING_DEPARTMENT]);
    vi.spyOn(categoriesApi, "getTicketCategory").mockResolvedValue({ ...EXISTING, departmentId: "dept-billing", departmentName: "Billing" });
    const updateTicketCategory = vi.spyOn(categoriesApi, "updateTicketCategory").mockResolvedValue(EXISTING);

    renderEdit("cat-1");
    await screen.findByDisplayValue("Complaints");
    expect(screen.getByText("Billing")).toBeInTheDocument();

    await userEvent.click(screen.getByLabelText("Department"));
    await userEvent.click(await screen.findByRole("option", { name: "— none —" }));
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    await screen.findByTestId("landed");
    expect(updateTicketCategory).toHaveBeenCalledWith("cat-1", { name: "Complaints", description: null, isActive: true, departmentId: null });
  });
});
