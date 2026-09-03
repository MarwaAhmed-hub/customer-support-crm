import type { AxiosError, AxiosResponse, InternalAxiosRequestConfig } from "axios";
import { AxiosHeaders } from "axios";
import { beforeEach, describe, expect, it, vi } from "vitest";

/**
 * The interceptors are exercised directly rather than over a mocked network: they are plain
 * functions on the instance, and calling them keeps the assertions about *their* behaviour.
 */
async function freshModule() {
  vi.resetModules();
  window.localStorage.clear();

  const storage = await import("../tokenStorage");
  const { http } = await import("../http");

  const requestInterceptor = http.interceptors.request as unknown as {
    handlers: { fulfilled: (c: InternalAxiosRequestConfig) => InternalAxiosRequestConfig }[];
  };
  const responseInterceptor = http.interceptors.response as unknown as {
    handlers: {
      fulfilled: (r: AxiosResponse) => AxiosResponse;
      rejected: (e: unknown) => Promise<never>;
    }[];
  };

  const onRequest = requestInterceptor.handlers[0]?.fulfilled;
  const onRejected = responseInterceptor.handlers[0]?.rejected;
  if (onRequest === undefined || onRejected === undefined) {
    throw new Error("Interceptors are not registered on the http instance.");
  }

  return { storage, onRequest, onRejected };
}

function axiosErrorWith(status: number, url: string): AxiosError {
  const config = { url, headers: new AxiosHeaders() } as InternalAxiosRequestConfig;
  const error = new Error("request failed") as AxiosError;
  error.isAxiosError = true;
  error.config = config;
  error.toJSON = () => ({});
  error.response = {
    status,
    statusText: "",
    data: {},
    headers: {},
    config,
  } as AxiosResponse;
  return error;
}

describe("http interceptors", () => {
  beforeEach(() => {
    window.localStorage.clear();
  });

  it("attaches the bearer header when a token is present", async () => {
    const { storage, onRequest } = await freshModule();
    storage.setToken("token-a");

    const config = onRequest({ headers: new AxiosHeaders() } as InternalAxiosRequestConfig);

    expect(config.headers.get("Authorization")).toBe("Bearer token-a");
  });

  it("omits the bearer header when there is no token", async () => {
    const { onRequest } = await freshModule();

    const config = onRequest({ headers: new AxiosHeaders() } as InternalAxiosRequestConfig);

    expect(config.headers.get("Authorization")).toBeUndefined();
  });

  it("clears the token on a 401 and notifies subscribers", async () => {
    const { storage, onRejected } = await freshModule();
    storage.setToken("token-a");

    const seen: (string | null)[] = [];
    storage.subscribe((token) => seen.push(token));

    await expect(onRejected(axiosErrorWith(401, "/auth/me"))).rejects.toBeDefined();

    expect(storage.getToken()).toBeNull();
    expect(seen).toEqual([null]);
  });

  it("does NOT clear the token on a 401 from the login path", async () => {
    const { storage, onRejected } = await freshModule();
    storage.setToken("token-a");

    await expect(onRejected(axiosErrorWith(401, "/auth/login"))).rejects.toBeDefined();

    expect(storage.getToken()).toBe("token-a");
  });

  it.each([400, 403, 500])("leaves the token intact on %i", async (status) => {
    const { storage, onRejected } = await freshModule();
    storage.setToken("token-a");

    await expect(onRejected(axiosErrorWith(status, "/auth/me"))).rejects.toBeDefined();

    expect(storage.getToken()).toBe("token-a");
  });

  it("propagates the rejection rather than swallowing or retrying it", async () => {
    const { onRejected } = await freshModule();
    const error = axiosErrorWith(401, "/auth/me");

    await expect(onRejected(error)).rejects.toBe(error);
  });
});
