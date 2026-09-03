import { createContext, useCallback, useEffect, useMemo, useState } from "react";
import type { ReactNode } from "react";
import { useAuth } from "../auth/useAuth";
import * as settingsApi from "./settingsApi";

/**
 * The subset of `SystemSettings` the UI actually renders live (topbar title/logo, MUI theme
 * colors) — deliberately narrower than the full settings DTO, which also carries
 * timezone/culture/support-email fields no component outside `SystemSettingsPage` needs.
 */
export interface Branding {
  applicationName: string;
  brandDisplayName: string;
  logoUrl: string | null;
  primaryColor: string;
  secondaryColor: string;
}

/** Matches the backend seeder's defaults (`DbSeeder.SeedSystemSettingsAsync`) so an unauthenticated/failed fetch renders identically to a freshly-seeded database. */
const DEFAULT_BRANDING: Branding = {
  applicationName: "Customer Support CRM",
  brandDisplayName: "Customer Support CRM",
  logoUrl: null,
  primaryColor: "#1976D2",
  secondaryColor: "#9C27B0",
};

export interface BrandingState {
  branding: Branding;
  /** Re-fetches from the server — called by `SystemSettingsPage` after a successful save so the header/theme update immediately, without a full reload. */
  refresh(): void;
}

export const BrandingContext = createContext<BrandingState | null>(null);

/**
 * Fetches `GET /api/system-settings` once the user is authenticated and hydrates the context.
 * Public/unauthenticated routes (the login page, or a viewer without `system.view`) simply keep
 * the defaults above — this provider never blocks rendering on the fetch.
 */
export function BrandingProvider({ children }: { children: ReactNode }) {
  const { status } = useAuth();
  const [branding, setBranding] = useState<Branding>(DEFAULT_BRANDING);

  const load = useCallback(() => {
    settingsApi
      .getSystemSettings()
      .then((settings) => {
        setBranding({
          applicationName: settings.applicationName,
          brandDisplayName: settings.brandDisplayName,
          logoUrl: settings.logoUrl,
          primaryColor: settings.primaryColor,
          secondaryColor: settings.secondaryColor,
        });
      })
      .catch(() => {
        // A non-admin viewer (403 — missing system.view), a fresh/misconfigured backend, or a
        // network blip all fall back to defaults rather than blocking the app from rendering.
        setBranding(DEFAULT_BRANDING);
      });
  }, []);

  useEffect(() => {
    if (status !== "authenticated") return;
    // Defer the fetch by one microtask so the token in AuthContext fully settles before
    // http.get() reads it from the interceptor (Story 06 logo-upload fix: simultaneous
    // status change and fetch were racing).
    queueMicrotask(() => load());
  }, [status, load]);

  const value = useMemo<BrandingState>(() => ({ branding, refresh: load }), [branding, load]);

  return <BrandingContext value={value}>{children}</BrandingContext>;
}
