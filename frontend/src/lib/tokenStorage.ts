/**
 * The single source of truth for the access token.
 *
 * This module imports **nothing** — not axios, not React — so it sits at the bottom of the
 * dependency graph and is what breaks the axios <-> AuthContext cycle:
 *
 *   tokenStorage.ts  (no imports)
 *         |                    |
 *      http.ts              AuthContext.tsx
 *         |
 *     authApi.ts  ---------> (imported by AuthContext)
 *
 * There is no `registerTokenProvider()` back-channel, no module-level callback injected from React,
 * and no window CustomEvent bus.
 */

const STORAGE_KEY = "crm.auth.token";

type Listener = (token: string | null) => void;

const listeners = new Set<Listener>();

/** Reads go to memory, so a localStorage failure degrades to memory-only rather than breaking auth. */
let current: string | null = readFromStorage();

function readFromStorage(): string | null {
  try {
    if (typeof window === "undefined") return null;
    return window.localStorage.getItem(STORAGE_KEY);
  } catch {
    // Private browsing, blocked site data, quota errors — never let this reach a render.
    return null;
  }
}

function writeToStorage(token: string | null): void {
  try {
    if (typeof window === "undefined") return;
    if (token === null) {
      window.localStorage.removeItem(STORAGE_KEY);
    } else {
      window.localStorage.setItem(STORAGE_KEY, token);
    }
  } catch {
    // Memory-only for this session: the user stays logged in until reload.
  }
}

function notify(token: string | null): void {
  for (const listener of listeners) {
    listener(token);
  }
}

export function getToken(): string | null {
  return current;
}

export function setToken(token: string): void {
  current = token;
  writeToStorage(token);
  notify(token);
}

export function clearToken(): void {
  current = null;
  writeToStorage(null);
  notify(null);
}

export function subscribe(listener: Listener): () => void {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

/**
 * Cross-tab synchronization. Living here rather than in AuthContext is deliberate: the token is this
 * module's state, and every consumer already listens through `subscribe`.
 */
if (typeof window !== "undefined") {
  window.addEventListener("storage", (event: StorageEvent) => {
    if (event.key !== STORAGE_KEY) return;
    current = event.newValue;
    notify(current);
  });
}

/** Exported for tests only — resets module state between cases. */
export function __resetForTests(): void {
  current = readFromStorage();
  listeners.clear();
}
