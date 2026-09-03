import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AuthContext } from "../../auth/AuthContext";
import type { AuthState } from "../../auth/AuthContext";
import * as departmentsApi from "../departmentsApi";
import { DepartmentsListPage } from "../DepartmentsListPage";
import type { Department } from "../types";

function department(overrides: Partial<Department> = {}): Department {
  return {
    id: "dept-1",
    name: "Support",
    code: null,
    isActive: true,
    createdAt: "2026-01-01T00:00:00Z",
    updatedAt: "2026-01-01T00:00:00Z",
    ...overrides,
  };
}

function stubAuth(permissions: string[]): AuthState {
  return {
    status: "authenticated",
    isAdmin: false,
    permissions,
    hasPermission: (code) => permissions.includes(code),
    hasAnyPermission: (codes) => codes.some((code) => permissions.includes(code)),
    user: { id: "current", email: "current@local.test", displayName: "Current User" },
    login: async () => undefined,
    logout: () => undefined,
  };
}

function renderPage(permissions: string[] = ["departments.view", "departments.create", "departments.update"]) {
  return render(
    <AuthContext value={stubAuth(permissions)}>
      <MemoryRouter>
        <DepartmentsListPage />
      </MemoryRouter>
    </AuthContext>,
  );
}

describe("DepartmentsListPage", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("shows a loading state before the fetch resolves", () => {
    vi.spyOn(departmentsApi, "listDepartments").mockReturnValue(new Promise(() => {}));

    renderPage();

    expect(screen.getByText("Loading…")).toBeInTheDocument();
  });

  it("renders the fetched departments", async () => {
    vi.spyOn(departmentsApi, "listDepartments").mockResolvedValue([
      department({ id: "dept-1", name: "Support", code: "SUP" }),
      department({ id: "dept-2", name: "Sales", isActive: false }),
    ]);

    renderPage();

    expect(await screen.findByText("Support")).toBeInTheDocument();
    expect(screen.getByText("SUP")).toBeInTheDocument();
    expect(screen.getByText("Sales")).toBeInTheDocument();
    expect(screen.getByText("Inactive")).toBeInTheDocument();
  });

  it("shows an empty state when there are no departments", async () => {
    vi.spyOn(departmentsApi, "listDepartments").mockResolvedValue([]);

    renderPage();

    expect(await screen.findByText("No departments found.")).toBeInTheDocument();
  });

  it("shows an error state when the fetch fails", async () => {
    vi.spyOn(departmentsApi, "listDepartments").mockRejectedValue(new Error("network"));

    renderPage();

    expect(await screen.findByRole("alert")).toHaveTextContent("Could not load departments. Please try again.");
  });

  it("refetches with includeInactive when 'Show inactive' is toggled", async () => {
    const listDepartments = vi.spyOn(departmentsApi, "listDepartments").mockResolvedValue([department()]);

    renderPage();
    await waitFor(() => expect(listDepartments).toHaveBeenLastCalledWith({ includeInactive: false }));

    await userEvent.click(screen.getByRole("switch", { name: "Show inactive" }));

    await waitFor(() => expect(listDepartments).toHaveBeenLastCalledWith({ includeInactive: true }));
  });

  it("hides the New department button when the caller lacks departments.create", async () => {
    vi.spyOn(departmentsApi, "listDepartments").mockResolvedValue([]);

    renderPage(["departments.view"]);
    await screen.findByText("No departments found.");

    expect(screen.queryByRole("link", { name: "New department" })).not.toBeInTheDocument();
  });

  it("hides the Edit action when the caller lacks departments.update", async () => {
    vi.spyOn(departmentsApi, "listDepartments").mockResolvedValue([department()]);

    renderPage(["departments.view"]);
    await screen.findByText("Support");

    expect(screen.queryByRole("link", { name: "Edit" })).not.toBeInTheDocument();
  });
});
