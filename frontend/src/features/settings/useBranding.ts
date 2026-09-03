import { useContext } from "react";
import { BrandingContext } from "./BrandingContext";
import type { BrandingState } from "./BrandingContext";

/**
 * Throws a clear error when used outside the provider, which satisfies strict null checks at every
 * call site without optional chaining. Mirrors `useAuth`.
 */
export function useBranding(): BrandingState {
  const context = useContext(BrandingContext);
  if (context === null) {
    throw new Error("useBranding must be used inside <BrandingProvider>.");
  }
  return context;
}
