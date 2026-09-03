import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AuthContext } from "../../auth/AuthContext";
import type { AuthState } from "../../auth/AuthContext";
import { PermissionRoute } from "../../auth/PermissionRoute";
import { BrandingContext } from "../../settings/BrandingContext";
import type { BrandingState } from "../../settings/BrandingContext";
import * as knowledgeBaseApi from "../knowledgeBaseApi";
import { KnowledgeBaseCategoriesPage } from "../KnowledgeBaseCategoriesPage";
import type { KnowledgeBaseCategory } from "../types";

const BRANDING_STUB: BrandingState = {
  branding: {
    applicationName: "Customer Support CRM",
    brandDisplayName: "Customer Support CRM",
    logoUrl: null,
    primaryColor: "#1976D2",
    secondaryColor: "#9C27B0",
  },
  refresh: () => undefined,
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

function renderGuarded(permissions: string[]) {
  return render(
    <AuthContext value={stubAuth(permissions)}>
      <BrandingContext value={BRANDING_STUB}>
        <MemoryRouter initialEntries={["/knowledge-base/categories"]}>
          <Routes>
            <Route path="/" element={<div data-testid="home">home</div>} />
            <Route
              path="/knowledge-base/categories"
              element={
                <PermissionRoute required="knowledgebase.categories.manage">
                  <KnowledgeBaseCategoriesPage />
                </PermissionRoute>
              }
            />
          </Routes>
        </MemoryRouter>
      </BrandingContext>
    </AuthContext>,
  );
}

function renderDirect() {
  return render(
    <AuthContext value={stubAuth(["knowledgebase.categories.manage"])}>
      <MemoryRouter>
        <KnowledgeBaseCategoriesPage />
      </MemoryRouter>
    </AuthContext>,
  );
}

describe("KnowledgeBaseCategoriesPage — route guard", () => {
  it("redirects a non-manager to Home", () => {
    renderGuarded([]);

    expect(screen.getByTestId("home")).toBeInTheDocument();
  });

  it("renders for a caller with knowledgebase.categories.manage", async () => {
    vi.spyOn(knowledgeBaseApi, "listCategories").mockResolvedValue([]);

    renderGuarded(["knowledgebase.categories.manage"]);

    expect(await screen.findByText("Knowledge Base Categories")).toBeInTheDocument();
  });
});

describe("KnowledgeBaseCategoriesPage — behavior", () => {
  const EXISTING: KnowledgeBaseCategory = { id: "cat-1", name: "Billing", isActive: true };

  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("renders the list returned by the API", async () => {
    vi.spyOn(knowledgeBaseApi, "listCategories").mockResolvedValue([EXISTING]);

    renderDirect();

    expect(await screen.findByText("Billing")).toBeInTheDocument();
  });

  it("creates a new category and reloads the list", async () => {
    vi.spyOn(knowledgeBaseApi, "listCategories").mockResolvedValueOnce([]).mockResolvedValueOnce([EXISTING]);
    const createCategory = vi.spyOn(knowledgeBaseApi, "createCategory").mockResolvedValue(EXISTING);

    renderDirect();
    await screen.findByText("No categories found.");
    await userEvent.click(screen.getByRole("button", { name: "New category" }));
    await userEvent.type(screen.getByLabelText("Name"), "Billing");
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    await vi.waitFor(() => expect(createCategory).toHaveBeenCalledWith({ name: "Billing" }));
    expect(await screen.findByText("Billing")).toBeInTheDocument();
  });

  it("shows a duplicate-name error on a 409 response", async () => {
    vi.spyOn(knowledgeBaseApi, "listCategories").mockResolvedValue([]);
    vi.spyOn(knowledgeBaseApi, "createCategory").mockRejectedValue({ isAxiosError: true, response: { status: 409 } });

    renderDirect();
    await screen.findByText("No categories found.");
    await userEvent.click(screen.getByRole("button", { name: "New category" }));
    await userEvent.type(screen.getByLabelText("Name"), "Billing");
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    expect(await screen.findByText("A category with this name already exists.")).toBeInTheDocument();
  });

  it("shows a referenced-by-articles message on a 409 delete response", async () => {
    vi.spyOn(knowledgeBaseApi, "listCategories").mockResolvedValue([EXISTING]);
    vi.spyOn(knowledgeBaseApi, "deleteCategory").mockRejectedValue({ isAxiosError: true, response: { status: 409 } });
    vi.spyOn(window, "confirm").mockReturnValue(true);

    renderDirect();
    await screen.findByText("Billing");
    await userEvent.click(screen.getByRole("button", { name: "Delete" }));

    expect(await screen.findByText(/still used by one or more knowledge base items/)).toBeInTheDocument();
  });
});
