import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { BrandingContext } from "../../settings/BrandingContext";
import type { BrandingState } from "../../settings/BrandingContext";
import { AuthContext } from "../AuthContext";
import type { AuthState } from "../AuthContext";
import { PermissionRoute } from "../PermissionRoute";
import type { AuthStatus, AuthUser } from "../types";

const USER: AuthUser = { id: "u-1", email: "person@local.test", displayName: "A Person" };

// PermissionRoute renders AppLayout when permitted, and AppLayout reads useBranding() (Story 06) —
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
    isAdmin: false,
    permissions,
    hasPermission: (code) => permissions.includes(code),
    hasAnyPermission: (codes) => codes.some((code) => permissions.includes(code)),
    user: status === "authenticated" ? USER : null,
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

function renderAt(status: AuthStatus, permissions: string[], required: string | string[], path = "/roles") {
  return render(
    <AuthContext value={stubAuth(status, permissions)}>
      <BrandingContext value={BRANDING_STUB}>
        <MemoryRouter initialEntries={[path]}>
          <Routes>
            <Route path="/" element={<HomeProbe />} />
            <Route path="/login" element={<LoginProbe />} />
            <Route
              path="/roles"
              element={
                <PermissionRoute required={required}>
                  <div data-testid="roles">roles page</div>
                </PermissionRoute>
              }
            />
          </Routes>
        </MemoryRouter>
      </BrandingContext>
    </AuthContext>,
  );
}

describe("PermissionRoute", () => {
  it("renders children when the user has the required permission", () => {
    renderAt("authenticated", ["roles.view"], "roles.view");

    expect(screen.getByTestId("roles")).toBeInTheDocument();
  });

  it("redirects an authenticated user missing the required permission to Home", () => {
    renderAt("authenticated", ["users.view"], "roles.view");

    expect(screen.getByTestId("home")).toBeInTheDocument();
    expect(screen.queryByTestId("roles")).not.toBeInTheDocument();
  });

  it("renders children when the user has at least one of an array of required permissions", () => {
    renderAt("authenticated", ["roles.create"], ["roles.view", "roles.create"]);

    expect(screen.getByTestId("roles")).toBeInTheDocument();
  });

  it("redirects when the user has none of an array of required permissions", () => {
    renderAt("authenticated", ["users.view"], ["roles.view", "roles.create"]);

    expect(screen.getByTestId("home")).toBeInTheDocument();
  });

  it("redirects to /login when anonymous, preserving the intended path", () => {
    renderAt("anonymous", [], "roles.view", "/roles?tab=open");

    expect(screen.getByTestId("login")).toHaveTextContent("login from /roles?tab=open");
  });

  it("renders neither the children nor a redirect while loading", () => {
    renderAt("loading", [], "roles.view");

    expect(screen.queryByTestId("roles")).not.toBeInTheDocument();
    expect(screen.queryByTestId("home")).not.toBeInTheDocument();
    expect(screen.queryByTestId("login")).not.toBeInTheDocument();
  });
});
