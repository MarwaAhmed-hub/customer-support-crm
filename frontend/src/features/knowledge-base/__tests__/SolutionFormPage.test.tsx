import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AuthContext } from "../../auth/AuthContext";
import type { AuthState } from "../../auth/AuthContext";
import * as knowledgeBaseApi from "../knowledgeBaseApi";
import { SolutionFormPage } from "../SolutionFormPage";
import type { KbSolution, KnowledgeBaseCategory } from "../types";

const CATEGORY: KnowledgeBaseCategory = { id: "cat-1", name: "Account", isActive: true };

const EXISTING: KbSolution = {
  id: "sol-1",
  title: "Printer offline",
  problem: "Printer shows offline",
  solutionBody: "Restart the print spooler",
  categoryId: "cat-1",
  categoryName: "Account",
  audience: "CustomerFacing",
  status: "Draft",
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

function renderCreate(permissions: string[] = ["knowledgebase.solutions.manage"]) {
  return render(
    <AuthContext value={stubAuth(permissions)}>
      <MemoryRouter initialEntries={["/knowledge-base/solutions/new"]}>
        <Routes>
          <Route path="/knowledge-base/solutions/new" element={<SolutionFormPage />} />
          <Route path="/knowledge-base/solutions" element={<LandingProbe />} />
        </Routes>
      </MemoryRouter>
    </AuthContext>,
  );
}

function renderEdit(id: string, permissions: string[] = ["knowledgebase.solutions.manage"]) {
  return render(
    <AuthContext value={stubAuth(permissions)}>
      <MemoryRouter initialEntries={[`/knowledge-base/solutions/${id}/edit`]}>
        <Routes>
          <Route path="/knowledge-base/solutions/:id/edit" element={<SolutionFormPage />} />
          <Route path="/knowledge-base/solutions" element={<LandingProbe />} />
        </Routes>
      </MemoryRouter>
    </AuthContext>,
  );
}

describe("SolutionFormPage — create mode", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.spyOn(knowledgeBaseApi, "listCategories").mockResolvedValue([CATEGORY]);
  });

  it("submits without a status field — the server defaults to Draft", async () => {
    const createSolution = vi.spyOn(knowledgeBaseApi, "createSolution").mockResolvedValue(EXISTING);

    renderCreate();
    await userEvent.type(screen.getByLabelText("Title"), "Printer offline");
    await userEvent.type(screen.getByLabelText("Problem"), "Printer shows offline");
    await userEvent.type(screen.getByLabelText("Solution"), "Restart the print spooler");
    await userEvent.click(screen.getByLabelText("Category"));
    await userEvent.click(await screen.findByRole("option", { name: "Account" }));
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    await vi.waitFor(() =>
      expect(createSolution).toHaveBeenCalledWith({
        title: "Printer offline",
        problem: "Printer shows offline",
        solutionBody: "Restart the print spooler",
        categoryId: "cat-1",
        audience: "CustomerFacing",
      }),
    );
    const call = createSolution.mock.calls[0]![0] as unknown as Record<string, unknown>;
    expect(Object.keys(call)).not.toContain("status");
    expect(await screen.findByTestId("landed")).toHaveTextContent("landed on /knowledge-base/solutions");
  }, 10000);

  it("shows validation errors when required fields are blank", async () => {
    renderCreate();

    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    expect(await screen.findByText("Title is required.")).toBeInTheDocument();
    expect(screen.getByText("Problem is required.")).toBeInTheDocument();
    expect(screen.getByText("Solution is required.")).toBeInTheDocument();
    expect(screen.getByText("Category is required.")).toBeInTheDocument();
  });

  it("does not show a Status display or Publish button in create mode", () => {
    renderCreate(["knowledgebase.solutions.manage", "knowledgebase.solutions.publish"]);

    expect(screen.queryByText(/^Status:/)).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Publish" })).not.toBeInTheDocument();
  });
});

describe("SolutionFormPage — edit mode", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.spyOn(knowledgeBaseApi, "listCategories").mockResolvedValue([CATEGORY]);
  });

  it("prefills fields and shows the Draft status", async () => {
    vi.spyOn(knowledgeBaseApi, "getSolution").mockResolvedValue(EXISTING);

    renderEdit("sol-1");

    expect(await screen.findByDisplayValue("Printer offline")).toBeInTheDocument();
    expect(screen.getByText("Draft", { selector: "strong" })).toBeInTheDocument();
  });

  it("shows the Publish button only when the caller has the publish permission", async () => {
    vi.spyOn(knowledgeBaseApi, "getSolution").mockResolvedValue(EXISTING);

    const { unmount } = renderEdit("sol-1", ["knowledgebase.solutions.manage", "knowledgebase.solutions.publish"]);
    expect(await screen.findByRole("button", { name: "Publish" })).toBeInTheDocument();
    unmount();

    renderEdit("sol-1", ["knowledgebase.solutions.manage"]);
    await screen.findByDisplayValue("Printer offline");
    expect(screen.queryByRole("button", { name: "Publish" })).not.toBeInTheDocument();
  });
});
