import { render, screen } from "@testing-library/react";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { describe, expect, it } from "vitest";
import { BrandingContext } from "../../settings/BrandingContext";
import type { BrandingState } from "../../settings/BrandingContext";
import { AuthContext } from "../AuthContext";
import type { AuthState } from "../AuthContext";
import { ProtectedRoute } from "../ProtectedRoute";
import type { AuthStatus, AuthUser } from "../types";

const USER: AuthUser = { id: "u-1", email: "person@local.test", displayName: "A Person" };

function stubAuth(status: AuthStatus): AuthState {
  return {
    status,
    user: status === "authenticated" ? USER : null,
    isAdmin: false,
    permissions: [],
    hasPermission: () => false,
    hasAnyPermission: () => false,
    login: async () => undefined,
    logout: () => undefined,
  };
}

// ProtectedRoute renders AppLayout when authenticated, and AppLayout reads useBranding() (Story
// 06) — a stub with no network call, same reasoning as stubAuth above.
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

function LoginProbe() {
  const location = useLocation();
  const state: unknown = location.state;
  const from =
    typeof state === "object" && state !== null && "from" in state
      ? String(Reflect.get(state, "from"))
      : "(none)";
  return <div data-testid="login">login from {from}</div>;
}

function renderAt(status: AuthStatus, path: string) {
  return render(
    <AuthContext value={stubAuth(status)}>
      <BrandingContext value={BRANDING_STUB}>
        <MemoryRouter initialEntries={[path]}>
          <Routes>
            <Route path="/login" element={<LoginProbe />} />
            <Route
              path="/secret"
              element={
                <ProtectedRoute>
                  <div data-testid="secret">secret content</div>
                </ProtectedRoute>
              }
            />
          </Routes>
        </MemoryRouter>
      </BrandingContext>
    </AuthContext>,
  );
}

describe("ProtectedRoute", () => {
  it("renders children when authenticated", () => {
    renderAt("authenticated", "/secret");

    expect(screen.getByTestId("secret")).toBeInTheDocument();
  });

  it("redirects to /login when anonymous, preserving the intended path", () => {
    renderAt("anonymous", "/secret?tab=open");

    expect(screen.getByTestId("login")).toHaveTextContent("login from /secret?tab=open");
  });

  it("renders neither the children nor a redirect while loading", () => {
    renderAt("loading", "/secret");

    // The refresh-bounce regression test.
    expect(screen.queryByTestId("secret")).not.toBeInTheDocument();
    expect(screen.queryByTestId("login")).not.toBeInTheDocument();
  });
});
