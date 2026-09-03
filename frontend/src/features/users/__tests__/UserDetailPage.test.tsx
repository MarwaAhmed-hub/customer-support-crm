import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AxiosError, AxiosHeaders } from "axios";
import type { AxiosResponse, InternalAxiosRequestConfig } from "axios";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AuthContext } from "../../auth/AuthContext";
import type { AuthState } from "../../auth/AuthContext";
import * as rolesApi from "../../roles/rolesApi";
import type { Role } from "../../roles/types";
import * as usersApi from "../usersApi";
import { UserDetailPage } from "../UserDetailPage";
import type { UserDetail } from "../types";

const DETAIL: UserDetail = {
  id: "u-1",
  email: "person@local.test",
  displayName: "A Person",
  isActive: true,
  createdAt: "2026-01-01T00:00:00Z",
  roles: [{ id: "role-agent", name: "Agent" }],
  departmentId: null,
  departmentName: null,
  branchId: null,
  branchName: null,
};

const AGENT_ROLE: Role = { id: "role-agent", name: "Agent", description: null, isSystem: true, permissions: [] };
const MANAGER_ROLE: Role = { id: "role-manager", name: "Manager", description: null, isSystem: true, permissions: [] };

function axiosErrorWith(status: number, data: unknown = {}): AxiosError {
  const config = { url: "/users/u-1", headers: new AxiosHeaders() } as InternalAxiosRequestConfig;
  const error = new AxiosError("failed", String(status), config);
  error.response = { status, statusText: "", data, headers: {}, config } as AxiosResponse;
  return error;
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

function renderAt(id: string, permissions: string[] = ["users.view", "users.update"]) {
  return render(
    <AuthContext value={stubAuth(permissions)}>
      <MemoryRouter initialEntries={[`/users/${id}`]}>
        <Routes>
          <Route path="/users/:id" element={<UserDetailPage />} />
        </Routes>
      </MemoryRouter>
    </AuthContext>,
  );
}

describe("UserDetailPage", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("renders the fetched user's details", async () => {
    vi.spyOn(usersApi, "getUser").mockResolvedValue(DETAIL);

    renderAt("u-1");

    expect(await screen.findByRole("heading", { name: "A Person" })).toBeInTheDocument();
    expect(screen.getByText("person@local.test")).toBeInTheDocument();
    expect(screen.getByText("Active")).toBeInTheDocument();
  });

  it("shows the assigned department and branch, or '—' when unassigned", async () => {
    vi.spyOn(usersApi, "getUser").mockResolvedValue({
      ...DETAIL,
      departmentId: "dept-1",
      departmentName: "Support",
      branchId: null,
      branchName: null,
    });

    renderAt("u-1");

    expect(await screen.findByText("Support")).toBeInTheDocument();
    expect(screen.getAllByText("—")).not.toHaveLength(0);
  });

  it("shows a not-found message for a 404", async () => {
    vi.spyOn(usersApi, "getUser").mockRejectedValue(axiosErrorWith(404));

    renderAt("missing");

    expect(await screen.findByText("User not found.")).toBeInTheDocument();
  });

  it("shows a generic error for a non-404 failure", async () => {
    vi.spyOn(usersApi, "getUser").mockRejectedValue(new Error("network"));

    renderAt("u-1");

    expect(await screen.findByRole("alert")).toHaveTextContent("Could not load this user. Please try again.");
  });

  it("toggling deactivate updates the displayed status", async () => {
    vi.spyOn(usersApi, "getUser").mockResolvedValue(DETAIL);
    const setUserActive = vi.spyOn(usersApi, "setUserActive").mockResolvedValue({ ...DETAIL, isActive: false });

    renderAt("u-1");
    await screen.findByText("Active");

    await userEvent.click(screen.getByRole("button", { name: "Deactivate" }));

    expect(setUserActive).toHaveBeenCalledWith("u-1", false);
    expect(await screen.findByText("Inactive")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Activate" })).toBeInTheDocument();
  });

  it("shows the user's current roles", async () => {
    vi.spyOn(usersApi, "getUser").mockResolvedValue(DETAIL);

    renderAt("u-1", ["users.view"]);

    expect(await screen.findByText("Agent")).toBeInTheDocument();
  });

  it("hides role assignment controls without permissions.assign", async () => {
    vi.spyOn(usersApi, "getUser").mockResolvedValue(DETAIL);

    renderAt("u-1", ["users.view"]);
    await screen.findByText("Agent");

    expect(screen.queryByLabelText("Assign role")).not.toBeInTheDocument();
  });

  it("assigns a role when permissions.assign is granted", async () => {
    vi.spyOn(usersApi, "getUser").mockResolvedValue(DETAIL);
    vi.spyOn(rolesApi, "listRoles").mockResolvedValue([AGENT_ROLE, MANAGER_ROLE]);
    const assignRoleToUser = vi
      .spyOn(rolesApi, "assignRoleToUser")
      .mockResolvedValue([{ id: "role-agent", name: "Agent" }, { id: "role-manager", name: "Manager" }]);

    renderAt("u-1", ["users.view", "permissions.assign"]);
    await screen.findByText("Agent");

    await userEvent.click(screen.getByLabelText("Assign role"));
    await userEvent.click(await screen.findByRole("option", { name: "Manager" }));
    await userEvent.click(screen.getByRole("button", { name: "Assign" }));

    await waitFor(() => expect(assignRoleToUser).toHaveBeenCalledWith("u-1", "role-manager"));
    expect(await screen.findByText("Manager")).toBeInTheDocument();
  });

  it("removes a role and shows a friendly error for the last-administrator guard", async () => {
    vi.spyOn(usersApi, "getUser").mockResolvedValue(DETAIL);
    vi.spyOn(rolesApi, "listRoles").mockResolvedValue([AGENT_ROLE, MANAGER_ROLE]);
    vi.spyOn(rolesApi, "removeRoleFromUser").mockRejectedValue(axiosErrorWith(400, { error: "last_administrator" }));

    renderAt("u-1", ["users.view", "permissions.assign"]);
    await screen.findByText("Agent");

    const chip = screen.getByText("Agent").closest(".MuiChip-root");
    const deleteIcon = chip?.querySelector(".MuiChip-deleteIcon");
    expect(deleteIcon).not.toBeNull();
    await userEvent.click(deleteIcon!);

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "This is the last administrator — assign Administrator to another user first.",
    );
  });
});
