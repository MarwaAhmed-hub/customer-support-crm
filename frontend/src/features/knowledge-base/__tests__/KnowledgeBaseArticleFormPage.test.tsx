import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AuthContext } from "../../auth/AuthContext";
import type { AuthState } from "../../auth/AuthContext";
import * as knowledgeBaseApi from "../knowledgeBaseApi";
import { KnowledgeBaseArticleFormPage } from "../KnowledgeBaseArticleFormPage";
import type { KnowledgeBaseArticle, KnowledgeBaseCategory } from "../types";

const CATEGORY: KnowledgeBaseCategory = { id: "cat-1", name: "Account", isActive: true };

const EXISTING: KnowledgeBaseArticle = {
  id: "kb-1",
  contentType: "HelpArticle",
  audience: "CustomerFacing",
  status: "Draft",
  title: "Resetting your password",
  body: "Step by step guide.",
  categoryId: "cat-1",
  categoryName: "Account",
  createdAtUtc: "2026-08-01T00:00:00Z",
  updatedAtUtc: null,
  publishedAtUtc: null,
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

function LandingProbe() {
  const location = useLocation();
  return <div data-testid="landed">landed on {location.pathname}</div>;
}

function renderCreate(path: string, permissions: string[] = ["knowledgebase.articles.manage"]) {
  return render(
    <AuthContext value={stubAuth(permissions)}>
      <MemoryRouter initialEntries={[path]}>
        <Routes>
          <Route path="/knowledge-base/faqs/new" element={<KnowledgeBaseArticleFormPage />} />
          <Route path="/knowledge-base/articles/new" element={<KnowledgeBaseArticleFormPage />} />
          <Route path="/knowledge-base/faqs" element={<LandingProbe />} />
          <Route path="/knowledge-base/articles" element={<LandingProbe />} />
        </Routes>
      </MemoryRouter>
    </AuthContext>,
  );
}

function renderEdit(id: string, permissions: string[] = ["knowledgebase.articles.manage"]) {
  return render(
    <AuthContext value={stubAuth(permissions)}>
      <MemoryRouter initialEntries={[`/knowledge-base/articles/${id}/edit`]}>
        <Routes>
          <Route path="/knowledge-base/articles/:id/edit" element={<KnowledgeBaseArticleFormPage />} />
          <Route path="/knowledge-base/articles" element={<LandingProbe />} />
        </Routes>
      </MemoryRouter>
    </AuthContext>,
  );
}

describe("KnowledgeBaseArticleFormPage — create mode", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.spyOn(knowledgeBaseApi, "listCategories").mockResolvedValue([CATEGORY]);
  });

  it("submits a create payload with contentType=HelpArticle when routed via /knowledge-base/articles/new", async () => {
    const createArticle = vi.spyOn(knowledgeBaseApi, "createArticle").mockResolvedValue(EXISTING);

    renderCreate("/knowledge-base/articles/new");
    await userEvent.type(screen.getByLabelText("Title"), "Resetting your password");
    await userEvent.type(screen.getByLabelText("Content"), "Step by step guide.");
    await userEvent.click(screen.getByLabelText("Category"));
    await userEvent.click(await screen.findByRole("option", { name: "Account" }));
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    await vi.waitFor(() =>
      expect(createArticle).toHaveBeenCalledWith({
        contentType: "HelpArticle",
        audience: "CustomerFacing",
        title: "Resetting your password",
        body: "Step by step guide.",
        categoryId: "cat-1",
      }),
    );
    expect(await screen.findByTestId("landed")).toHaveTextContent("landed on /knowledge-base/articles");
  }, 10000);

  it("submits a create payload with contentType=Faq when routed via /knowledge-base/faqs/new", async () => {
    const createArticle = vi.spyOn(knowledgeBaseApi, "createArticle").mockResolvedValue(EXISTING);

    renderCreate("/knowledge-base/faqs/new");
    await userEvent.type(screen.getByLabelText("Question"), "How do I reset my password?");
    await userEvent.type(screen.getByLabelText("Answer"), "Click forgot password.");
    await userEvent.click(screen.getByLabelText("Category"));
    await userEvent.click(await screen.findByRole("option", { name: "Account" }));
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    await vi.waitFor(() => expect(createArticle).toHaveBeenCalledWith(expect.objectContaining({ contentType: "Faq" })));
  }, 10000);

  it("shows validation errors when required fields are blank", async () => {
    renderCreate("/knowledge-base/articles/new");

    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    expect(await screen.findByText("Title is required.")).toBeInTheDocument();
    expect(screen.getByText("Content is required.")).toBeInTheDocument();
    expect(screen.getByText("Category is required.")).toBeInTheDocument();
  });
});

describe("KnowledgeBaseArticleFormPage — edit mode", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.spyOn(knowledgeBaseApi, "listCategories").mockResolvedValue([CATEGORY]);
  });

  it("defaults the status display to the loaded article's status (Draft)", async () => {
    vi.spyOn(knowledgeBaseApi, "getArticle").mockResolvedValue(EXISTING);

    renderEdit("kb-1");

    expect(await screen.findByText("Draft", { selector: "strong" })).toBeInTheDocument();
  });

  it("shows the Publish button only when the caller has the publish permission", async () => {
    vi.spyOn(knowledgeBaseApi, "getArticle").mockResolvedValue(EXISTING);

    const { unmount } = renderEdit("kb-1", ["knowledgebase.articles.manage", "knowledgebase.articles.publish"]);
    expect(await screen.findByRole("button", { name: "Publish" })).toBeInTheDocument();
    unmount();

    renderEdit("kb-1", ["knowledgebase.articles.manage"]);
    await screen.findByDisplayValue("Resetting your password");
    expect(screen.queryByRole("button", { name: "Publish" })).not.toBeInTheDocument();
  });

  it("never sends contentType in the update payload", async () => {
    vi.spyOn(knowledgeBaseApi, "getArticle").mockResolvedValue(EXISTING);
    const updateArticle = vi.spyOn(knowledgeBaseApi, "updateArticle").mockResolvedValue(EXISTING);

    renderEdit("kb-1");
    await screen.findByDisplayValue("Resetting your password");
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    await vi.waitFor(() =>
      expect(updateArticle).toHaveBeenCalledWith("kb-1", {
        audience: "CustomerFacing",
        title: "Resetting your password",
        body: "Step by step guide.",
        categoryId: "cat-1",
      }),
    );
  });
});
