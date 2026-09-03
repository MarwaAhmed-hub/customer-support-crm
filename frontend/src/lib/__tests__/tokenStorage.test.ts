import { beforeEach, describe, expect, it, vi } from "vitest";

async function freshModule() {
  vi.resetModules();
  window.localStorage.clear();
  return await import("../tokenStorage");
}

describe("tokenStorage", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    window.localStorage.clear();
  });

  it("round-trips through memory and localStorage", async () => {
    const storage = await freshModule();

    expect(storage.getToken()).toBeNull();

    storage.setToken("token-a");
    expect(storage.getToken()).toBe("token-a");
    expect(window.localStorage.getItem("crm.auth.token")).toBe("token-a");

    storage.clearToken();
    expect(storage.getToken()).toBeNull();
    expect(window.localStorage.getItem("crm.auth.token")).toBeNull();
  });

  it("notifies subscribers on set and clear, and stops after unsubscribe", async () => {
    const storage = await freshModule();
    const seen: (string | null)[] = [];

    const unsubscribe = storage.subscribe((token) => seen.push(token));

    storage.setToken("token-a");
    storage.clearToken();
    unsubscribe();
    storage.setToken("token-b");

    expect(seen).toEqual(["token-a", null]);
  });

  it("keeps the token in memory when localStorage.setItem throws", async () => {
    const storage = await freshModule();

    vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => {
      throw new DOMException("QuotaExceededError");
    });

    expect(() => storage.setToken("token-a")).not.toThrow();
    expect(storage.getToken()).toBe("token-a");
  });

  it("reads null rather than throwing when localStorage.getItem throws at load", async () => {
    vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => {
      throw new DOMException("SecurityError");
    });

    const storage = await freshModule();

    expect(storage.getToken()).toBeNull();
  });

  it("reacts to a storage event from another tab", async () => {
    const storage = await freshModule();
    const seen: (string | null)[] = [];
    storage.subscribe((token) => seen.push(token));

    window.dispatchEvent(
      new StorageEvent("storage", { key: "crm.auth.token", newValue: "from-other-tab" }),
    );
    expect(storage.getToken()).toBe("from-other-tab");

    window.dispatchEvent(new StorageEvent("storage", { key: "crm.auth.token", newValue: null }));
    expect(storage.getToken()).toBeNull();

    // An unrelated key must be ignored.
    window.dispatchEvent(new StorageEvent("storage", { key: "something.else", newValue: "x" }));

    expect(seen).toEqual(["from-other-tab", null]);
  });
});
