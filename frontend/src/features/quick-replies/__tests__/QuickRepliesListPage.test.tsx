import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AuthContext } from "../../auth/AuthContext";
import type { AuthState } from "../../auth/AuthContext";
import * as quickRepliesApi from "../quickRepliesApi";
import { QuickRepliesListPage } from "../QuickRepliesListPage";
import type { QuickReply } from "../types";

function quickReply(overrides: Partial<QuickReply> = {}): QuickReply {
  return {
    id: "qr-1",
    title: "Greeting",
    body: "Hello, thanks for reaching out!",
    isActive: true,
    createdAt: "2026-08-31T00:00:00Z",
    updatedAt: "2026-08-31T00:00:00Z",
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
    user: { id: "current", email: "current@local.test", displayName: "Current User" },
    login: async () => undefined,
    logout: () => undefined,
  };
}

function renderPage(permissions: string[]) {
  return render(
    <AuthContext value={stubAuth(permissions)}>
      <MemoryRouter>
        <QuickRepliesListPage />
      </MemoryRouter>
    </AuthContext>,
  );
}

describe("QuickRepliesListPage", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("renders the empty state when there are no quick replies", async () => {
    vi.spyOn(quickRepliesApi, "listQuickReplies").mockResolvedValue([]);

    renderPage(["quickreplies.view"]);

    expect(await screen.findByText("No quick replies found.")).toBeInTheDocument();
  });

  it("lists quick replies returned from the API", async () => {
    vi.spyOn(quickRepliesApi, "listQuickReplies").mockResolvedValue([quickReply()]);

    renderPage(["quickreplies.view"]);

    expect(await screen.findByText("Greeting")).toBeInTheDocument();
  });

  it("hides the New/Edit/Delete actions and the status column for a view-only caller", async () => {
    vi.spyOn(quickRepliesApi, "listQuickReplies").mockResolvedValue([quickReply()]);

    renderPage(["quickreplies.view"]);
    await screen.findByText("Greeting");

    expect(screen.queryByRole("link", { name: /new quick reply/i })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Edit" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Delete" })).not.toBeInTheDocument();
    expect(screen.queryByText("Active")).not.toBeInTheDocument();
  });

  it("shows the New/Edit/Delete actions for a caller with quickreplies.manage", async () => {
    vi.spyOn(quickRepliesApi, "listQuickReplies").mockResolvedValue([quickReply()]);

    renderPage(["quickreplies.view", "quickreplies.manage"]);
    await screen.findByText("Greeting");

    expect(screen.getByRole("link", { name: /new quick reply/i })).toHaveAttribute("href", "/quick-replies/new");
    expect(screen.getByRole("button", { name: "Edit" })).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Delete" })).toBeInTheDocument();
  });

  it("deletes a quick reply after confirmation", async () => {
    vi.spyOn(quickRepliesApi, "listQuickReplies").mockResolvedValue([quickReply()]);
    const deleteQuickReply = vi.spyOn(quickRepliesApi, "deleteQuickReply").mockResolvedValue(undefined);
    vi.spyOn(window, "confirm").mockReturnValue(true);

    renderPage(["quickreplies.view", "quickreplies.manage"]);
    await screen.findByText("Greeting");

    await userEvent.click(screen.getByRole("button", { name: "Delete" }));

    await vi.waitFor(() => expect(deleteQuickReply).toHaveBeenCalledWith("qr-1"));
    expect(screen.queryByText("Greeting")).not.toBeInTheDocument();
  });

  it("does not delete when the confirmation is declined", async () => {
    vi.spyOn(quickRepliesApi, "listQuickReplies").mockResolvedValue([quickReply()]);
    const deleteQuickReply = vi.spyOn(quickRepliesApi, "deleteQuickReply");
    vi.spyOn(window, "confirm").mockReturnValue(false);

    renderPage(["quickreplies.view", "quickreplies.manage"]);
    await screen.findByText("Greeting");

    await userEvent.click(screen.getByRole("button", { name: "Delete" }));

    expect(deleteQuickReply).not.toHaveBeenCalled();
    expect(screen.getByText("Greeting")).toBeInTheDocument();
  });
});
