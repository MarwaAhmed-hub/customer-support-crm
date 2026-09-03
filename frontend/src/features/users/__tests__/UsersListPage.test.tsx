import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AuthContext } from "../../auth/AuthContext";
import type { AuthState } from "../../auth/AuthContext";
import * as branchesApi from "../../branches/branchesApi";
import type { Branch } from "../../branches/types";
import * as departmentsApi from "../../departments/departmentsApi";
import type { Department } from "../../departments/types";
import * as usersApi from "../usersApi";
import { UsersListPage } from "../UsersListPage";
import type { PagedResult, UserListItem } from "../types";

function user(overrides: Partial<UserListItem> = {}): UserListItem {
  return {
    id: "u-1",
    email: "person@local.test",
    displayName: "A Person",
    isActive: true,
    departmentId: null,
    departmentName: null,
    branchId: null,
    branchName: null,
    ...overrides,
  };
}

const SUPPORT_DEPARTMENT: Department = {
  id: "dept-1",
  name: "Support",
  code: null,
  isActive: true,
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: "2026-01-01T00:00:00Z",
};

const CAIRO_BRANCH: Branch = {
  id: "branch-1",
  name: "Cairo",
  code: null,
  isActive: true,
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: "2026-01-01T00:00:00Z",
};

function paged(items: UserListItem[], overrides: Partial<PagedResult<UserListItem>> = {}): PagedResult<UserListItem> {
  return { items, page: 1, pageSize: 20, total: items.length, ...overrides };
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

function renderPage(permissions: string[] = ["users.view", "users.create", "users.update"]) {
  return render(
    <AuthContext value={stubAuth(permissions)}>
      <MemoryRouter>
        <UsersListPage />
      </MemoryRouter>
    </AuthContext>,
  );
}

describe("UsersListPage", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    // Most tests in this file exercise the table itself, not the filter dropdowns — default their
    // sources to empty so those tests don't need to know about them.
    vi.spyOn(departmentsApi, "listDepartments").mockResolvedValue([]);
    vi.spyOn(branchesApi, "listBranches").mockResolvedValue([]);
  });

  it("shows a loading state before the fetch resolves", () => {
    vi.spyOn(usersApi, "listUsers").mockReturnValue(new Promise(() => {}));

    renderPage();

    expect(screen.getByText("Loading…")).toBeInTheDocument();
  });

  it("renders the fetched users", async () => {
    vi.spyOn(usersApi, "listUsers").mockResolvedValue(
      paged([
        user({ id: "u-1", email: "one@local.test", displayName: "User One" }),
        user({ id: "u-2", email: "two@local.test", displayName: "User Two", isActive: false }),
      ]),
    );

    renderPage();

    expect(await screen.findByText("one@local.test")).toBeInTheDocument();
    expect(screen.getByText("User One")).toBeInTheDocument();
    expect(screen.getByText("two@local.test")).toBeInTheDocument();
    expect(screen.getByText("Active")).toBeInTheDocument();
    expect(screen.getByText("Inactive")).toBeInTheDocument();
  });

  it("shows the assigned department and branch, or '—' when unassigned", async () => {
    vi.spyOn(usersApi, "listUsers").mockResolvedValue(
      paged([
        user({ id: "u-1", email: "one@local.test", departmentId: "dept-1", departmentName: "Support" }),
        user({ id: "u-2", email: "two@local.test" }),
      ]),
    );

    renderPage();

    expect(await screen.findByText("Support")).toBeInTheDocument();
    expect(screen.getAllByText("—").length).toBeGreaterThanOrEqual(2); // u-2's department AND branch cells
  });

  it("filters by the selected department", async () => {
    vi.spyOn(departmentsApi, "listDepartments").mockResolvedValue([SUPPORT_DEPARTMENT]);
    vi.spyOn(branchesApi, "listBranches").mockResolvedValue([CAIRO_BRANCH]);
    const listUsers = vi.spyOn(usersApi, "listUsers").mockResolvedValue(paged([]));

    renderPage();
    await waitFor(() => expect(listUsers).toHaveBeenCalledTimes(1));

    await userEvent.click(screen.getByLabelText("Department"));
    await userEvent.click(await screen.findByRole("option", { name: "Support" }));

    await waitFor(() =>
      expect(listUsers).toHaveBeenLastCalledWith({ page: 1, pageSize: 20, departmentId: "dept-1" }),
    );
  });

  it("filters by the selected branch", async () => {
    vi.spyOn(departmentsApi, "listDepartments").mockResolvedValue([SUPPORT_DEPARTMENT]);
    vi.spyOn(branchesApi, "listBranches").mockResolvedValue([CAIRO_BRANCH]);
    const listUsers = vi.spyOn(usersApi, "listUsers").mockResolvedValue(paged([]));

    renderPage();
    await waitFor(() => expect(listUsers).toHaveBeenCalledTimes(1));

    await userEvent.click(screen.getByLabelText("Branch"));
    await userEvent.click(await screen.findByRole("option", { name: "Cairo" }));

    await waitFor(() =>
      expect(listUsers).toHaveBeenLastCalledWith({ page: 1, pageSize: 20, branchId: "branch-1" }),
    );
  });

  it("shows an empty state when there are no users", async () => {
    vi.spyOn(usersApi, "listUsers").mockResolvedValue(paged([]));

    renderPage();

    expect(await screen.findByText("No users found.")).toBeInTheDocument();
  });

  it("shows an error state when the fetch fails", async () => {
    vi.spyOn(usersApi, "listUsers").mockRejectedValue(new Error("network"));

    renderPage();

    expect(await screen.findByRole("alert")).toHaveTextContent("Could not load users. Please try again.");
  });

  it("debounces the search input before refetching", async () => {
    const listUsers = vi.spyOn(usersApi, "listUsers").mockResolvedValue(paged([]));

    renderPage();
    await waitFor(() => expect(listUsers).toHaveBeenCalledTimes(1));

    fireEvent.change(screen.getByLabelText("Search users"), { target: { value: "jane" } });

    // Not yet — still inside the debounce window (real timers: fake timers don't reliably drive
    // React's effect scheduler in this setup).
    await new Promise((resolve) => setTimeout(resolve, 150));
    expect(listUsers).toHaveBeenCalledTimes(1);

    await waitFor(() =>
      expect(listUsers).toHaveBeenLastCalledWith({ page: 1, pageSize: 20, search: "jane" }),
    );
  }, 10000);

  it("paginates using the Next and Previous buttons", async () => {
    const listUsers = vi.spyOn(usersApi, "listUsers").mockImplementation((params) =>
      Promise.resolve(
        paged(
          [user({ id: `u-${String(params?.page ?? 1)}`, email: `page${String(params?.page ?? 1)}@local.test` })],
          { page: params?.page ?? 1, total: 40 },
        ),
      ),
    );

    renderPage();
    expect(await screen.findByText("page1@local.test")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Previous" })).toBeDisabled();

    await userEvent.click(screen.getByRole("button", { name: "Next" }));

    expect(await screen.findByText("page2@local.test")).toBeInTheDocument();
    expect(listUsers).toHaveBeenLastCalledWith({ page: 2, pageSize: 20 });
    expect(screen.getByRole("button", { name: "Previous" })).toBeEnabled();
  });

  it("toggles a user's active status", async () => {
    vi.spyOn(usersApi, "listUsers").mockResolvedValue(
      paged([user({ id: "u-1", email: "active@local.test", isActive: true })]),
    );
    const setUserActive = vi
      .spyOn(usersApi, "setUserActive")
      .mockResolvedValue({ ...user({ id: "u-1", email: "active@local.test", isActive: false }), createdAt: "", roles: [] });

    renderPage();
    await screen.findByText("active@local.test");

    await userEvent.click(screen.getByRole("button", { name: "Deactivate" }));

    expect(setUserActive).toHaveBeenCalledWith("u-1", false);
    expect(await screen.findByText("Inactive")).toBeInTheDocument();
  });

  it("hides the New user button when the caller lacks users.create", async () => {
    vi.spyOn(usersApi, "listUsers").mockResolvedValue(paged([]));

    renderPage(["users.view"]);
    await screen.findByText("No users found.");

    expect(screen.queryByRole("button", { name: "New user" })).not.toBeInTheDocument();
  });

  it("hides Edit and Deactivate when the caller lacks users.update", async () => {
    vi.spyOn(usersApi, "listUsers").mockResolvedValue(
      paged([user({ id: "u-1", email: "one@local.test", displayName: "User One" })]),
    );

    renderPage(["users.view"]);
    await screen.findByText("one@local.test");

    expect(screen.getByRole("link", { name: "View" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Edit" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Deactivate" })).not.toBeInTheDocument();
  });
});
