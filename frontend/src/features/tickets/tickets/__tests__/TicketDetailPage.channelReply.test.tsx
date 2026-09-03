import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AuthContext } from "../../../auth/AuthContext";
import type { AuthState } from "../../../auth/AuthContext";
import * as collaborationApi from "../collaboration/collaborationApi";
import * as ticketHistoryApi from "../history/ticketHistoryApi";
import { TicketDetailPage } from "../TicketDetailPage";
import * as ticketsApi from "../ticketsApi";
import type { TicketDetail } from "../types";

const BASE_TICKET: TicketDetail = {
  id: "ticket-1",
  customerId: "customer-1",
  customerName: "Jane Doe",
  subject: "Cannot log in",
  categoryId: "cat-1",
  categoryName: "Account / Access",
  priorityId: "pri-1",
  priorityName: "High",
  status: "open",
  createdByUserId: "creator-1",
  createdByUserName: "Local Admin",
  assignedUserId: null,
  assignedUserName: null,
  isEscalated: false,
  createdAt: "2026-08-01T00:00:00Z",
  updatedAt: "2026-08-01T00:00:00Z",
  description: "Customer cannot access their account.",
  escalatedAt: null,
  escalatedByUserId: null,
  escalatedByUserName: null,
  escalationReason: null,
  sourceChannel: null,
  sla: null,
  categoryDepartmentId: null,
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
      <MemoryRouter initialEntries={["/tickets/ticket-1"]}>
        <Routes>
          <Route path="/tickets/:id" element={<TicketDetailPage />} />
        </Routes>
      </MemoryRouter>
    </AuthContext>,
  );
}

describe("TicketDetailPage — WhatsApp/SMS reply composer", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.spyOn(ticketHistoryApi, "getTicketHistory").mockResolvedValue([]);
    vi.spyOn(collaborationApi, "listCollaborationComments").mockResolvedValue([]);
  });

  it.each(["WhatsApp", "Sms"])("shows the Send button for a %s-sourced ticket when the caller has tickets.channel.reply", async (channel) => {
    vi.spyOn(ticketsApi, "getTicket").mockResolvedValue({ ...BASE_TICKET, sourceChannel: channel });

    renderPage(["tickets.channel.reply"]);

    expect(await screen.findByText(new RegExp(`came in via ${channel} — pressing Send sends your reply to the customer`))).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Send" })).toBeDisabled();
  });

  it("hides the Send button on a WhatsApp ticket when the caller lacks tickets.channel.reply", async () => {
    vi.spyOn(ticketsApi, "getTicket").mockResolvedValue({ ...BASE_TICKET, sourceChannel: "WhatsApp" });

    renderPage([]);

    await screen.findByText("A scratch area for composing your reply. Nothing here is sent.");
    expect(screen.queryByRole("button", { name: "Send" })).not.toBeInTheDocument();
  });

  it("does not show the channel Send button for an email-sourced ticket even with tickets.channel.reply", async () => {
    vi.spyOn(ticketsApi, "getTicket").mockResolvedValue({ ...BASE_TICKET, sourceChannel: "Email" });

    renderPage(["tickets.channel.reply"]);

    await screen.findByText("A scratch area for composing your reply. Nothing here is sent.");
    expect(screen.queryByRole("button", { name: "Send" })).not.toBeInTheDocument();
  });

  it("sends the reply via sendChannelReply, refreshes the ticket, clears the draft, and shows a success message", async () => {
    vi.spyOn(ticketsApi, "getTicket").mockResolvedValue({ ...BASE_TICKET, sourceChannel: "WhatsApp" });
    const sendChannelReply = vi.spyOn(ticketsApi, "sendChannelReply").mockResolvedValue({ ...BASE_TICKET, sourceChannel: "WhatsApp" });
    const sendEmailReply = vi.spyOn(ticketsApi, "sendEmailReply");

    renderPage(["tickets.channel.reply"]);
    const textarea = await screen.findByPlaceholderText("Type a reply, or insert a quick reply above…");
    await userEvent.type(textarea, "Thanks, please try again.");
    await userEvent.click(screen.getByRole("button", { name: "Send" }));

    await vi.waitFor(() => expect(sendChannelReply).toHaveBeenCalledWith("ticket-1", "Thanks, please try again."));
    expect(sendEmailReply).not.toHaveBeenCalled();
    expect(await screen.findByText("Reply sent.")).toBeInTheDocument();
    expect(textarea).toHaveValue("");
  });

  it("on failure keeps the draft contents and shows the server's error message", async () => {
    vi.spyOn(ticketsApi, "getTicket").mockResolvedValue({ ...BASE_TICKET, sourceChannel: "Sms" });
    vi.spyOn(ticketsApi, "sendChannelReply").mockRejectedValue({
      isAxiosError: true,
      response: { status: 400, data: { error: "no_recipient" } },
    });

    renderPage(["tickets.channel.reply"]);
    const textarea = await screen.findByPlaceholderText("Type a reply, or insert a quick reply above…");
    await userEvent.type(textarea, "Thanks, please try again.");
    await userEvent.click(screen.getByRole("button", { name: "Send" }));

    expect(await screen.findByText("There is no phone number to reply to for this ticket.")).toBeInTheDocument();
    expect(textarea).toHaveValue("Thanks, please try again.");
  });

  it("shows a generic error and keeps the draft on a 502 provider failure", async () => {
    vi.spyOn(ticketsApi, "getTicket").mockResolvedValue({ ...BASE_TICKET, sourceChannel: "WhatsApp" });
    vi.spyOn(ticketsApi, "sendChannelReply").mockRejectedValue({
      isAxiosError: true,
      response: { status: 502, data: { error: "provider_failed" } },
    });

    renderPage(["tickets.channel.reply"]);
    const textarea = await screen.findByPlaceholderText("Type a reply, or insert a quick reply above…");
    await userEvent.type(textarea, "Draft text");
    await userEvent.click(screen.getByRole("button", { name: "Send" }));

    expect(await screen.findByText("The message could not be sent. Please try again.")).toBeInTheDocument();
    expect(textarea).toHaveValue("Draft text");
  });
});
