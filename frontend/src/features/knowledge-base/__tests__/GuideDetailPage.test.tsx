import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AuthContext } from "../../auth/AuthContext";
import type { AuthState } from "../../auth/AuthContext";
import * as knowledgeBaseApi from "../knowledgeBaseApi";
import { GuideDetailPage } from "../GuideDetailPage";
import type { KbGuide } from "../types";

const GUIDE: KbGuide = {
  id: "guide-1",
  title: "Set up two-factor auth",
  description: "Enable 2FA on your account",
  categoryId: "cat-1",
  categoryName: "Account",
  audience: "CustomerFacing",
  status: "Published",
  // Deliberately out of order to prove the page sorts by `order`, not array position.
  steps: [
    { order: 1, instruction: "Enable 2FA" },
    { order: 0, instruction: "Open settings" },
  ],
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
      <MemoryRouter initialEntries={["/knowledge-base/guides/guide-1"]}>
        <Routes>
          <Route path="/knowledge-base/guides/:id" element={<GuideDetailPage />} />
        </Routes>
      </MemoryRouter>
    </AuthContext>,
  );
}

describe("GuideDetailPage", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("renders steps in their received order, not array order", async () => {
    vi.spyOn(knowledgeBaseApi, "getGuide").mockResolvedValue(GUIDE);

    renderPage(["knowledgebase.guides.view"]);

    await screen.findByText("Set up two-factor auth");
    const steps = screen.getAllByRole("listitem").map((el) => el.textContent);
    expect(steps).toEqual(["Open settings", "Enable 2FA"]);
  });

  it("hides Edit/Publish for a caller without manage/publish permissions", async () => {
    vi.spyOn(knowledgeBaseApi, "getGuide").mockResolvedValue(GUIDE);

    renderPage(["knowledgebase.guides.view"]);

    await screen.findByText("Set up two-factor auth");
    expect(screen.queryByRole("button", { name: "Edit" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: /Publish|Unpublish/ })).not.toBeInTheDocument();
  });
});
