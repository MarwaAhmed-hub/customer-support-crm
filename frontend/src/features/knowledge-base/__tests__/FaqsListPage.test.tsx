import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AuthContext } from "../../auth/AuthContext";
import type { AuthState } from "../../auth/AuthContext";
import { FaqsListPage } from "../FaqsListPage";
import * as knowledgeBaseApi from "../knowledgeBaseApi";
import type { KnowledgeBaseArticle } from "../types";

function article(overrides: Partial<KnowledgeBaseArticle> = {}): KnowledgeBaseArticle {
  return {
    id: "kb-1",
    contentType: "Faq",
    audience: "CustomerFacing",
    status: "Published",
    title: "How do I reset my password?",
    body: "Click forgot password on the login page.",
    categoryId: "cat-1",
    categoryName: "Account",
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
        <FaqsListPage />
      </MemoryRouter>
    </AuthContext>,
  );
}

describe("FaqsListPage", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.spyOn(knowledgeBaseApi, "listCategories").mockResolvedValue([{ id: "cat-1", name: "Account", isActive: true }]);
  });

  it("renders published FAQs for an agent", async () => {
    vi.spyOn(knowledgeBaseApi, "listArticles").mockResolvedValue([article()]);

    renderPage(["knowledgebase.articles.view", "knowledgebase.articles.view.internal"]);

    expect(await screen.findByText("How do I reset my password?")).toBeInTheDocument();
  });

  it("shows the New button for a manager but not for an agent", async () => {
    vi.spyOn(knowledgeBaseApi, "listArticles").mockResolvedValue([article()]);

    const { unmount } = renderPage(["knowledgebase.articles.view", "knowledgebase.articles.manage"]);
    expect(await screen.findByRole("link", { name: /New/ })).toBeInTheDocument();
    unmount();

    renderPage(["knowledgebase.articles.view"]);
    await screen.findByText("How do I reset my password?");
    expect(screen.queryByRole("link", { name: /New/ })).not.toBeInTheDocument();
  });

  it("filters by category", async () => {
    const listArticles = vi.spyOn(knowledgeBaseApi, "listArticles").mockResolvedValue([article()]);

    renderPage(["knowledgebase.articles.view"]);
    await screen.findByText("How do I reset my password?");
    listArticles.mockClear();

    await userEvent.click(screen.getByLabelText("Category"));
    await userEvent.click(await screen.findByRole("option", { name: "Account" }));

    await vi.waitFor(() =>
      expect(listArticles).toHaveBeenCalledWith(expect.objectContaining({ contentType: "Faq", categoryId: "cat-1" })),
    );
  });

  it("does not show manager-only status/audience filters for a non-manager", async () => {
    vi.spyOn(knowledgeBaseApi, "listArticles").mockResolvedValue([]);

    renderPage(["knowledgebase.articles.view"]);

    await screen.findByText("Nothing here yet.");
    expect(screen.queryByLabelText("Status")).not.toBeInTheDocument();
    expect(screen.queryByLabelText("Audience")).not.toBeInTheDocument();
  });
});
