import { render, screen } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { CustomerInteractionHistory } from "../CustomerInteractionHistory";
import * as interactionsApi from "../interactionsApi";
import type { CustomerInteraction } from "../types";

function interaction(overrides: Partial<CustomerInteraction> = {}): CustomerInteraction {
  return {
    id: "int-1",
    customerId: "customer-1",
    occurredAt: "2026-08-01T00:00:00Z",
    interactionType: "email_inbound",
    summary: "Subject line",
    details: null,
    userId: null,
    userDisplayName: null,
    ...overrides,
  };
}

describe("CustomerInteractionHistory", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("without a ticketId, requests the customer's full history and shows the customer-scoped empty state", async () => {
    const listCustomerInteractions = vi.spyOn(interactionsApi, "listCustomerInteractions").mockResolvedValue({
      items: [],
      total: 0,
      page: 1,
      pageSize: 25,
    });

    render(<CustomerInteractionHistory customerId="customer-1" />);

    expect(await screen.findByText("No interactions recorded for this customer yet.")).toBeInTheDocument();
    expect(listCustomerInteractions).toHaveBeenCalledWith("customer-1", { page: 1, pageSize: 25 });
  });

  it("with a ticketId, requests only that ticket's interactions and shows the ticket-scoped empty state", async () => {
    const listCustomerInteractions = vi.spyOn(interactionsApi, "listCustomerInteractions").mockResolvedValue({
      items: [],
      total: 0,
      page: 1,
      pageSize: 25,
    });

    render(<CustomerInteractionHistory customerId="customer-1" ticketId="ticket-1" />);

    expect(await screen.findByText("No interactions recorded for this ticket yet.")).toBeInTheDocument();
    expect(listCustomerInteractions).toHaveBeenCalledWith("customer-1", { page: 1, pageSize: 25, ticketId: "ticket-1" });
  });

  it("lists the ticket-scoped interactions returned by the API", async () => {
    vi.spyOn(interactionsApi, "listCustomerInteractions").mockResolvedValue({
      items: [interaction({ interactionType: "email_outbound", summary: "Re: refund request" })],
      total: 1,
      page: 1,
      pageSize: 25,
    });

    render(<CustomerInteractionHistory customerId="customer-1" ticketId="ticket-1" />);

    expect(await screen.findByText("Re: refund request")).toBeInTheDocument();
    expect(screen.getByText("email_outbound")).toBeInTheDocument();
  });
});
