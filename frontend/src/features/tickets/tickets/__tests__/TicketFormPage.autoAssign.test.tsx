import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import * as categoriesApi from "../../categories/categoriesApi";
import type { TicketCategory } from "../../categories/types";
import * as customersApi from "../../../customers/customersApi";
import type { Customer } from "../../../customers/types";
import * as prioritiesApi from "../../priorities/prioritiesApi";
import type { TicketPriority } from "../../priorities/types";
import { TicketFormPage } from "../TicketFormPage";
import * as ticketsApi from "../ticketsApi";
import type { TicketDetail } from "../types";

const CUSTOMER: Customer = {
  id: "customer-1",
  firstName: "Jane",
  lastName: "Doe",
  companyName: null,
  email: null,
  phone: null,
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: "2026-01-01T00:00:00Z",
};

const GENERAL_INQUIRY: TicketCategory = {
  id: "cat-general",
  name: "General Inquiry",
  description: null,
  isActive: true,
  departmentId: null,
  departmentName: null,
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: "2026-01-01T00:00:00Z",
};

const BILLING: TicketCategory = {
  id: "cat-billing",
  name: "Billing",
  description: null,
  isActive: true,
  departmentId: "dept-finance",
  departmentName: "Finance",
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: "2026-01-01T00:00:00Z",
};

const PRIORITY: TicketPriority = {
  id: "pri-1",
  name: "Low",
  sortOrder: 10,
  description: null,
  isActive: true,
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: "2026-01-01T00:00:00Z",
};

const EXISTING_TICKET: TicketDetail = {
  id: "ticket-1",
  customerId: "customer-1",
  customerName: "Jane Doe",
  subject: "Cannot log in",
  categoryId: "cat-general",
  categoryName: "General Inquiry",
  priorityId: "pri-1",
  priorityName: "Low",
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

function LandingProbe() {
  const location = useLocation();
  const state = location.state as { autoAssignNotice?: string } | null;
  return <div data-testid="landed">landed on {location.pathname} — notice: {state?.autoAssignNotice ?? "none"}</div>;
}

function renderEdit() {
  return render(
    <MemoryRouter initialEntries={["/tickets/ticket-1/edit"]}>
      <Routes>
        <Route path="/tickets/:id/edit" element={<TicketFormPage />} />
        <Route path="/tickets/:id" element={<LandingProbe />} />
      </Routes>
    </MemoryRouter>,
  );
}

describe("TicketFormPage — Story 23 auto-assignment notice hand-off", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.spyOn(customersApi, "listCustomers").mockResolvedValue([CUSTOMER]);
    vi.spyOn(categoriesApi, "listTicketCategories").mockResolvedValue([GENERAL_INQUIRY, BILLING]);
    vi.spyOn(prioritiesApi, "listTicketPriorities").mockResolvedValue([PRIORITY]);
  });

  it("hands off a notice when saving turns an unassigned ticket into an assigned one", async () => {
    vi.spyOn(ticketsApi, "getTicket").mockResolvedValue(EXISTING_TICKET);
    vi.spyOn(ticketsApi, "updateTicket").mockResolvedValue({
      ...EXISTING_TICKET,
      categoryId: "cat-billing",
      categoryName: "Billing",
      assignedUserId: "agent-1",
      assignedUserName: "Finance Agent",
    });

    renderEdit();
    await screen.findByDisplayValue("Cannot log in");
    await userEvent.click(screen.getByLabelText("Category"));
    await userEvent.click(await screen.findByRole("option", { name: "Billing" }));
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    const landed = await screen.findByTestId("landed");
    expect(landed).toHaveTextContent("notice: Ticket auto-assigned to Finance Agent.");
  });

  it("hands off no notice when the ticket stays unassigned after saving", async () => {
    vi.spyOn(ticketsApi, "getTicket").mockResolvedValue(EXISTING_TICKET);
    vi.spyOn(ticketsApi, "updateTicket").mockResolvedValue({
      ...EXISTING_TICKET,
      categoryId: "cat-billing",
      categoryName: "Billing",
    });

    renderEdit();
    await screen.findByDisplayValue("Cannot log in");
    await userEvent.click(screen.getByLabelText("Category"));
    await userEvent.click(await screen.findByRole("option", { name: "Billing" }));
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    const landed = await screen.findByTestId("landed");
    expect(landed).toHaveTextContent("notice: none");
  });

  it("hands off no notice when the ticket was already assigned before this edit", async () => {
    vi.spyOn(ticketsApi, "getTicket").mockResolvedValue({ ...EXISTING_TICKET, assignedUserId: "agent-0", assignedUserName: "Existing Agent" });
    vi.spyOn(ticketsApi, "updateTicket").mockResolvedValue({
      ...EXISTING_TICKET,
      categoryId: "cat-billing",
      categoryName: "Billing",
      assignedUserId: "agent-0",
      assignedUserName: "Existing Agent",
    });

    renderEdit();
    await screen.findByDisplayValue("Cannot log in");
    await userEvent.click(screen.getByLabelText("Category"));
    await userEvent.click(await screen.findByRole("option", { name: "Billing" }));
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    const landed = await screen.findByTestId("landed");
    expect(landed).toHaveTextContent("notice: none");
  });
});
