import { useContext } from "react";
import { AuthContext } from "./AuthContext";
import type { AuthState } from "./AuthContext";

/**
 * Throws a clear error when used outside the provider, which satisfies strict null checks at every
 * call site without optional chaining.
 */
export function useAuth(): AuthState {
  const context = useContext(AuthContext);
  if (context === null) {
    throw new Error("useAuth must be used inside <AuthProvider>.");
  }
  return context;
}
