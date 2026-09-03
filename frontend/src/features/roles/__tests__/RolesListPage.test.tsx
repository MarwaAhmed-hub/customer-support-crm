import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AuthContext } from "../../auth/AuthContext";
import type { AuthState } from "../../auth/AuthContext";
import * as rolesApi from "../rolesApi";
import { RolesListPage } from "../RolesListPage";
import type { Role } from "../types";

function role(overrides: Partial<Role> = {}): Role {
  return {
    id: "role-1",
    name: "Agent",
    description: "Front-line support",
    isSystem: true,
    permissions: ["tickets.view", "tickets.update"],
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

function renderPage(permissions: string[]) {
  return render(
    <AuthContext value={stubAuth(permissions)}>
      <MemoryRouter>
        <RolesListPage />
      </MemoryRouter>
    </AuthContext>,
  );
}

describe("RolesListPage", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("renders the fetched roles", async () => {
    vi.spyOn(rolesApi, "listRoles").mockResolvedValue([role()]);

    renderPage(["roles.view"]);

    expect(await screen.findByText("Agent")).toBeInTheDocument();
    expect(screen.getByText("Front-line support")).toBeInTheDocument();
  });

  it("shows the New role button when the caller has roles.create", async () => {
    vi.spyOn(rolesApi, "listRoles").mockResolvedValue([]);

    renderPage(["roles.view", "roles.create"]);
    await screen.findByText("No roles found.");

    expect(screen.getByRole("link", { name: /new role/i })).toBeInTheDocument();
  });

  it("hides the New role button when roles.create is missing", async () => {
    vi.spyOn(rolesApi, "listRoles").mockResolvedValue([]);

    renderPage(["roles.view"]);
    await screen.findByText("No roles found.");

    expect(screen.queryByRole("link", { name: /new role/i })).not.toBeInTheDocument();
  });

  it("shows an error state when the fetch fails", async () => {
    vi.spyOn(rolesApi, "listRoles").mockRejectedValue(new Error("network"));

    renderPage(["roles.view"]);

    expect(await screen.findByRole("alert")).toHaveTextContent("Could not load roles. Please try again.");
  });
});
