import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AuthContext } from "../../../auth/AuthContext";
import type { AuthState } from "../../../auth/AuthContext";
import * as categoriesApi from "../../categories/categoriesApi";
import * as prioritiesApi from "../../priorities/prioritiesApi";
import { TicketsListPage } from "../TicketsListPage";
import * as ticketsApi from "../ticketsApi";
import type { PagedResult, TicketListItem } from "../types";

function stubAuth(): AuthState {
  return {
    status: "authenticated",
    isAdmin: false,
    permissions: ["tickets.view"],
    hasPermission: (code) => code === "tickets.view",
    hasAnyPermission: (codes) => codes.includes("tickets.view"),
    user: { id: "current", email: "current@local.test", displayName: "Current User" },
    login: async () => undefined,
    logout: () => undefined,
  };
}

function emptyPage(): PagedResult<TicketListItem> {
  return { items: [], page: 1, pageSize: 20, total: 0 };
}

function renderPage() {
  return render(
    <AuthContext value={stubAuth()}>
      <MemoryRouter initialEntries={["/tickets"]}>
        <Routes>
          <Route path="/tickets" element={<TicketsListPage />} />
        </Routes>
      </MemoryRouter>
    </AuthContext>,
  );
}

describe("TicketsListPage — Story 23 Unassigned Tickets Queue filter", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.spyOn(categoriesApi, "listTicketCategories").mockResolvedValue([]);
    vi.spyOn(prioritiesApi, "listTicketPriorities").mockResolvedValue([]);
  });

  it("does not send unassignedOnly by default", async () => {
    const listTickets = vi.spyOn(ticketsApi, "listTickets").mockResolvedValue(emptyPage());

    renderPage();

    await screen.findByText("No tickets found.");
    expect(listTickets).toHaveBeenCalledWith(expect.not.objectContaining({ unassignedOnly: expect.anything() }));
  });

  it("sends unassignedOnly: true once 'Unassigned only' is selected", async () => {
    const listTickets = vi.spyOn(ticketsApi, "listTickets").mockResolvedValue(emptyPage());

    renderPage();
    await screen.findByText("No tickets found.");
    listTickets.mockClear();

    await userEvent.click(screen.getByLabelText("Assignment"));
    await userEvent.click(await screen.findByRole("option", { name: "Unassigned only" }));

    await screen.findByText("No tickets found.");
    expect(listTickets).toHaveBeenCalledWith(expect.objectContaining({ unassignedOnly: true }));
  });

  it("clears unassignedOnly when switching back to 'All tickets'", async () => {
    const listTickets = vi.spyOn(ticketsApi, "listTickets").mockResolvedValue(emptyPage());

    renderPage();
    await screen.findByText("No tickets found.");
    await userEvent.click(screen.getByLabelText("Assignment"));
    await userEvent.click(await screen.findByRole("option", { name: "Unassigned only" }));
    await screen.findByText("No tickets found.");
    listTickets.mockClear();

    await userEvent.click(screen.getByLabelText("Assignment"));
    await userEvent.click(await screen.findByRole("option", { name: "All tickets" }));

    await screen.findByText("No tickets found.");
    expect(listTickets).toHaveBeenCalledWith(expect.not.objectContaining({ unassignedOnly: expect.anything() }));
  });
});
