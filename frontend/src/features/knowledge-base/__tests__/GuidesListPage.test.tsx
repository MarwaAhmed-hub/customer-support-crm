import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AuthContext } from "../../auth/AuthContext";
import type { AuthState } from "../../auth/AuthContext";
import * as knowledgeBaseApi from "../knowledgeBaseApi";
import { GuidesListPage } from "../GuidesListPage";
import type { KbGuide } from "../types";

function guide(overrides: Partial<KbGuide> = {}): KbGuide {
  return {
    id: "guide-1",
    title: "Set up two-factor auth",
    description: "Enable 2FA on your account",
    categoryId: "cat-1",
    categoryName: "Account",
    audience: "CustomerFacing",
    status: "Published",
    steps: [{ order: 0, instruction: "Open settings" }],
    createdAtUtc: "2026-08-01T00:00:00Z",
    updatedAtUtc: null,
    publishedAtUtc: "2026-08-01T00:00:00Z",
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
    user: { id: "u-1", email: "person@local.test", displayName: "A Person" },
    login: async () => undefined,
    logout: () => undefined,
  };
}

function renderPage(permissions: string[]) {
  return render(
    <AuthContext value={stubAuth(permissions)}>
      <MemoryRouter>
        <GuidesListPage />
      </MemoryRouter>
    </AuthContext>,
  );
}

describe("GuidesListPage", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.spyOn(knowledgeBaseApi, "listCategories").mockResolvedValue([{ id: "cat-1", name: "Account", isActive: true }]);
  });

  it("renders published guides for an agent, including the step count", async () => {
    vi.spyOn(knowledgeBaseApi, "listGuides").mockResolvedValue([guide()]);

    renderPage(["knowledgebase.guides.view", "knowledgebase.guides.view.internal"]);

    expect(await screen.findByText("Set up two-factor auth")).toBeInTheDocument();
    expect(screen.getByText("1")).toBeInTheDocument();
  });

  it("shows the New button for a manager but not for an agent", async () => {
    vi.spyOn(knowledgeBaseApi, "listGuides").mockResolvedValue([guide()]);

    const { unmount } = renderPage(["knowledgebase.guides.view", "knowledgebase.guides.manage"]);
    expect(await screen.findByRole("link", { name: /New/ })).toBeInTheDocument();
    unmount();

    renderPage(["knowledgebase.guides.view"]);
    await screen.findByText("Set up two-factor auth");
    expect(screen.queryByRole("link", { name: /New/ })).not.toBeInTheDocument();
  });
});
