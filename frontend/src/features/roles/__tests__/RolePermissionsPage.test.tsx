import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import * as rolesApi from "../rolesApi";
import { RolePermissionsPage } from "../RolePermissionsPage";
import type { PermissionCategory, Role } from "../types";

const CATEGORIES: PermissionCategory[] = [
  {
    category: "tickets",
    permissions: [
      { code: "tickets.view", category: "tickets", displayName: "View tickets", description: null },
      { code: "tickets.update", category: "tickets", displayName: "Update tickets", description: null },
    ],
  },
];

function renderAt(id: string) {
  return render(
    <MemoryRouter initialEntries={[`/roles/${id}/permissions`]}>
      <Routes>
        <Route path="/roles/:id/permissions" element={<RolePermissionsPage />} />
      </Routes>
    </MemoryRouter>,
  );
}

describe("RolePermissionsPage", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("checkboxes reflect the role's current permission selection", async () => {
    const agent: Role = { id: "role-1", name: "Agent", description: null, isSystem: true, permissions: ["tickets.view"] };
    vi.spyOn(rolesApi, "getRole").mockResolvedValue(agent);
    vi.spyOn(rolesApi, "listEligiblePermissions").mockResolvedValue(CATEGORIES);

    renderAt("role-1");

    expect(await screen.findByLabelText("View tickets")).toBeChecked();
    expect(screen.getByLabelText("Update tickets")).not.toBeChecked();
  });

  it("Save posts the selected permission set", async () => {
    const agent: Role = { id: "role-1", name: "Agent", description: null, isSystem: true, permissions: ["tickets.view"] };
    vi.spyOn(rolesApi, "getRole").mockResolvedValue(agent);
    vi.spyOn(rolesApi, "listEligiblePermissions").mockResolvedValue(CATEGORIES);
    const replace = vi.spyOn(rolesApi, "replaceRolePermissions").mockResolvedValue({
      ...agent,
      permissions: ["tickets.view", "tickets.update"],
    });

    renderAt("role-1");
    await screen.findByLabelText("View tickets");

    await userEvent.click(screen.getByLabelText("Update tickets"));
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() =>
      expect(replace).toHaveBeenCalledWith("role-1", expect.arrayContaining(["tickets.view", "tickets.update"])),
    );
  });

  it("shows the Administrator role as read-only with every permission checked and disabled", async () => {
    const administrator: Role = {
      id: "role-admin",
      name: "Administrator",
      description: null,
      isSystem: true,
      permissions: ["tickets.view", "tickets.update"],
    };
    vi.spyOn(rolesApi, "getRole").mockResolvedValue(administrator);
    vi.spyOn(rolesApi, "listEligiblePermissions").mockResolvedValue(CATEGORIES);

    renderAt("role-admin");

    const checkbox = await screen.findByLabelText("View tickets");
    expect(checkbox).toBeChecked();
    expect(checkbox).toBeDisabled();
    expect(screen.getByRole("button", { name: "Save" })).toBeDisabled();
  });

  it("only renders the Customer role's eligible categories, never an admin-only row even disabled", async () => {
    const customer: Role = { id: "role-customer", name: "Customer", description: null, isSystem: true, permissions: ["portal.access"] };
    const CUSTOMER_ELIGIBLE: PermissionCategory[] = [
      {
        category: "portal",
        permissions: [{ code: "portal.access", category: "portal", displayName: "Access the customer portal", description: null }],
      },
      {
        category: "tickets",
        permissions: [
          { code: "tickets.view", category: "tickets", displayName: "View tickets", description: null },
          { code: "tickets.create", category: "tickets", displayName: "Create tickets", description: null },
          { code: "tickets.update", category: "tickets", displayName: "Update tickets", description: null },
        ],
      },
    ];
    vi.spyOn(rolesApi, "getRole").mockResolvedValue(customer);
    const eligible = vi.spyOn(rolesApi, "listEligiblePermissions").mockResolvedValue(CUSTOMER_ELIGIBLE);

    renderAt("role-customer");

    expect(await screen.findByLabelText("Access the customer portal")).toBeChecked();
    expect(screen.getByLabelText("View tickets")).toBeInTheDocument();
    // Not merely unchecked/disabled — absent from the DOM entirely, because the backend never sent
    // them for a Customer role (the Eligible Permissions Matrix is enforced server-side, not hidden
    // client-side).
    expect(screen.queryByLabelText(/view users/i)).not.toBeInTheDocument();
    expect(screen.queryByLabelText(/view roles/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/^users$/i)).not.toBeInTheDocument();
    expect(screen.queryByText(/^roles$/i)).not.toBeInTheDocument();
    expect(eligible).toHaveBeenCalledWith("role-customer");
  });
});
