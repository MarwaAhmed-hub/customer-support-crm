import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AuthContext } from "../../auth/AuthContext";
import type { AuthState } from "../../auth/AuthContext";
import * as knowledgeBaseApi from "../knowledgeBaseApi";
import { GuideFormPage } from "../GuideFormPage";
import type { KbGuide, KnowledgeBaseCategory } from "../types";

const CATEGORY: KnowledgeBaseCategory = { id: "cat-1", name: "Account", isActive: true };

const EXISTING: KbGuide = {
  id: "guide-1",
  title: "Set up two-factor auth",
  description: "Enable 2FA on your account",
  categoryId: "cat-1",
  categoryName: "Account",
  audience: "CustomerFacing",
  status: "Draft",
  steps: [
    { order: 0, instruction: "Open settings" },
    { order: 1, instruction: "Enable 2FA" },
  ],
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

function renderCreate(permissions: string[] = ["knowledgebase.guides.manage"]) {
  return render(
    <AuthContext value={stubAuth(permissions)}>
      <MemoryRouter initialEntries={["/knowledge-base/guides/new"]}>
        <Routes>
          <Route path="/knowledge-base/guides/new" element={<GuideFormPage />} />
          <Route path="/knowledge-base/guides" element={<LandingProbe />} />
        </Routes>
      </MemoryRouter>
    </AuthContext>,
  );
}

describe("GuideFormPage — create mode", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.spyOn(knowledgeBaseApi, "listCategories").mockResolvedValue([CATEGORY]);
  });

  it("adds a step row and submits steps in the order shown", async () => {
    const createGuide = vi.spyOn(knowledgeBaseApi, "createGuide").mockResolvedValue(EXISTING);

    renderCreate();
    await userEvent.type(screen.getByLabelText("Title"), "Set up two-factor auth");
    await userEvent.type(screen.getByLabelText("Description"), "Enable 2FA on your account");
    await userEvent.click(screen.getByLabelText("Category"));
    await userEvent.click(await screen.findByRole("option", { name: "Account" }));

    await userEvent.type(screen.getByLabelText("Step 1"), "Open settings");
    await userEvent.click(screen.getByRole("button", { name: "Add step" }));
    await userEvent.type(screen.getByLabelText("Step 2"), "Enable 2FA");

    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    await vi.waitFor(() =>
      expect(createGuide).toHaveBeenCalledWith(
        expect.objectContaining({
          steps: [{ instruction: "Open settings" }, { instruction: "Enable 2FA" }],
        }),
      ),
    );
    expect(await screen.findByTestId("landed")).toHaveTextContent("landed on /knowledge-base/guides");
  }, 10000);

  it("reorders steps with the move-down control before submitting", async () => {
    const createGuide = vi.spyOn(knowledgeBaseApi, "createGuide").mockResolvedValue(EXISTING);

    renderCreate();
    await userEvent.type(screen.getByLabelText("Title"), "Title");
    await userEvent.type(screen.getByLabelText("Description"), "Description");
    await userEvent.click(screen.getByLabelText("Category"));
    await userEvent.click(await screen.findByRole("option", { name: "Account" }));

    await userEvent.type(screen.getByLabelText("Step 1"), "First");
    await userEvent.click(screen.getByRole("button", { name: "Add step" }));
    await userEvent.type(screen.getByLabelText("Step 2"), "Second");

    await userEvent.click(screen.getByLabelText("Move step 1 down"));
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    await vi.waitFor(() =>
      expect(createGuide).toHaveBeenCalledWith(
        expect.objectContaining({
          steps: [{ instruction: "Second" }, { instruction: "First" }],
        }),
      ),
    );
  }, 10000);

  it("removes a step row and drops it from the submitted payload", async () => {
    const createGuide = vi.spyOn(knowledgeBaseApi, "createGuide").mockResolvedValue(EXISTING);

    renderCreate();
    await userEvent.type(screen.getByLabelText("Title"), "Title");
    await userEvent.type(screen.getByLabelText("Description"), "Description");
    await userEvent.click(screen.getByLabelText("Category"));
    await userEvent.click(await screen.findByRole("option", { name: "Account" }));

    await userEvent.type(screen.getByLabelText("Step 1"), "First");
    await userEvent.click(screen.getByRole("button", { name: "Add step" }));
    await userEvent.type(screen.getByLabelText("Step 2"), "Second");
    await userEvent.click(screen.getByLabelText("Remove step 2"));

    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    await vi.waitFor(() =>
      expect(createGuide).toHaveBeenCalledWith(expect.objectContaining({ steps: [{ instruction: "First" }] })),
    );
  }, 10000);

  it("rejects a guide with no non-empty steps", async () => {
    renderCreate();
    await userEvent.type(screen.getByLabelText("Title"), "Title");
    await userEvent.type(screen.getByLabelText("Description"), "Description");
    await userEvent.click(screen.getByLabelText("Category"));
    await userEvent.click(await screen.findByRole("option", { name: "Account" }));

    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    expect(await screen.findByText("At least one step is required.")).toBeInTheDocument();
  });
});
