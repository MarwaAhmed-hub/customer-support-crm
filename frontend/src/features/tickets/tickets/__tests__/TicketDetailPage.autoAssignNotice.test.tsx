import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AuthContext } from "../../../auth/AuthContext";
import type { AuthState } from "../../../auth/AuthContext";
import * as usersApi from "../../../users/usersApi";
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
  categoryName: "Billing",
  priorityId: "pri-1",
  priorityName: "High",
  status: "open",
  createdByUserId: "creator-1",
  createdByUserName: "Local Admin",
  assignedUserId: "agent-1",
  assignedUserName: "Finance Agent",
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
  categoryDepartmentId: "dept-finance",
};

function stubAuth(): AuthState {
  return {
    status: "authenticated",
    isAdmin: false,
    permissions: ["tickets.assign"],
    hasPermission: (code) => code === "tickets.assign",
    hasAnyPermission: (codes) => codes.includes("tickets.assign"),
    user: { id: "current", email: "current@local.test", displayName: "Current User" },
    login: async () => undefined,
    logout: () => undefined,
  };
}

function renderPage(initialState?: unknown) {
  return render(
    <AuthContext value={stubAuth()}>
      <MemoryRouter initialEntries={[{ pathname: "/tickets/ticket-1", state: initialState }]}>
        <Routes>
          <Route path="/tickets/:id" element={<TicketDetailPage />} />
        </Routes>
      </MemoryRouter>
    </AuthContext>,
  );
}

describe("TicketDetailPage — Story 23 auto-assignment notice", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.spyOn(ticketsApi, "getTicket").mockResolvedValue(BASE_TICKET);
    vi.spyOn(ticketHistoryApi, "getTicketHistory").mockResolvedValue([]);
    vi.spyOn(collaborationApi, "listCollaborationComments").mockResolvedValue([]);
    vi.spyOn(usersApi, "listUsers").mockResolvedValue({ items: [], page: 1, pageSize: 100, total: 0 });
  });

  it("shows the notice handed off via navigation state", async () => {
    renderPage({ autoAssignNotice: "Ticket auto-assigned to Finance Agent." });

    expect(await screen.findByText("Ticket auto-assigned to Finance Agent.")).toBeInTheDocument();
  });

  it("shows nothing when no notice was handed off", async () => {
    renderPage(undefined);

    await screen.findByText(BASE_TICKET.subject);
    expect(screen.queryByText(/auto-assigned/)).not.toBeInTheDocument();
  });

  it("can be dismissed", async () => {
    renderPage({ autoAssignNotice: "Ticket auto-assigned to Finance Agent." });

    const alert = await screen.findByText("Ticket auto-assigned to Finance Agent.");
    const closeButton = screen.getByRole("button", { name: /close/i });
    await userEvent.click(closeButton);

    expect(alert).not.toBeInTheDocument();
  });
});
