import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { BrandingContext } from "../../settings/BrandingContext";
import type { BrandingState } from "../../settings/BrandingContext";
import { AdminRoute } from "../AdminRoute";
import { AuthContext } from "../AuthContext";
import type { AuthState } from "../AuthContext";
import type { AuthStatus, AuthUser } from "../types";

const ADMIN: AuthUser = { id: "u-1", email: "admin@local.test", displayName: "An Admin" };
const NON_ADMIN: AuthUser = { id: "u-2", email: "person@local.test", displayName: "A Person" };

// AdminRoute renders AppLayout when authenticated, and AppLayout reads useBranding() (Story 06) —
// a stub with no network call, same reasoning as stubAuth below.
const BRANDING_STUB: BrandingState = {
  branding: {
    applicationName: "Customer Support CRM",
    brandDisplayName: "Customer Support CRM",
    logoUrl: null,
    primaryColor: "#1976D2",
    secondaryColor: "#9C27B0",
  },
  refresh: () => undefined,
};

function stubAuth(status: AuthStatus, permissions: string[]): AuthState {
  return {
    status,
    isAdmin: permissions.length > 0,
    permissions,
    hasPermission: (code) => permissions.includes(code),
    hasAnyPermission: (codes) => codes.some((code) => permissions.includes(code)),
    user: status === "authenticated" ? (permissions.length > 0 ? ADMIN : NON_ADMIN) : null,
    login: async () => undefined,
    logout: () => undefined,
  };
}

function HomeProbe() {
  return <div data-testid="home">home</div>;
}

function LoginProbe() {
  const location = useLocation();
  const state: unknown = location.state;
  const from =
    typeof state === "object" && state !== null && "from" in state
      ? String(Reflect.get(state, "from"))
      : "(none)";
  return <div data-testid="login">login from {from}</div>;
}

function renderAt(status: AuthStatus, permissions: string[], path: string) {
  return render(
    <AuthContext value={stubAuth(status, permissions)}>
      <BrandingContext value={BRANDING_STUB}>
        <MemoryRouter initialEntries={[path]}>
          <Routes>
            <Route path="/" element={<HomeProbe />} />
            <Route path="/login" element={<LoginProbe />} />
            <Route
              path="/users"
              element={
                <AdminRoute>
                  <div data-testid="users">users page</div>
                </AdminRoute>
              }
            />
          </Routes>
        </MemoryRouter>
      </BrandingContext>
    </AuthContext>,
  );
}

// AdminRoute (Story 03) is a thin wrapper around <PermissionRoute required="users.view">; the full
// loading/anonymous/permitted/forbidden matrix is covered by PermissionRoute.test.tsx. These tests
// just confirm the delegation wires up the right permission.
describe("AdminRoute", () => {
  it("renders children for a user with users.view", () => {
    renderAt("authenticated", ["users.view"], "/users");

    expect(screen.getByTestId("users")).toBeInTheDocument();
  });

  it("redirects an authenticated user without users.view to Home", () => {
    renderAt("authenticated", [], "/users");

    expect(screen.getByTestId("home")).toBeInTheDocument();
    expect(screen.queryByTestId("users")).not.toBeInTheDocument();
  });

  it("redirects to /login when anonymous, preserving the intended path", () => {
    renderAt("anonymous", [], "/users?tab=open");

    expect(screen.getByTestId("login")).toHaveTextContent("login from /users?tab=open");
  });
});
