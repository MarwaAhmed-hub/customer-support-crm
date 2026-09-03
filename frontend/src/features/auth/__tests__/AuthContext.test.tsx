import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { StrictMode } from "react";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AuthProvider } from "../AuthContext";
import * as authApi from "../authApi";
import { clearToken, setToken } from "../../../lib/tokenStorage";
import { useAuth } from "../useAuth";
import type { AuthUser, MeResponse } from "../types";

const USER: AuthUser = { id: "u-1", email: "person@local.test", displayName: "A Person" };
const ME: MeResponse = { ...USER, permissions: ["users.view", "roles.view"] };

/** A structurally-valid (unsigned) JWT: base64url header/payload, matching what decodeJwtRole reads. */
function fakeJwt(payload: Record<string, unknown>): string {
  const base64url = (obj: unknown) =>
    btoa(JSON.stringify(obj)).replaceAll("+", "-").replaceAll("/", "_").replaceAll("=", "");
  return `${base64url({ alg: "none" })}.${base64url(payload)}.`;
}

const ADMIN_TOKEN = fakeJwt({ sub: "u-1", role: "Admin" });
const PLAIN_TOKEN = fakeJwt({ sub: "u-1" });

function Probe() {
  const { user, status, isAdmin, permissions, hasPermission, login, logout } = useAuth();
  return (
    <div>
      <span data-testid="status">{status}</span>
      <span data-testid="user">{user?.displayName ?? "none"}</span>
      <span data-testid="isAdmin">{String(isAdmin)}</span>
      <span data-testid="permissions">{permissions.join(",")}</span>
      <span data-testid="hasUsersView">{String(hasPermission("users.view"))}</span>
      <button type="button" onClick={() => void login("person@local.test", "Correct!23")}>
        do-login
      </button>
      <button type="button" onClick={logout}>
        do-logout
      </button>
    </div>
  );
}

function renderProbe(strict = false) {
  const tree = (
    <AuthProvider>
      <Probe />
    </AuthProvider>
  );
  return render(strict ? <StrictMode>{tree}</StrictMode> : tree);
}

