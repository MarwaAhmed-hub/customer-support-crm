import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AuthContext } from "../../auth/AuthContext";
import type { AuthState } from "../../auth/AuthContext";
import * as quickRepliesApi from "../../quick-replies/quickRepliesApi";
import * as liveChatApi from "../liveChatApi";
import { LiveChatConversationPage } from "../LiveChatConversationPage";
import type { LiveChatSessionDetail } from "../types";

const BASE_SESSION: LiveChatSessionDetail = {
  sessionId: "session-1",
  ticketId: "ticket-1",
  status: "Waiting",
  customerId: "customer-1",
  customerName: "Ali Hassan",
  subject: "Trouble logging in",
  assignedUserId: null,
  assignedUserName: null,
  messages: [
    { id: "m1", sender: "Customer", senderUserId: null, senderName: null, body: "Hi, I need help", occurredAt: "2026-08-31T10:00:00Z" },
  ],
};

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
      <MemoryRouter initialEntries={["/agent-desk/live-chat/session-1"]}>
        <Routes>
          <Route path="/agent-desk/live-chat/:id" element={<LiveChatConversationPage />} />
        </Routes>
      </MemoryRouter>
    </AuthContext>,
  );
}

describe("LiveChatConversationPage", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("shows the conversation with a reply box when the caller has livechat.send", async () => {
    vi.spyOn(liveChatApi, "getConversation").mockResolvedValue(BASE_SESSION);

    renderPage(["livechat.view", "livechat.send"]);

    expect(await screen.findByText("Hi, I need help")).toBeInTheDocument();
    expect(screen.getByPlaceholderText("Type a reply…")).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "View ticket" })).toHaveAttribute("href", "/tickets/ticket-1");
  });

  it("hides the reply box when the caller lacks livechat.send", async () => {
    vi.spyOn(liveChatApi, "getConversation").mockResolvedValue(BASE_SESSION);

    renderPage(["livechat.view"]);

    await screen.findByText("Hi, I need help");
    expect(screen.queryByPlaceholderText("Type a reply…")).not.toBeInTheDocument();
  });

  it("sends a reply and appends it to the thread", async () => {
    vi.spyOn(liveChatApi, "getConversation").mockResolvedValue(BASE_SESSION);
    vi.spyOn(quickRepliesApi, "listQuickReplies").mockResolvedValue([]);
    const sendReply = vi.spyOn(liveChatApi, "sendReply").mockResolvedValue({
      id: "m2",
      sender: "Agent",
      senderUserId: "agent-1",
      senderName: "Agent Smith",
      body: "How can I help?",
      occurredAt: "2026-08-31T10:01:00Z",
    });

    renderPage(["livechat.view", "livechat.send"]);
    const textarea = await screen.findByPlaceholderText("Type a reply…");
    await userEvent.type(textarea, "How can I help?");
    await userEvent.click(screen.getByRole("button", { name: "Send" }));

    await vi.waitFor(() => expect(sendReply).toHaveBeenCalledWith("session-1", "How can I help?"));
    expect(await screen.findByText("How can I help?")).toBeInTheDocument();
    expect(textarea).toHaveValue("");
  });

  it("shows a not-found message for an unknown session", async () => {
    vi.spyOn(liveChatApi, "getConversation").mockRejectedValue({
      isAxiosError: true,
      response: { status: 404, data: {} },
    });

    renderPage(["livechat.view"]);

    expect(await screen.findByText("Conversation not found.")).toBeInTheDocument();
  });

  it("disables the composer once the linked ticket is closed", async () => {
    vi.spyOn(liveChatApi, "getConversation").mockResolvedValue({ ...BASE_SESSION, status: "Closed" });
    vi.spyOn(quickRepliesApi, "listQuickReplies").mockResolvedValue([]);

    renderPage(["livechat.view", "livechat.send"]);

    const textarea = await screen.findByPlaceholderText("This conversation is closed.");
    expect(textarea).toBeDisabled();
    expect(screen.getByRole("button", { name: "Send" })).toBeDisabled();
  });
});
