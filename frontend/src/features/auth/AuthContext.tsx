import { createContext, useCallback, useEffect, useMemo, useRef, useState } from "react";
import type { ReactNode } from "react";
import { decodeJwtRole } from "../../lib/jwt";
import { clearToken, getToken, setToken, subscribe } from "../../lib/tokenStorage";
import * as authApi from "./authApi";
import type { AuthStatus, AuthUser } from "./types";

export interface AuthState {
  user: AuthUser | null;
  status: AuthStatus;
  /**
   * Derived by decoding the current access token's `role` claim (Story 02) — not part of the
   * `AuthUser`/`LoginResponse` wire contract, which stays additive-only per Story 01's JWT
   * forward-compatibility rules. Never used for real authorization; the backend re-checks the
   * signed token on every request. See `lib/jwt.ts`.
   *
   * Kept for backward compatibility (Story 03): new UI gating should prefer `hasPermission`/
   * `hasAnyPermission` below, which reflect the real roles/permissions model instead of a single
   * hard-coded role name.
   */
  isAdmin: boolean;
  /** The caller's effective permission codes (union across their roles). Refreshed on hydrate, login, and every `/api/auth/me` call. */
  permissions: string[];
  hasPermission(code: string): boolean;
  hasAnyPermission(codes: string[]): boolean;
  login(email: string, password: string): Promise<void>;
  logout(): void;
}

export const AuthContext = createContext<AuthState | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<AuthUser | null>(null);
  const [status, setStatus] = useState<AuthStatus>("loading");
  const [isAdmin, setIsAdmin] = useState(false);
  const [permissions, setPermissions] = useState<string[]>([]);

  // Hydration. `status` starts at "loading" on purpose: without it, ProtectedRoute would see
  // user === null on the first render and bounce a legitimately logged-in user to /login on every
  // page refresh.
  // React 19 StrictMode mounts, unmounts and remounts effects in development, so a naive effect
  // calls me() twice.
  //
  // An AbortController alone does NOT fix that: it cancels the first request, but the call — and
  // the network round-trip — has already happened, so the browser still shows two. A ref survives
  // StrictMode's simulated remount (a genuine remount builds a new component instance with a fresh
  // ref), so guarding on one gives exactly one call, which is what the "one GET /api/auth/me per
  // page load" requirement actually asks for.
  //
  // The trade-off is that the response is applied even if the provider unmounts first. In React 19
  // a state update on an unmounted component is a no-op with no warning, and the provider lives for
  // the lifetime of the app, so there is nothing to leak here.
  const hydrationStarted = useRef(false);

  useEffect(() => {
    if (hydrationStarted.current) return;
    hydrationStarted.current = true;

    const token = getToken();
    if (token === null) {
      setStatus("anonymous");
      return;
    }

    // The role claim is already inside the stored token — no need to wait for me() to resolve it.
    setIsAdmin(decodeJwtRole(token) === "Admin");

    authApi
      .me()
      .then((current) => {
        setUser(current);
        setPermissions(current.permissions);
        setStatus("authenticated");
      })
      .catch(() => {
        // On a 401 the interceptor has already cleared the token.
        setUser(null);
        setStatus("anonymous");
        setIsAdmin(false);
        setPermissions([]);
      });
  }, []);

  // One code path for every way a session ends: an interceptor 401, another tab logging out, or an
  // explicit logout() — all of them clear the token, and this is what reacts.
  useEffect(
    () =>
      subscribe((token) => {
        if (token === null) {
          setUser(null);
          setStatus("anonymous");
          setIsAdmin(false);
          setPermissions([]);
        }
      }),
    [],
  );

  const login = useCallback(async (email: string, password: string): Promise<void> => {
    // The single place the login request happens. It must not call me() afterwards:
    // LoginResponse.user is exactly the projection me() returns, so a second call would be the
    // duplicate request this story forbids. Errors rethrow so LoginPage can render them.
    const response = await authApi.login({ email, password });
    setToken(response.accessToken);
    setUser(response.user);
    setStatus("authenticated");
    setIsAdmin(decodeJwtRole(response.accessToken) === "Admin");
    setPermissions(response.permissions);
  }, []);

  const logout = useCallback((): void => {
    // No state is set here — clearToken() notifies the subscription above, which does the reset.
    // There is no server call: no server-side session or revocation list exists in this story.
    clearToken();
  }, []);

  const hasPermission = useCallback((code: string): boolean => permissions.includes(code), [permissions]);

  const hasAnyPermission = useCallback(
    (codes: string[]): boolean => codes.some((code) => permissions.includes(code)),
    [permissions],
  );

  const value = useMemo<AuthState>(
    () => ({ user, status, isAdmin, permissions, hasPermission, hasAnyPermission, login, logout }),
    [user, status, isAdmin, permissions, hasPermission, hasAnyPermission, login, logout],
  );

  return <AuthContext value={value}>{children}</AuthContext>;
}
