import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import * as liveChatApi from "../liveChatApi";
import { LiveChatInboxPage } from "../LiveChatInboxPage";
import type { LiveChatSessionListItem } from "../types";

function renderPage() {
  return render(
    <MemoryRouter>
      <LiveChatInboxPage />
    </MemoryRouter>,
  );
}

const WAITING_ITEM: LiveChatSessionListItem = {
  sessionId: "session-1",
  ticketId: "ticket-1",
  status: "Waiting",
  customerId: "customer-1",
  customerName: "Ali Hassan",
  subject: "Hi, I need help",
  assignedUserId: null,
  assignedUserName: null,
  createdAt: "2026-08-31T10:00:00Z",
  lastMessageAt: "2026-08-31T10:00:00Z",
};

describe("LiveChatInboxPage", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("defaults to the Waiting tab and lists its conversations", async () => {
    const list = vi.spyOn(liveChatApi, "listConversations").mockResolvedValue([WAITING_ITEM]);

    renderPage();

    expect(screen.getByRole("tab", { name: "Waiting" })).toHaveAttribute("aria-selected", "true");
    expect(await screen.findByText("Ali Hassan")).toBeInTheDocument();
    await vi.waitFor(() => expect(list).toHaveBeenCalledWith("Waiting"));
  });

  it("refetches with the selected status when a tab is clicked", async () => {
    const list = vi.spyOn(liveChatApi, "listConversations").mockResolvedValue([]);

    renderPage();
    await vi.waitFor(() => expect(list).toHaveBeenCalledWith("Waiting"));

    await userEvent.click(screen.getByRole("tab", { name: "All" }));

    await vi.waitFor(() => expect(list).toHaveBeenCalledWith(undefined));
  });

  it("shows an empty state when there are no conversations", async () => {
    vi.spyOn(liveChatApi, "listConversations").mockResolvedValue([]);

    renderPage();

    expect(await screen.findByText("No conversations here.")).toBeInTheDocument();
  });

  it("shows an error message when the list fails to load", async () => {
    vi.spyOn(liveChatApi, "listConversations").mockRejectedValue(new Error("boom"));

    renderPage();

    expect(await screen.findByText("Could not load conversations. Please try again.")).toBeInTheDocument();
  });
});
