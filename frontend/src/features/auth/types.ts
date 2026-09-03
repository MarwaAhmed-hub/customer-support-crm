/**
 * Mirrors of the backend DTOs (camelCase — the default System.Text.Json policy).
 *
 * These are deliberately **open** shapes. Per the additive contract, later stories may add more
 * fields; nothing here may be validated against a closed schema that would reject them, and no
 * speculative optional fields are declared now.
 */

export interface AuthUser {
  id: string;
  email: string;
  displayName: string;
}

export interface LoginRequest {
  email: string;
  password: string;
}

/** `permissions` (Story 03) is additive on top of Story 01's shape. */
export interface LoginResponse {
  accessToken: string;
  expiresAt: string;
  user: AuthUser;
  permissions: string[];
}

/** The body of `GET /api/auth/me`, refreshed on every page load so permission gates stay current. */
export interface MeResponse {
  id: string;
  email: string;
  displayName: string;
  permissions: string[];
}

export type AuthStatus = "loading" | "authenticated" | "anonymous";