describe("AuthContext", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    window.localStorage.clear();
    clearToken();
  });

  it("settles to anonymous and never calls me() when there is no stored token", async () => {
    const me = vi.spyOn(authApi, "me");

    renderProbe();

    await waitFor(() => expect(screen.getByTestId("status")).toHaveTextContent("anonymous"));
    expect(me).not.toHaveBeenCalled();
  });

  it("calls me() exactly once under StrictMode and settles to authenticated", async () => {
    setToken("stored-token");
    const me = vi.spyOn(authApi, "me").mockResolvedValue(ME);

    renderProbe(true);

    await waitFor(() => expect(screen.getByTestId("status")).toHaveTextContent("authenticated"));
    expect(screen.getByTestId("user")).toHaveTextContent("A Person");

    // React 19 StrictMode double-invokes the effect; the AbortController in the cleanup is what
    // keeps this at one *effective* call.
    expect(me).toHaveBeenCalledTimes(1);
  });

  it("settles to anonymous when the stored token is rejected", async () => {
    setToken("stale-token");
    vi.spyOn(authApi, "me").mockRejectedValue(new Error("401"));

    renderProbe();

    await waitFor(() => expect(screen.getByTestId("status")).toHaveTextContent("anonymous"));
    expect(screen.getByTestId("user")).toHaveTextContent("none");
  });

  it("login() performs exactly one POST and does not additionally call me()", async () => {
    const login = vi.spyOn(authApi, "login").mockResolvedValue({
      accessToken: "fresh-token",
      expiresAt: new Date().toISOString(),
      user: USER,
      permissions: [],
    });
    const me = vi.spyOn(authApi, "me");

    renderProbe();
    await waitFor(() => expect(screen.getByTestId("status")).toHaveTextContent("anonymous"));

    await userEvent.click(screen.getByRole("button", { name: "do-login" }));

    await waitFor(() => expect(screen.getByTestId("status")).toHaveTextContent("authenticated"));
    expect(login).toHaveBeenCalledTimes(1);
    expect(me).not.toHaveBeenCalled();
  });

  it("logout() clears the token and returns to anonymous", async () => {
    vi.spyOn(authApi, "login").mockResolvedValue({
      accessToken: "fresh-token",
      expiresAt: new Date().toISOString(),
      user: USER,
      permissions: [],
    });

    renderProbe();
    await waitFor(() => expect(screen.getByTestId("status")).toHaveTextContent("anonymous"));
    await userEvent.click(screen.getByRole("button", { name: "do-login" }));
    await waitFor(() => expect(screen.getByTestId("status")).toHaveTextContent("authenticated"));

    await userEvent.click(screen.getByRole("button", { name: "do-logout" }));

    await waitFor(() => expect(screen.getByTestId("status")).toHaveTextContent("anonymous"));
    expect(window.localStorage.getItem("crm.auth.token")).toBeNull();
  });

  it("exposes isAdmin decoded from the stored token on hydration", async () => {
    setToken(ADMIN_TOKEN);
    vi.spyOn(authApi, "me").mockResolvedValue(ME);

    renderProbe();

    await waitFor(() => expect(screen.getByTestId("status")).toHaveTextContent("authenticated"));
    expect(screen.getByTestId("isAdmin")).toHaveTextContent("true");
  });

  it("does not expose isAdmin for a token without a role claim", async () => {
    setToken(PLAIN_TOKEN);
    vi.spyOn(authApi, "me").mockResolvedValue(ME);

    renderProbe();

    await waitFor(() => expect(screen.getByTestId("status")).toHaveTextContent("authenticated"));
    expect(screen.getByTestId("isAdmin")).toHaveTextContent("false");
  });

  it("login() exposes isAdmin decoded from the returned access token", async () => {
    vi.spyOn(authApi, "login").mockResolvedValue({
      accessToken: ADMIN_TOKEN,
      expiresAt: new Date().toISOString(),
      user: USER,
      permissions: [],
    });

    renderProbe();
    await waitFor(() => expect(screen.getByTestId("status")).toHaveTextContent("anonymous"));

    await userEvent.click(screen.getByRole("button", { name: "do-login" }));

    await waitFor(() => expect(screen.getByTestId("status")).toHaveTextContent("authenticated"));
    expect(screen.getByTestId("isAdmin")).toHaveTextContent("true");
  });

  it("logout() resets isAdmin to false", async () => {
    vi.spyOn(authApi, "login").mockResolvedValue({
      accessToken: ADMIN_TOKEN,
      expiresAt: new Date().toISOString(),
      user: USER,
      permissions: [],
    });

    renderProbe();
    await waitFor(() => expect(screen.getByTestId("status")).toHaveTextContent("anonymous"));
    await userEvent.click(screen.getByRole("button", { name: "do-login" }));
    await waitFor(() => expect(screen.getByTestId("isAdmin")).toHaveTextContent("true"));

    await userEvent.click(screen.getByRole("button", { name: "do-logout" }));

    await waitFor(() => expect(screen.getByTestId("status")).toHaveTextContent("anonymous"));
    expect(screen.getByTestId("isAdmin")).toHaveTextContent("false");
  });

  it("drops to anonymous when the token is cleared from outside", async () => {
    setToken("stored-token");
    vi.spyOn(authApi, "me").mockResolvedValue(ME);

    renderProbe();
    await waitFor(() => expect(screen.getByTestId("status")).toHaveTextContent("authenticated"));

    // Simulates the interceptor's 401 handling or another tab logging out.
    clearToken();

    await waitFor(() => expect(screen.getByTestId("status")).toHaveTextContent("anonymous"));
    expect(screen.getByTestId("user")).toHaveTextContent("none");
  });

  it("exposes permissions from me() on hydration, and hasPermission reflects them", async () => {
    setToken("stored-token");
    vi.spyOn(authApi, "me").mockResolvedValue(ME);

    renderProbe();

    await waitFor(() => expect(screen.getByTestId("status")).toHaveTextContent("authenticated"));
    expect(screen.getByTestId("permissions")).toHaveTextContent("users.view,roles.view");
    expect(screen.getByTestId("hasUsersView")).toHaveTextContent("true");
  });

  it("exposes permissions from the login response", async () => {
    vi.spyOn(authApi, "login").mockResolvedValue({
      accessToken: "fresh-token",
      expiresAt: new Date().toISOString(),
      user: USER,
      permissions: ["tickets.view"],
    });

    renderProbe();
    await waitFor(() => expect(screen.getByTestId("status")).toHaveTextContent("anonymous"));

    await userEvent.click(screen.getByRole("button", { name: "do-login" }));

    await waitFor(() => expect(screen.getByTestId("status")).toHaveTextContent("authenticated"));
    expect(screen.getByTestId("permissions")).toHaveTextContent("tickets.view");
    expect(screen.getByTestId("hasUsersView")).toHaveTextContent("false");
  });

  it("clears permissions on logout", async () => {
    vi.spyOn(authApi, "login").mockResolvedValue({
      accessToken: "fresh-token",
      expiresAt: new Date().toISOString(),
      user: USER,
      permissions: ["users.view"],
    });

    renderProbe();
    await waitFor(() => expect(screen.getByTestId("status")).toHaveTextContent("anonymous"));
    await userEvent.click(screen.getByRole("button", { name: "do-login" }));
    await waitFor(() => expect(screen.getByTestId("hasUsersView")).toHaveTextContent("true"));

    await userEvent.click(screen.getByRole("button", { name: "do-logout" }));

    await waitFor(() => expect(screen.getByTestId("status")).toHaveTextContent("anonymous"));
    expect(screen.getByTestId("permissions")).toHaveTextContent("");
  });
});
