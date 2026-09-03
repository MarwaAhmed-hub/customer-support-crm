import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { BrandingContext } from "../BrandingContext";
import type { BrandingState } from "../BrandingContext";
import * as settingsApi from "../settingsApi";
import { SystemSettingsPage } from "../SystemSettingsPage";
import type { SystemSettings } from "../types";

const SEEDED: SystemSettings = {
  applicationName: "Customer Support CRM",
  supportEmail: "support@localhost",
  defaultTimezone: "UTC",
  defaultCulture: "en-US",
  brandDisplayName: "Customer Support CRM",
  logoUrl: null,
  primaryColor: "#1976D2",
  secondaryColor: "#9C27B0",
  updatedAtUtc: "2026-01-01T00:00:00Z",
};

function stubBranding(): BrandingState {
  return {
    branding: {
      applicationName: SEEDED.applicationName,
      brandDisplayName: SEEDED.brandDisplayName,
      logoUrl: SEEDED.logoUrl,
      primaryColor: SEEDED.primaryColor,
      secondaryColor: SEEDED.secondaryColor,
    },
    refresh: vi.fn(),
  };
}

function renderPage(brandingState: BrandingState = stubBranding()) {
  return render(
    <BrandingContext value={brandingState}>
      <SystemSettingsPage />
    </BrandingContext>,
  );
}

describe("SystemSettingsPage", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("renders the loaded settings", async () => {
    vi.spyOn(settingsApi, "getSystemSettings").mockResolvedValue(SEEDED);

    renderPage();

    expect(await screen.findByLabelText("Application name")).toHaveValue("Customer Support CRM");
    expect(screen.getByLabelText("Brand display name")).toHaveValue("Customer Support CRM");
    expect(screen.getByLabelText("Support email")).toHaveValue("support@localhost");
    expect(screen.getByLabelText("Default timezone")).toHaveValue("UTC");
    expect(screen.getByLabelText("Default culture")).toHaveValue("en-US");
    expect(screen.getByLabelText("Primary color")).toHaveValue("#1976D2");
    expect(screen.getByLabelText("Secondary color")).toHaveValue("#9C27B0");
  });

  it("shows a validation error when the application name is cleared", async () => {
    vi.spyOn(settingsApi, "getSystemSettings").mockResolvedValue(SEEDED);

    renderPage();
    await screen.findByDisplayValue("support@localhost");

    await userEvent.clear(screen.getByLabelText("Application name"));
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    expect(await screen.findByText("Application name is required.")).toBeInTheDocument();
  });

  it("shows a validation error for an invalid support email", async () => {
    vi.spyOn(settingsApi, "getSystemSettings").mockResolvedValue(SEEDED);

    renderPage();
    await screen.findByDisplayValue("support@localhost");

    const emailField = screen.getByLabelText("Support email");
    await userEvent.clear(emailField);
    await userEvent.type(emailField, "not-an-email");
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    expect(await screen.findByText("Enter a valid email address.")).toBeInTheDocument();
  });

  it("shows a validation error for a bad hex color", async () => {
    vi.spyOn(settingsApi, "getSystemSettings").mockResolvedValue(SEEDED);

    renderPage();
    await screen.findByDisplayValue("support@localhost");

    const primaryColorField = screen.getByLabelText("Primary color");
    await userEvent.clear(primaryColorField);
    await userEvent.type(primaryColorField, "blue");
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    expect(await screen.findByText("Enter a hex color like #1976D2.")).toBeInTheDocument();
  });

  it("shows a validation error for a non-http logo URL", async () => {
    vi.spyOn(settingsApi, "getSystemSettings").mockResolvedValue(SEEDED);

    renderPage();
    await screen.findByDisplayValue("support@localhost");

    await userEvent.type(screen.getByLabelText("Logo URL"), "ftp://example.com/logo.png");
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    expect(await screen.findByText("Logo URL must be an absolute http:// or https:// URL.")).toBeInTheDocument();
  });

  it("submits the update, refreshes branding, and shows a success toast", async () => {
    vi.spyOn(settingsApi, "getSystemSettings").mockResolvedValue(SEEDED);
    const updateSystemSettings = vi.spyOn(settingsApi, "updateSystemSettings").mockResolvedValue({
      ...SEEDED,
      brandDisplayName: "Acme Support",
    });
    const branding = stubBranding();

    renderPage(branding);
    await screen.findByDisplayValue("support@localhost");

    const brandField = screen.getByLabelText("Brand display name");
    await userEvent.clear(brandField);
    await userEvent.type(brandField, "Acme Support");
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    expect(await screen.findByText("Settings saved.")).toBeInTheDocument();
    expect(updateSystemSettings).toHaveBeenCalledWith(
      expect.objectContaining({ brandDisplayName: "Acme Support" }),
    );
    expect(branding.refresh).toHaveBeenCalled();
  });

  it("uploads a logo file and fills the Logo URL field with the returned URL", async () => {
    vi.spyOn(settingsApi, "getSystemSettings").mockResolvedValue(SEEDED);
    const uploadLogo = vi
      .spyOn(settingsApi, "uploadLogo")
      .mockResolvedValue({ logoUrl: "http://localhost:5080/uploads/logos/abc123.png" });

    renderPage();
    await screen.findByDisplayValue("support@localhost");

    const file = new File(["fake-image-bytes"], "logo.png", { type: "image/png" });
    const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement;
    await userEvent.upload(fileInput, file);

    expect(uploadLogo).toHaveBeenCalledWith(file);
    expect(await screen.findByDisplayValue("http://localhost:5080/uploads/logos/abc123.png")).toBeInTheDocument();
  });

  it("rejects an unsupported file type client-side without calling the upload API", async () => {
    vi.spyOn(settingsApi, "getSystemSettings").mockResolvedValue(SEEDED);
    const uploadLogo = vi.spyOn(settingsApi, "uploadLogo");

    renderPage();
    await screen.findByDisplayValue("support@localhost");

    const file = new File(["not-an-image"], "notes.txt", { type: "text/plain" });
    const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement;
    // applyAccept: false — a real browser lets a user override the `accept` filter (e.g. "All
    // Files"), so the component's own type check, not just the input's `accept` attribute, is what
    // this test exercises.
    await userEvent.upload(fileInput, file, { applyAccept: false });

    expect(await screen.findByText("Choose a PNG, JPEG, GIF, WEBP, or SVG image.")).toBeInTheDocument();
    expect(uploadLogo).not.toHaveBeenCalled();
  });
});
