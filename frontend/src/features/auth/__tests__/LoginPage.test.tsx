import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import loginPageSource from "../LoginPage.tsx?raw";
import { AxiosError, AxiosHeaders } from "axios";
import type { AxiosResponse, InternalAxiosRequestConfig } from "axios";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AuthContext } from "../AuthContext";
import type { AuthState } from "../AuthContext";
import { LoginPage } from "../LoginPage";
import type { AuthStatus } from "../types";

function axiosErrorWith(status: number): AxiosError {
  const config = { url: "/auth/login", headers: new AxiosHeaders() } as InternalAxiosRequestConfig;
  const error = new AxiosError("failed", "ERR_BAD_REQUEST", config);
  error.response = { status, statusText: "", data: {}, headers: {}, config } as AxiosResponse;
  return error;
}

function LandingProbe() {
  const location = useLocation();
  return <div data-testid="landed">landed on {location.pathname}</div>;
}

function renderLogin(
  login: AuthState["login"],
  { status = "anonymous" as AuthStatus, state }: { status?: AuthStatus; state?: unknown } = {},
) {
  const auth: AuthState = {
    user: null,
    status,
    isAdmin: false,
    permissions: [],
    hasPermission: () => false,
    hasAnyPermission: () => false,
    login,
    logout: () => undefined,
  };

  return render(
    <AuthContext value={auth}>
      <MemoryRouter initialEntries={[{ pathname: "/login", state }]}>
        <Routes>
          <Route path="/login" element={<LoginPage />} />
          <Route path="/" element={<LandingProbe />} />
          <Route path="/deep/link" element={<LandingProbe />} />
        </Routes>
      </MemoryRouter>
    </AuthContext>,
  );
}

async function fillAndSubmit() {
  await userEvent.type(screen.getByLabelText("Email"), "person@local.test");
  await userEvent.type(screen.getByLabelText("Password"), "Correct!23");
  await userEvent.click(screen.getByRole("button", { name: /sign in/i }));
}

describe("LoginPage", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("renders labelled inputs and a submit button", () => {
    renderLogin(async () => undefined);

    expect(screen.getByLabelText("Email")).toBeInTheDocument();
    expect(screen.getByLabelText("Password")).toBeInTheDocument();
    expect(screen.getByRole("button", { name: /sign in/i })).toBeInTheDocument();
  });

  it("disables submit while either field is empty", async () => {
    renderLogin(async () => undefined);
    const button = screen.getByRole("button", { name: /sign in/i });

    expect(button).toBeDisabled();

    await userEvent.type(screen.getByLabelText("Email"), "person@local.test");
    expect(button).toBeDisabled();

    await userEvent.type(screen.getByLabelText("Password"), "Correct!23");
    expect(button).toBeEnabled();
  });

  it("shows the credentials message on 401, inside an alert region", async () => {
    renderLogin(() => Promise.reject(axiosErrorWith(401)));

    await fillAndSubmit();

    const alert = await screen.findByRole("alert");
    expect(alert).toHaveTextContent("Invalid email or password.");
  });

  it.each([500, 400])("shows the generic message on %i", async (status) => {
    renderLogin(() => Promise.reject(axiosErrorWith(status)));

    await fillAndSubmit();

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Something went wrong. Please try again.",
    );
  });

  it("shows the generic message on a network error", async () => {
    renderLogin(() => Promise.reject(new Error("Network Error")));

    await fillAndSubmit();

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Something went wrong. Please try again.",
    );
  });

  it("disables submit while pending and re-enables it after a failure", async () => {
    let release: (() => void) | undefined;
    const pending = new Promise<void>((_, reject) => {
      release = () => reject(axiosErrorWith(401));
    });

    renderLogin(() => pending);
    await fillAndSubmit();

    const button = screen.getByRole("button", { name: /signing in/i });
    expect(button).toBeDisabled();

    release?.();

    await waitFor(() => expect(screen.getByRole("button", { name: /sign in/i })).toBeEnabled());
  });

  it("navigates to / on success", async () => {
    renderLogin(async () => undefined);

    await fillAndSubmit();

    expect(await screen.findByTestId("landed")).toHaveTextContent("landed on /");
  });

  it("navigates to the preserved location.state.from when present", async () => {
    renderLogin(async () => undefined, { state: { from: "/deep/link" } });

    await fillAndSubmit();

    expect(await screen.findByTestId("landed")).toHaveTextContent("landed on /deep/link");
  });

  it("calls login exactly once per submit, even on a rapid double-click", async () => {
    let resolve: (() => void) | undefined;
    const pending = new Promise<void>((r) => {
      resolve = () => r();
    });
    const login = vi.fn(() => pending);

    renderLogin(login);

    await userEvent.type(screen.getByLabelText("Email"), "person@local.test");
    await userEvent.type(screen.getByLabelText("Password"), "Correct!23");

    const button = screen.getByRole("button", { name: /sign in/i });
    await userEvent.dblClick(button);

    expect(login).toHaveBeenCalledTimes(1);
    resolve?.();
  });

  it("redirects away when already authenticated", () => {
    renderLogin(async () => undefined, { status: "authenticated" });

    expect(screen.getByTestId("landed")).toHaveTextContent("landed on /");
  });

  it("does not import authApi — the request must go through useAuth().login", () => {
    // A static guard for the rule the duplicate-request requirement depends on.
    expect(loginPageSource).not.toMatch(/from\s+["'].*authApi["']/);
  });

  it("links straight to the Channel Simulator and the public Live Chat widget", () => {
    renderLogin(async () => undefined);

    expect(screen.getByRole("link", { name: /Channel Simulator/ })).toHaveAttribute("href", "/admin/channel-simulator");
    expect(screen.getByRole("link", { name: /Live Chat widget/ })).toHaveAttribute("href", "/live-chat");
  });
});
