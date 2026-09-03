import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import * as publicWebFormApi from "../../../public/publicWebFormApi";
import * as channelInboundApi from "../channelInboundApi";
import { ChannelSimulatorPage } from "../ChannelSimulatorPage";
import * as emailIngestionApi from "../emailIngestionApi";

function renderPage() {
  return render(
    <MemoryRouter>
      <ChannelSimulatorPage />
    </MemoryRouter>,
  );
}

describe("ChannelSimulatorPage", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("shows all five channel tabs, defaulting to Email", () => {
    renderPage();

    for (const label of ["Email", "Web Form", "WhatsApp", "SMS", "Live Chat"]) {
      expect(screen.getByRole("tab", { name: label })).toBeInTheDocument();
    }
    expect(screen.getByRole("tab", { name: "Email" })).toHaveAttribute("aria-selected", "true");
    expect(screen.getByLabelText("From")).toBeInTheDocument();
  });

  it("disables the email submit button until the required fields are filled", async () => {
    renderPage();

    const submit = screen.getByRole("button", { name: "Simulate inbound email" });
    expect(submit).toBeDisabled();

    await userEvent.type(screen.getByLabelText("From"), "jane@example.com");
    await userEvent.type(screen.getByLabelText("Subject"), "Cannot log in");
    await userEvent.type(screen.getByLabelText("Body"), "Help please");
    // Message-ID is pre-filled automatically, so all four required fields are now satisfied.
    expect(submit).toBeEnabled();
  });

  it("submits the email payload and shows a result with links to the ticket and customer", async () => {
    const ingest = vi.spyOn(emailIngestionApi, "ingestEmail").mockResolvedValue({
      ticketId: "ticket-1",
      customerId: "customer-1",
      alreadyProcessed: false,
    });

    renderPage();
    await userEvent.type(screen.getByLabelText("From"), "jane@example.com");
    await userEvent.type(screen.getByLabelText("Subject"), "Cannot log in");
    await userEvent.type(screen.getByLabelText("Body"), "Help please");
    await userEvent.click(screen.getByRole("button", { name: "Simulate inbound email" }));

    await vi.waitFor(() =>
      expect(ingest).toHaveBeenCalledWith(
        expect.objectContaining({ from: "jane@example.com", subject: "Cannot log in", bodyText: "Help please" }),
      ),
    );
    expect(await screen.findByRole("link", { name: "View ticket" })).toHaveAttribute("href", "/tickets/ticket-1");
    expect(screen.getByRole("link", { name: "View customer" })).toHaveAttribute("href", "/customers/customer-1");
  });

  it("shows a generic error on an unexpected failure from the email endpoint", async () => {
    vi.spyOn(emailIngestionApi, "ingestEmail").mockRejectedValue(new Error("boom"));

    renderPage();
    await userEvent.type(screen.getByLabelText("From"), "jane@example.com");
    await userEvent.type(screen.getByLabelText("Subject"), "Cannot log in");
    await userEvent.type(screen.getByLabelText("Body"), "Help please");
    await userEvent.click(screen.getByRole("button", { name: "Simulate inbound email" }));

    expect(await screen.findByText("Something went wrong. Please try again.")).toBeInTheDocument();
  });

  it("switches to the Web Form tab and submits through the public web form API", async () => {
    const submitTicket = vi.spyOn(publicWebFormApi, "submitWebFormTicket").mockResolvedValue({
      ticketId: "ticket-2",
      customerId: "customer-2",
    });

    renderPage();
    await userEvent.click(screen.getByRole("tab", { name: "Web Form" }));

    await userEvent.type(screen.getByLabelText("Name"), "Ali Hassan");
    await userEvent.type(screen.getByLabelText("Email"), "ali@example.com");
    await userEvent.type(screen.getByLabelText("Subject"), "Feature request");
    await userEvent.type(screen.getByLabelText("Description"), "Add dark mode");
    await userEvent.click(screen.getByRole("button", { name: "Simulate web form submission" }));

    await vi.waitFor(() =>
      expect(submitTicket).toHaveBeenCalledWith({
        name: "Ali Hassan",
        email: "ali@example.com",
        subject: "Feature request",
        description: "Add dark mode",
      }),
    );
    expect(await screen.findByRole("link", { name: "View ticket" })).toHaveAttribute("href", "/tickets/ticket-2");
  });

  it("links out to the live widget and agent inbox for Live Chat instead of a form", async () => {
    renderPage();

    await userEvent.click(screen.getByRole("tab", { name: "Live Chat" }));

    expect(await screen.findByText(/has no "ingest" to simulate/)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: "/live-chat" })).toHaveAttribute("href", "/live-chat");
    expect(screen.getByRole("link", { name: "agent inbox" })).toHaveAttribute("href", "/agent-desk/live-chat");
    expect(screen.queryByRole("button", { name: /simulate/i })).not.toBeInTheDocument();
  });

  it("switches to the WhatsApp tab and submits through the WhatsApp inbound API", async () => {
    const ingest = vi.spyOn(channelInboundApi, "ingestWhatsApp").mockResolvedValue({
      ticketId: "ticket-3",
      customerId: "customer-3",
      deduplicated: false,
    });

    renderPage();
    await userEvent.click(screen.getByRole("tab", { name: "WhatsApp" }));

    await userEvent.type(screen.getByLabelText("From (phone number)"), "+201001234567");
    await userEvent.type(screen.getByLabelText("Body"), "Hello via WhatsApp");
    await userEvent.click(screen.getByRole("button", { name: "Simulate inbound WhatsApp" }));

    await vi.waitFor(() =>
      expect(ingest).toHaveBeenCalledWith(expect.objectContaining({ fromPhoneNumber: "+201001234567", body: "Hello via WhatsApp" })),
    );
    expect(await screen.findByRole("link", { name: "View ticket" })).toHaveAttribute("href", "/tickets/ticket-3");
  });

  it("switches to the SMS tab and submits through the SMS inbound API", async () => {
    const ingest = vi.spyOn(channelInboundApi, "ingestSms").mockResolvedValue({
      ticketId: "ticket-4",
      customerId: "customer-4",
      deduplicated: true,
    });

    renderPage();
    await userEvent.click(screen.getByRole("tab", { name: "SMS" }));

    await userEvent.type(screen.getByLabelText("From (phone number)"), "+201001234567");
    await userEvent.type(screen.getByLabelText("Body"), "Hello via SMS");
    await userEvent.click(screen.getByRole("button", { name: "Simulate inbound SMS" }));

    await vi.waitFor(() => expect(ingest).toHaveBeenCalled());
    expect(await screen.findByText(/Already processed/)).toBeInTheDocument();
  });

  it("shows a generic error on an unexpected failure from the WhatsApp endpoint", async () => {
    vi.spyOn(channelInboundApi, "ingestWhatsApp").mockRejectedValue(new Error("boom"));

    renderPage();
    await userEvent.click(screen.getByRole("tab", { name: "WhatsApp" }));
    await userEvent.type(screen.getByLabelText("From (phone number)"), "+201001234567");
    await userEvent.type(screen.getByLabelText("Body"), "Hello");
    await userEvent.click(screen.getByRole("button", { name: "Simulate inbound WhatsApp" }));

    expect(await screen.findByText("Something went wrong. Please try again.")).toBeInTheDocument();
  });
});
