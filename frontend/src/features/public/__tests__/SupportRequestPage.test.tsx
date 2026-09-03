import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import * as publicWebFormApi from "../publicWebFormApi";
import { SupportRequestPage } from "../SupportRequestPage";

describe("SupportRequestPage", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("shows validation errors when required fields are blank", async () => {
    render(<SupportRequestPage />);

    await userEvent.click(screen.getByRole("button", { name: "Send request" }));

    expect(await screen.findByText("Name is required.")).toBeInTheDocument();
    expect(screen.getByText("Email is required.")).toBeInTheDocument();
    expect(screen.getByText("Subject is required.")).toBeInTheDocument();
    expect(screen.getByText("Please describe your issue.")).toBeInTheDocument();
  });

  it("shows a validation error for a malformed email", async () => {
    render(<SupportRequestPage />);

    await userEvent.type(screen.getByLabelText("Email"), "not-an-email");
    await userEvent.click(screen.getByRole("button", { name: "Send request" }));

    expect(await screen.findByText("Enter a valid email address.")).toBeInTheDocument();
  });

  it("submits the trimmed payload (without an empty phone) and shows the thank-you screen", async () => {
    const submit = vi.spyOn(publicWebFormApi, "submitWebFormTicket").mockResolvedValue({
      ticketId: "ticket-1",
      customerId: "customer-1",
    });

    render(<SupportRequestPage />);
    await userEvent.type(screen.getByLabelText("Your name"), "  Ali Hassan  ");
    await userEvent.type(screen.getByLabelText("Email"), "ali@example.com");
    await userEvent.type(screen.getByLabelText("Subject"), "Feature request");
    await userEvent.type(screen.getByLabelText("How can we help?"), "Add dark mode");
    await userEvent.click(screen.getByRole("button", { name: "Send request" }));

    await vi.waitFor(() =>
      expect(submit).toHaveBeenCalledWith({
        name: "Ali Hassan",
        email: "ali@example.com",
        subject: "Feature request",
        description: "Add dark mode",
        website: "",
      }),
    );
    expect(await screen.findByText("Thanks — we've got it.")).toBeInTheDocument();
  });

  it("includes phone when provided", async () => {
    const submit = vi.spyOn(publicWebFormApi, "submitWebFormTicket").mockResolvedValue({
      ticketId: "ticket-1",
      customerId: "customer-1",
    });

    render(<SupportRequestPage />);
    await userEvent.type(screen.getByLabelText("Your name"), "Ali");
    await userEvent.type(screen.getByLabelText("Email"), "ali@example.com");
    await userEvent.type(screen.getByLabelText("Phone (optional)"), "555-1234");
    await userEvent.type(screen.getByLabelText("Subject"), "Subject");
    await userEvent.type(screen.getByLabelText("How can we help?"), "Body");
    await userEvent.click(screen.getByRole("button", { name: "Send request" }));

    await vi.waitFor(() => expect(submit).toHaveBeenCalledWith(expect.objectContaining({ phone: "555-1234" })));
  });

  it("shows a rate-limit message on 429 and keeps the form (not the thank-you screen)", async () => {
    vi.spyOn(publicWebFormApi, "submitWebFormTicket").mockRejectedValue({
      isAxiosError: true,
      response: { status: 429, data: {} },
    });

    render(<SupportRequestPage />);
    await userEvent.type(screen.getByLabelText("Your name"), "Ali");
    await userEvent.type(screen.getByLabelText("Email"), "ali@example.com");
    await userEvent.type(screen.getByLabelText("Subject"), "Subject");
    await userEvent.type(screen.getByLabelText("How can we help?"), "Body");
    await userEvent.click(screen.getByRole("button", { name: "Send request" }));

    expect(await screen.findByText("Too many requests — please wait a minute and try again.")).toBeInTheDocument();
    expect(screen.queryByText("Thanks — we've got it.")).not.toBeInTheDocument();
  });

  it("surfaces a server-side invalid_email as a field error", async () => {
    vi.spyOn(publicWebFormApi, "submitWebFormTicket").mockRejectedValue({
      isAxiosError: true,
      response: { status: 400, data: { error: "invalid_email" } },
    });

    render(<SupportRequestPage />);
    await userEvent.type(screen.getByLabelText("Your name"), "Ali");
    await userEvent.type(screen.getByLabelText("Email"), "ali@example.com");
    await userEvent.type(screen.getByLabelText("Subject"), "Subject");
    await userEvent.type(screen.getByLabelText("How can we help?"), "Body");
    await userEvent.click(screen.getByRole("button", { name: "Send request" }));

    expect(await screen.findByText("Enter a valid email address.")).toBeInTheDocument();
  });

  it("renders the honeypot field but keeps it out of tab order", () => {
    render(<SupportRequestPage />);

    const honeypot = screen.getByLabelText("Website");
    expect(honeypot).toHaveAttribute("tabindex", "-1");
  });
});
