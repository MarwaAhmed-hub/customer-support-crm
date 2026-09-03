import { http, LOGIN_PATH } from "../../lib/http";
import type { LoginRequest, LoginResponse, MeResponse } from "./types";

/**
 * The only module that talks to the auth endpoints. No token handling lives here — the axios
 * interceptor owns that.
 */

export async function login(request: LoginRequest): Promise<LoginResponse> {
  const response = await http.post<LoginResponse>(LOGIN_PATH, request);
  return response.data;
}

export async function me(config?: { signal?: AbortSignal }): Promise<MeResponse> {
  const response = await http.get<MeResponse>("/auth/me", config);
  return response.data;
}
