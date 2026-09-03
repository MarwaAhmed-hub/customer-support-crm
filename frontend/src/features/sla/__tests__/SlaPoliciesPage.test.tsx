import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AuthContext } from "../../auth/AuthContext";
import type { AuthState } from "../../auth/AuthContext";
import * as slaPoliciesApi from "../slaPoliciesApi";
import { SlaPoliciesPage } from "../SlaPoliciesPage";
import type { SlaPolicy } from "../types";

const DEFAULT_POLICY: SlaPolicy = {
  id: "policy-1",
  priorityId: null,
  priorityName: null,
  name: "Default SLA",
  firstResponseMinutes: 30,
  resolutionMinutes: 240,
  isActive: true,
};

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
      <SlaPoliciesPage />
    </AuthContext>,
  );
}

describe("SlaPoliciesPage", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("shows the policy read-only for a caller without system.update", async () => {
    vi.spyOn(slaPoliciesApi, "listSlaPolicies").mockResolvedValue([DEFAULT_POLICY]);

    renderPage(["system.view"]);

    expect(await screen.findByText("Default SLA")).toBeInTheDocument();
    expect(screen.getByText("Default (every priority)")).toBeInTheDocument();
    expect(screen.getByText("30")).toBeInTheDocument();
    expect(screen.getByText("240")).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Save" })).not.toBeInTheDocument();
  });

  it("lets a caller with system.update edit and save the minutes", async () => {
    vi.spyOn(slaPoliciesApi, "listSlaPolicies").mockResolvedValue([DEFAULT_POLICY]);
    const update = vi.spyOn(slaPoliciesApi, "updateSlaPolicy").mockResolvedValue({ ...DEFAULT_POLICY, firstResponseMinutes: 15 });

    renderPage(["system.view", "system.update"]);
    const firstResponseInput = await screen.findByDisplayValue("30");
    const saveButton = screen.getByRole("button", { name: "Save" });
    expect(saveButton).toBeDisabled();

    await userEvent.clear(firstResponseInput);
    await userEvent.type(firstResponseInput, "15");
    expect(saveButton).toBeEnabled();
    await userEvent.click(saveButton);

    await vi.waitFor(() =>
      expect(update).toHaveBeenCalledWith("policy-1", { firstResponseMinutes: 15, resolutionMinutes: 240, isActive: true }),
    );
    expect(await screen.findByText("Policy saved.")).toBeInTheDocument();
  });

  it("rejects a first response value below 1 minute without calling the API", async () => {
    vi.spyOn(slaPoliciesApi, "listSlaPolicies").mockResolvedValue([DEFAULT_POLICY]);
    const update = vi.spyOn(slaPoliciesApi, "updateSlaPolicy");

    renderPage(["system.view", "system.update"]);
    const firstResponseInput = await screen.findByDisplayValue("30");
    await userEvent.clear(firstResponseInput);
    await userEvent.type(firstResponseInput, "0");
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    expect(await screen.findByText(/at least 1/)).toBeInTheDocument();
    expect(update).not.toHaveBeenCalled();
  });
});
