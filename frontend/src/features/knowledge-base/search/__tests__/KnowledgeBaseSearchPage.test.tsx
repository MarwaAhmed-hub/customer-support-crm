import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import * as knowledgeBaseApi from "../../knowledgeBaseApi";
import * as knowledgeBaseSearchApi from "../knowledgeBaseSearchApi";
import { KnowledgeBaseSearchPage } from "../KnowledgeBaseSearchPage";
import type { KnowledgeBaseSearchResponse, KnowledgeBaseSearchResultItem } from "../types";

function item(overrides: Partial<KnowledgeBaseSearchResultItem> = {}): KnowledgeBaseSearchResultItem {
  return {
    id: "kb-1",
    type: "Faq",
    title: "How do I reset my password?",
    categoryId: "cat-1",
    categoryName: "Account",
    excerpt: "Click forgot password on the login page.",
    publishedAtUtc: "2026-08-01T00:00:00Z",
    ...overrides,
  };
}

function response(items: KnowledgeBaseSearchResultItem[]): KnowledgeBaseSearchResponse {
  return { page: 1, pageSize: 20, total: items.length, items };
}

function renderPage(initialEntries: string[] = ["/knowledge-base/search"]) {
  return render(
    <MemoryRouter initialEntries={initialEntries}>
      <Routes>
        <Route path="/knowledge-base/search" element={<KnowledgeBaseSearchPage />} />
        <Route path="/knowledge-base/faqs/:id" element={<div data-testid="faq-detail">faq detail</div>} />
        <Route path="/knowledge-base/articles/:id" element={<div data-testid="article-detail">article detail</div>} />
        <Route path="/knowledge-base/solutions/:id" element={<div data-testid="solution-detail">solution detail</div>} />
        <Route path="/knowledge-base/guides/:id" element={<div data-testid="guide-detail">guide detail</div>} />
      </Routes>
    </MemoryRouter>,
  );
}

describe("KnowledgeBaseSearchPage", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.spyOn(knowledgeBaseApi, "listCategories").mockResolvedValue([{ id: "cat-1", name: "Account", isActive: true }]);
  });

  it("renders an idle empty state and never calls the API when there is no query or filter", async () => {
    const search = vi.spyOn(knowledgeBaseSearchApi, "searchKnowledgeBase");

    renderPage();

    expect(await screen.findByText(/Start typing, or pick a type\/category/)).toBeInTheDocument();
    expect(search).not.toHaveBeenCalled();
  });

  it("debounces the query and calls the API once after typing settles", async () => {
    const search = vi.spyOn(knowledgeBaseSearchApi, "searchKnowledgeBase").mockResolvedValue(response([item()]));

    renderPage();
    await userEvent.type(screen.getByLabelText("Search"), "password");

    await vi.waitFor(() => expect(search).toHaveBeenCalledTimes(1), { timeout: 2000 });
    expect(search).toHaveBeenCalledWith(expect.objectContaining({ q: "password" }));
  }, 10000);

  it("renders result rows with a type chip, category, and excerpt", async () => {
    vi.spyOn(knowledgeBaseSearchApi, "searchKnowledgeBase").mockResolvedValue(response([item()]));

    renderPage();
    await userEvent.type(screen.getByLabelText("Search"), "password");

    expect(await screen.findByText("How do I reset my password?")).toBeInTheDocument();
    expect(screen.getByText("Click forgot password on the login page.")).toBeInTheDocument();
    const resultCard = screen.getByText("How do I reset my password?").closest(".MuiPaper-root");
    expect(resultCard).not.toBeNull();
    expect(resultCard).toHaveTextContent("FAQ");
    expect(resultCard).toHaveTextContent("Account");
  }, 10000);

  it("sends the selected content types when a type toggle is clicked", async () => {
    const search = vi.spyOn(knowledgeBaseSearchApi, "searchKnowledgeBase").mockResolvedValue(response([]));

    renderPage();
    await userEvent.click(screen.getByRole("button", { name: "Solution" }));

    await vi.waitFor(() => expect(search).toHaveBeenCalledWith(expect.objectContaining({ type: ["Solution"] })), { timeout: 2000 });
  });

  it("sends the selected category", async () => {
    const search = vi.spyOn(knowledgeBaseSearchApi, "searchKnowledgeBase").mockResolvedValue(response([]));

    renderPage();
    await userEvent.click(screen.getByLabelText("Category"));
    await userEvent.click(await screen.findByRole("option", { name: "Account" }));

    await vi.waitFor(() => expect(search).toHaveBeenCalledWith(expect.objectContaining({ categoryId: "cat-1" })), { timeout: 2000 });
  });

  it("routes to the correct detail page per content type when a result is clicked", async () => {
    vi.spyOn(knowledgeBaseSearchApi, "searchKnowledgeBase").mockResolvedValue(response([item({ type: "Solution", id: "sol-1", title: "Printer offline" })]));

    renderPage();
    await userEvent.type(screen.getByLabelText("Search"), "printer");
    await userEvent.click(await screen.findByText("Printer offline"));

    expect(await screen.findByTestId("solution-detail")).toBeInTheDocument();
  }, 10000);

  it("shows a no-results message when the search returns nothing", async () => {
    vi.spyOn(knowledgeBaseSearchApi, "searchKnowledgeBase").mockResolvedValue(response([]));

    renderPage();
    await userEvent.type(screen.getByLabelText("Search"), "nonexistent");

    expect(await screen.findByText("No results found.")).toBeInTheDocument();
  }, 10000);
});
