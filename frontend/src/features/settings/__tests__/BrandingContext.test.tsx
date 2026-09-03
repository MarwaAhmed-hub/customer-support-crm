import { render, screen, waitFor } from "@testing-library/react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AuthContext } from "../../auth/AuthContext";
import type { AuthState } from "../../auth/AuthContext";
import type { AuthStatus } from "../../auth/types";
import { BrandingProvider } from "../BrandingContext";
import * as settingsApi from "../settingsApi";
import type { SystemSettings } from "../types";
import { useBranding } from "../useBranding";

function stubAuth(status: AuthStatus): AuthState {
  return {
    status,
    user: null,
    isAdmin: false,
    permissions: [],
    hasPermission: () => false,
    hasAnyPermission: () => false,
    login: async () => undefined,
    logout: () => undefined,
  };
}

function Probe() {
  const { branding } = useBranding();
  return (
    <div>
      <div data-testid="brand-name">{branding.brandDisplayName}</div>
      <div data-testid="primary-color">{branding.primaryColor}</div>
    </div>
  );
}

function renderWithAuth(status: AuthStatus) {
  return render(
    <AuthContext value={stubAuth(status)}>
      <BrandingProvider>
        <Probe />
      </BrandingProvider>
    </AuthContext>,
  );
}

describe("BrandingContext", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("uses defaults before the fetch resolves", () => {
    // Never resolves within this test — asserts the synchronous initial render only.
    vi.spyOn(settingsApi, "getSystemSettings").mockReturnValue(new Promise(() => {}));

    renderWithAuth("authenticated");

    expect(screen.getByTestId("brand-name")).toHaveTextContent("Customer Support CRM");
    expect(screen.getByTestId("primary-color")).toHaveTextContent("#1976D2");
  });

  it("applies the fetched primaryColor/brandDisplayName after resolve", async () => {
    const settings: SystemSettings = {
      applicationName: "Acme",
      supportEmail: "support@acme.test",
      defaultTimezone: "UTC",
      defaultCulture: "en-US",
      brandDisplayName: "Acme Support",
      logoUrl: null,
      primaryColor: "#123456",
      secondaryColor: "#654321",
      updatedAtUtc: "2026-01-01T00:00:00Z",
    };
    vi.spyOn(settingsApi, "getSystemSettings").mockResolvedValue(settings);

    renderWithAuth("authenticated");

    expect(await screen.findByTestId("brand-name")).toHaveTextContent("Acme Support");
    expect(screen.getByTestId("primary-color")).toHaveTextContent("#123456");
  });

  it("falls back to defaults on fetch rejection", async () => {
    const getSystemSettings = vi.spyOn(settingsApi, "getSystemSettings").mockRejectedValue(new Error("network error"));

    renderWithAuth("authenticated");

    await waitFor(() => expect(getSystemSettings).toHaveBeenCalled());
    expect(screen.getByTestId("brand-name")).toHaveTextContent("Customer Support CRM");
  });

  it("does not fetch while unauthenticated", () => {
    const getSystemSettings = vi.spyOn(settingsApi, "getSystemSettings");

    renderWithAuth("anonymous");

    expect(getSystemSettings).not.toHaveBeenCalled();
  });
});
