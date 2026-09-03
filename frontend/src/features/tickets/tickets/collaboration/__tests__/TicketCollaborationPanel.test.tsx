import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AuthContext } from "../../../../auth/AuthContext";
import type { AuthState } from "../../../../auth/AuthContext";
import * as collaborationApi from "../collaborationApi";
import { TicketCollaborationPanel } from "../TicketCollaborationPanel";
import type { TicketCollaborationComment } from "../types";

function comment(overrides: Partial<TicketCollaborationComment> = {}): TicketCollaborationComment {
  return {
    id: "comment-1",
    ticketId: "ticket-1",
    body: "Let's check with billing before responding.",
    authorUserId: "user-1",
    authorDisplayName: "Jane Agent",
    createdAt: "2026-08-31T00:00:00Z",
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

function renderPanel(permissions: string[]) {
  return render(
    <AuthContext value={stubAuth(permissions)}>
      <TicketCollaborationPanel ticketId="ticket-1" />
    </AuthContext>,
  );
}

describe("TicketCollaborationPanel", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("renders nothing when the caller lacks tickets.collaboration.view", () => {
    const listComments = vi.spyOn(collaborationApi, "listCollaborationComments");

    const { container } = renderPanel([]);

    expect(container).toBeEmptyDOMElement();
    expect(listComments).not.toHaveBeenCalled();
  });

  it("shows the empty state when there are no comments", async () => {
    vi.spyOn(collaborationApi, "listCollaborationComments").mockResolvedValue([]);

    renderPanel(["tickets.collaboration.view"]);

    expect(await screen.findByText("No internal comments yet.")).toBeInTheDocument();
  });

  it("renders the internal-only badge and each comment's author and timestamp", async () => {
    vi.spyOn(collaborationApi, "listCollaborationComments").mockResolvedValue([comment()]);

    renderPanel(["tickets.collaboration.view"]);

    expect(await screen.findByText("Let's check with billing before responding.")).toBeInTheDocument();
    expect(screen.getByText("Internal — not visible to the customer")).toBeInTheDocument();
    expect(screen.getByText("Jane Agent")).toBeInTheDocument();
  });

  it("hides the compose form when the caller lacks tickets.collaboration.create", async () => {
    vi.spyOn(collaborationApi, "listCollaborationComments").mockResolvedValue([]);

    renderPanel(["tickets.collaboration.view"]);
    await screen.findByText("No internal comments yet.");

    expect(screen.queryByPlaceholderText("Add an internal comment…")).not.toBeInTheDocument();
  });

  it("shows the compose form when the caller has tickets.collaboration.create, disabled while empty", async () => {
    vi.spyOn(collaborationApi, "listCollaborationComments").mockResolvedValue([]);

    renderPanel(["tickets.collaboration.view", "tickets.collaboration.create"]);
    await screen.findByText("No internal comments yet.");

    expect(screen.getByRole("button", { name: "Add" })).toBeDisabled();
  });

  it("submits a new comment, appends it to the list, and clears the textarea", async () => {
    vi.spyOn(collaborationApi, "listCollaborationComments").mockResolvedValue([]);
    const createComment = vi.spyOn(collaborationApi, "createCollaborationComment").mockResolvedValue(
      comment({ id: "comment-new", body: "New internal note" }),
    );

    renderPanel(["tickets.collaboration.view", "tickets.collaboration.create"]);
    await screen.findByText("No internal comments yet.");

    const textarea = screen.getByPlaceholderText("Add an internal comment…");
    await userEvent.type(textarea, "New internal note");
    await userEvent.click(screen.getByRole("button", { name: "Add" }));

    await vi.waitFor(() => expect(createComment).toHaveBeenCalledWith("ticket-1", "New internal note"));
    expect(await screen.findByText("New internal note")).toBeInTheDocument();
    expect(textarea).toHaveValue("");
  });
});
