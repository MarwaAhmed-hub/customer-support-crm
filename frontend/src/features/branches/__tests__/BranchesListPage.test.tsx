import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AuthContext } from "../../auth/AuthContext";
import type { AuthState } from "../../auth/AuthContext";
import * as branchesApi from "../branchesApi";
import { BranchesListPage } from "../BranchesListPage";
import type { Branch } from "../types";

function branch(overrides: Partial<Branch> = {}): Branch {
  return {
    id: "branch-1",
    name: "Cairo",
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

function renderPage(permissions: string[] = ["branches.view", "branches.create", "branches.update"]) {
  return render(
    <AuthContext value={stubAuth(permissions)}>
      <MemoryRouter>
        <BranchesListPage />
      </MemoryRouter>
    </AuthContext>,
  );
}

describe("BranchesListPage", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("shows a loading state before the fetch resolves", () => {
    vi.spyOn(branchesApi, "listBranches").mockReturnValue(new Promise(() => {}));

    renderPage();

    expect(screen.getByText("Loading…")).toBeInTheDocument();
  });

  it("renders the fetched branches", async () => {
    vi.spyOn(branchesApi, "listBranches").mockResolvedValue([
      branch({ id: "branch-1", name: "Cairo", code: "CAI" }),
      branch({ id: "branch-2", name: "Closed Branch", isActive: false }),
    ]);

    renderPage();

    expect(await screen.findByText("Cairo")).toBeInTheDocument();
    expect(screen.getByText("CAI")).toBeInTheDocument();
    expect(screen.getByText("Closed Branch")).toBeInTheDocument();
    expect(screen.getByText("Inactive")).toBeInTheDocument();
  });

  it("shows an empty state when there are no branches", async () => {
    vi.spyOn(branchesApi, "listBranches").mockResolvedValue([]);

    renderPage();

    expect(await screen.findByText("No branches found.")).toBeInTheDocument();
  });

  it("shows an error state when the fetch fails", async () => {
    vi.spyOn(branchesApi, "listBranches").mockRejectedValue(new Error("network"));

    renderPage();

    expect(await screen.findByRole("alert")).toHaveTextContent("Could not load branches. Please try again.");
  });

  it("refetches with includeInactive when 'Show inactive' is toggled", async () => {
    const listBranches = vi.spyOn(branchesApi, "listBranches").mockResolvedValue([branch()]);

    renderPage();
    await waitFor(() => expect(listBranches).toHaveBeenLastCalledWith({ includeInactive: false }));

    await userEvent.click(screen.getByRole("switch", { name: "Show inactive" }));

    await waitFor(() => expect(listBranches).toHaveBeenLastCalledWith({ includeInactive: true }));
  });

  it("hides the New branch button when the caller lacks branches.create", async () => {
    vi.spyOn(branchesApi, "listBranches").mockResolvedValue([]);

    renderPage(["branches.view"]);
    await screen.findByText("No branches found.");

    expect(screen.queryByRole("link", { name: "New branch" })).not.toBeInTheDocument();
  });

  it("hides the Edit action when the caller lacks branches.update", async () => {
    vi.spyOn(branchesApi, "listBranches").mockResolvedValue([branch()]);

    renderPage(["branches.view"]);
    await screen.findByText("Cairo");

    expect(screen.queryByRole("link", { name: "Edit" })).not.toBeInTheDocument();
  });
});
