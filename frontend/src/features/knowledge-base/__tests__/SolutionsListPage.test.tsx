import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AuthContext } from "../../auth/AuthContext";
import type { AuthState } from "../../auth/AuthContext";
import * as knowledgeBaseApi from "../knowledgeBaseApi";
import { SolutionsListPage } from "../SolutionsListPage";
import type { KbSolution } from "../types";

function solution(overrides: Partial<KbSolution> = {}): KbSolution {
  return {
    id: "sol-1",
    title: "Printer offline",
    problem: "Printer shows offline",
    solutionBody: "Restart the print spooler",
    categoryId: "cat-1",
    categoryName: "Account",
    audience: "CustomerFacing",
    status: "Published",
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
        <SolutionsListPage />
      </MemoryRouter>
    </AuthContext>,
  );
}

describe("SolutionsListPage", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.spyOn(knowledgeBaseApi, "listCategories").mockResolvedValue([{ id: "cat-1", name: "Account", isActive: true }]);
  });

  it("renders published solutions for an agent", async () => {
    vi.spyOn(knowledgeBaseApi, "listSolutions").mockResolvedValue([solution()]);

    renderPage(["knowledgebase.solutions.view", "knowledgebase.solutions.view.internal"]);

    expect(await screen.findByText("Printer offline")).toBeInTheDocument();
  });

  it("shows the New button for a manager but not for an agent", async () => {
    vi.spyOn(knowledgeBaseApi, "listSolutions").mockResolvedValue([solution()]);

    const { unmount } = renderPage(["knowledgebase.solutions.view", "knowledgebase.solutions.manage"]);
    expect(await screen.findByRole("link", { name: /New/ })).toBeInTheDocument();
    unmount();

    renderPage(["knowledgebase.solutions.view"]);
    await screen.findByText("Printer offline");
    expect(screen.queryByRole("link", { name: /New/ })).not.toBeInTheDocument();
  });

  it("shows the Publish action only when the caller has the publish permission", async () => {
    vi.spyOn(knowledgeBaseApi, "listSolutions").mockResolvedValue([solution({ status: "Draft" })]);

    const { unmount } = renderPage(["knowledgebase.solutions.view", "knowledgebase.solutions.manage", "knowledgebase.solutions.publish"]);
    expect(await screen.findByRole("button", { name: "Publish" })).toBeInTheDocument();
    unmount();

    renderPage(["knowledgebase.solutions.view", "knowledgebase.solutions.manage"]);
    await screen.findByText("Printer offline");
    expect(screen.queryByRole("button", { name: "Publish" })).not.toBeInTheDocument();
  });
});
