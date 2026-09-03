import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AuthContext } from "../../../auth/AuthContext";
import type { AuthState } from "../../../auth/AuthContext";
import type { PagedResult, TicketListItem } from "../../../tickets/tickets/types";
import * as agentDashboardApi from "../agentDashboardApi";
import { AgentDashboardPage } from "../AgentDashboardPage";

function ticket(overrides: Partial<TicketListItem> = {}): TicketListItem {
  return {
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
    assignedUserId: "current",
    assignedUserName: "Current User",
    isEscalated: false,
    createdAt: "2026-08-31T00:00:00Z",
    updatedAt: "2026-08-31T00:00:00Z",
    sourceChannel: null,
    sla: null,
    ...overrides,
  };
}

function page(items: TicketListItem[]): PagedResult<TicketListItem> {
  return { items, page: 1, pageSize: 100, total: items.length };
}

function stubAuth(overrides: Partial<AuthState> = {}): AuthState {
  return {
    status: "authenticated",
    isAdmin: false,
    permissions: ["tickets.view"],
    hasPermission: () => true,
    hasAnyPermission: () => true,
    user: { id: "current", email: "current@local.test", displayName: "Current User" },
    login: async () => undefined,
    logout: () => undefined,
    ...overrides,
  };
}

function renderPage(authOverrides: Partial<AuthState> = {}) {
  return render(
    <AuthContext value={stubAuth(authOverrides)}>
      <MemoryRouter>
        <AgentDashboardPage />
      </MemoryRouter>
    </AuthContext>,
  );
}

describe("AgentDashboardPage", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("renders a loading state before the fetch resolves", () => {
    vi.spyOn(agentDashboardApi, "fetchMyAssignedTickets").mockReturnValue(new Promise(() => undefined));

    renderPage();

    expect(screen.getByText("Loading…")).toBeInTheDocument();
  });

  it("renders fetched tickets with all required columns", async () => {
    vi.spyOn(agentDashboardApi, "fetchMyAssignedTickets").mockResolvedValue(page([ticket()]));

    renderPage();

    expect(await screen.findByText("Cannot log in")).toBeInTheDocument();
    expect(screen.getByText("Open")).toBeInTheDocument();
    expect(screen.getByText("High")).toBeInTheDocument();
    expect(screen.getByText("Account / Access")).toBeInTheDocument();
    expect(screen.getByText("Jane Doe")).toBeInTheDocument();
  });

  it("shows the empty-state message when there are no assigned tickets", async () => {
    vi.spyOn(agentDashboardApi, "fetchMyAssignedTickets").mockResolvedValue(page([]));

    renderPage();

    expect(await screen.findByText("You have no assigned tickets.")).toBeInTheDocument();
  });

  it("links the subject to the ticket detail page and the customer to the customer detail page", async () => {
    vi.spyOn(agentDashboardApi, "fetchMyAssignedTickets").mockResolvedValue(page([ticket()]));

    renderPage();

    expect(await screen.findByRole("link", { name: "Cannot log in" })).toHaveAttribute("href", "/tickets/ticket-1");
    expect(screen.getByRole("link", { name: "Jane Doe" })).toHaveAttribute("href", "/customers/customer-1");
  });

  it("shows an error state when the fetch fails", async () => {
    vi.spyOn(agentDashboardApi, "fetchMyAssignedTickets").mockRejectedValue(new Error("network"));

    renderPage();

    expect(await screen.findByRole("alert")).toHaveTextContent("Could not load your assigned tickets. Please try again.");
  });

  it("does not call the API when there is no authenticated user", () => {
    const spy = vi.spyOn(agentDashboardApi, "fetchMyAssignedTickets");

    renderPage({ user: null });

    expect(spy).not.toHaveBeenCalled();
  });
});
