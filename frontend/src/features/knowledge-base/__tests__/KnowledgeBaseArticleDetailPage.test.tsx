import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AuthContext } from "../../auth/AuthContext";
import type { AuthState } from "../../auth/AuthContext";
import * as knowledgeBaseApi from "../knowledgeBaseApi";
import { KnowledgeBaseArticleDetailPage } from "../KnowledgeBaseArticleDetailPage";
import type { KnowledgeBaseArticle } from "../types";

const ARTICLE: KnowledgeBaseArticle = {
  id: "kb-1",
  contentType: "Faq",
  audience: "Internal",
  status: "Published",
  title: "How do we escalate a P1?",
  body: "Page the on-call manager.",
  categoryId: "cat-1",
  categoryName: "Internal Ops",
  createdAtUtc: "2026-08-01T00:00:00Z",
  updatedAtUtc: null,
  publishedAtUtc: "2026-08-01T00:00:00Z",
};

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
      <MemoryRouter initialEntries={["/knowledge-base/faqs/kb-1"]}>
        <Routes>
          <Route path="/knowledge-base/faqs/:id" element={<KnowledgeBaseArticleDetailPage />} />
        </Routes>
      </MemoryRouter>
    </AuthContext>,
  );
}

describe("KnowledgeBaseArticleDetailPage", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("renders the article's title and body", async () => {
    vi.spyOn(knowledgeBaseApi, "getArticle").mockResolvedValue(ARTICLE);

    renderPage(["knowledgebase.articles.view", "knowledgebase.articles.view.internal"]);

    expect(await screen.findByText("How do we escalate a P1?")).toBeInTheDocument();
    expect(screen.getByText("Page the on-call manager.")).toBeInTheDocument();
  });

  it("hides Edit/Publish and the internal status chips for a customer view", async () => {
    vi.spyOn(knowledgeBaseApi, "getArticle").mockResolvedValue(ARTICLE);

    renderPage(["knowledgebase.articles.view"]);

    await screen.findByText("How do we escalate a P1?");
    expect(screen.queryByRole("button", { name: "Edit" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Publish|Unpublish/ })).not.toBeInTheDocument();
    expect(screen.queryByText("Published")).not.toBeInTheDocument();
  });

  it("shows Edit and Unpublish for a manager", async () => {
    vi.spyOn(knowledgeBaseApi, "getArticle").mockResolvedValue(ARTICLE);

    renderPage(["knowledgebase.articles.view", "knowledgebase.articles.manage", "knowledgebase.articles.publish"]);

    expect(await screen.findByRole("button", { name: "Edit" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Unpublish" })).toBeInTheDocument();
  });

  it("shows a not-found message when the article cannot be loaded (leak-safe 404)", async () => {
    vi.spyOn(knowledgeBaseApi, "getArticle").mockRejectedValue({ isAxiosError: true, response: { status: 404 } });

    renderPage(["knowledgebase.articles.view"]);

    expect(await screen.findByText("This item could not be found.")).toBeInTheDocument();
  });
});
