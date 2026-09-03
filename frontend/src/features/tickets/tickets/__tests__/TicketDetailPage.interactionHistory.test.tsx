import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AuthContext } from "../../../auth/AuthContext";
import type { AuthState } from "../../../auth/AuthContext";
import * as interactionsApi from "../../../customers/interactions/interactionsApi";
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

describe("TicketDetailPage — Interaction History panel", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.spyOn(ticketsApi, "getTicket").mockResolvedValue(BASE_TICKET);
    vi.spyOn(ticketHistoryApi, "getTicketHistory").mockResolvedValue([]);
    vi.spyOn(collaborationApi, "listCollaborationComments").mockResolvedValue([]);
    vi.spyOn(usersApi, "listUsers").mockResolvedValue({ items: [], page: 1, pageSize: 100, total: 0 });
  });

  it("shows Interaction History scoped to this ticket's customer and id when the caller can read it", async () => {
    const listCustomerInteractions = vi
      .spyOn(interactionsApi, "listCustomerInteractions")
      .mockResolvedValue({ items: [], total: 0, page: 1, pageSize: 25 });

    renderPage(["customers.interactions.read"]);

    expect(await screen.findByText("Interaction History")).toBeInTheDocument();
    expect(listCustomerInteractions).toHaveBeenCalledWith("customer-1", { page: 1, pageSize: 25, ticketId: "ticket-1" });
  });

  it("hides Interaction History entirely when the caller lacks the permission", async () => {
    renderPage([]);

    await screen.findByText(BASE_TICKET.subject);
    expect(screen.queryByText("Interaction History")).not.toBeInTheDocument();
  });
});
