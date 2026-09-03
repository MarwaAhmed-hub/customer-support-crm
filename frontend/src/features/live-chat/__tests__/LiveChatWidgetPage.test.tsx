import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import * as publicLiveChatApi from "../publicLiveChatApi";
import { LiveChatWidgetPage } from "../LiveChatWidgetPage";

describe("LiveChatWidgetPage", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    window.sessionStorage.clear();
  });

  it("disables Start chat until a message is entered", () => {
    render(<LiveChatWidgetPage />);

    expect(screen.getByRole("button", { name: "Start chat" })).toBeDisabled();
  });

  it("starts a session and shows the conversation view with the first message", async () => {
    vi.spyOn(publicLiveChatApi, "startSession").mockResolvedValue({
      sessionId: "session-1",
      sessionToken: "token-1",
      ticketId: "ticket-1",
      customerId: "customer-1",
      status: "Waiting",
    });
    vi.spyOn(publicLiveChatApi, "getSession").mockResolvedValue({
      sessionId: "session-1",
      ticketId: "ticket-1",
      status: "Waiting",
      messages: [
        { id: "m1", sender: "Customer", senderUserId: null, senderName: null, body: "Hi, I need help", occurredAt: new Date().toISOString() },
      ],
    });

    render(<LiveChatWidgetPage />);
    await userEvent.type(screen.getByLabelText("How can we help?"), "Hi, I need help");
    await userEvent.click(screen.getByRole("button", { name: "Start chat" }));

    expect(await screen.findByText("Waiting for an agent…")).toBeInTheDocument();
    expect(await screen.findByText("Hi, I need help")).toBeInTheDocument();
  });

  it("shows a rate-limit message on 429 and stays on the start form", async () => {
    vi.spyOn(publicLiveChatApi, "startSession").mockRejectedValue({
      isAxiosError: true,
      response: { status: 429, data: {} },
    });

    render(<LiveChatWidgetPage />);
    await userEvent.type(screen.getByLabelText("How can we help?"), "Hi");
    await userEvent.click(screen.getByRole("button", { name: "Start chat" }));

    expect(await screen.findByText("Too many requests — please wait a minute and try again.")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Start chat" })).toBeInTheDocument();
  });

  it("sends a follow-up customer message and appends it to the thread", async () => {
    vi.spyOn(publicLiveChatApi, "startSession").mockResolvedValue({
      sessionId: "session-1",
      sessionToken: "token-1",
      ticketId: "ticket-1",
      customerId: "customer-1",
      status: "Waiting",
    });
    vi.spyOn(publicLiveChatApi, "getSession").mockResolvedValue({
      sessionId: "session-1",
      ticketId: "ticket-1",
      status: "Waiting",
      messages: [],
    });
    const sendMessage = vi.spyOn(publicLiveChatApi, "sendMessage").mockResolvedValue({
      id: "m2",
      sender: "Customer",
      senderUserId: null,
      senderName: null,
      body: "Are you there?",
      occurredAt: new Date().toISOString(),
    });

    render(<LiveChatWidgetPage />);
    await userEvent.type(screen.getByLabelText("How can we help?"), "Hi");
    await userEvent.click(screen.getByRole("button", { name: "Start chat" }));

    const draft = await screen.findByPlaceholderText("Type a message…");
    await userEvent.type(draft, "Are you there?");
    await userEvent.click(screen.getByRole("button", { name: "Send" }));

    await vi.waitFor(() => expect(sendMessage).toHaveBeenCalledWith("session-1", "token-1", "Are you there?"));
    expect(await screen.findByText("Are you there?")).toBeInTheDocument();
  });

  it("offers to start a new conversation once the stored one is closed, and returns to the start form", async () => {
    window.sessionStorage.setItem("livechat.session", JSON.stringify({ sessionId: "session-1", sessionToken: "token-1" }));
    vi.spyOn(publicLiveChatApi, "getSession").mockResolvedValue({
      sessionId: "session-1",
      ticketId: "ticket-1",
      status: "Closed",
      messages: [
        { id: "m1", sender: "Customer", senderUserId: null, senderName: null, body: "internet issue", occurredAt: new Date().toISOString() },
      ],
    });

    render(<LiveChatWidgetPage />);

    expect(await screen.findByText("This conversation has been closed.")).toBeInTheDocument();
    expect(screen.queryByPlaceholderText("Type a message…")).not.toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: "Start a new conversation" }));

    expect(screen.getByRole("button", { name: "Start chat" })).toBeInTheDocument();
    expect(window.sessionStorage.getItem("livechat.session")).toBeNull();
  });
});
