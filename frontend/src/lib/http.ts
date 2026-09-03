import axios from "axios";
import { clearToken, getToken } from "./tokenStorage";

export const LOGIN_PATH = "/auth/login";

export const http = axios.create({ baseURL: "/api" });

// Read the token at request time, not at instance creation, so a login mid-session is picked up
// without rebuilding the client.
http.interceptors.request.use((config) => {
  const token = getToken();
  if (token !== null) {
    config.headers.set("Authorization", `Bearer ${token}`);
  }
  return config;
});

http.interceptors.response.use(
  (response) => response,
  (error: unknown) => {
    if (axios.isAxiosError(error) && error.response?.status === 401) {
      // A 401 from the login request means "wrong password", not "session expired". Clearing the
      // token there would fire a spurious logout notification while the user is already
      // unauthenticated.
      const url = error.config?.url ?? "";
      if (!url.startsWith(LOGIN_PATH)) {
        // tokenStorage's subscribers do the rest: AuthContext drops the user and ProtectedRoute
        // redirects. No retry, no refresh call (out of scope), and no navigation from here —
        // routing belongs to React, and an imperative redirect would reintroduce the cycle.
        clearToken();
      }
    }
    return Promise.reject(error);
  },
);
