import { http } from "../../lib/http";
import type { LiveChatMessage, LiveChatSessionPublic, StartLiveChatSessionPayload, StartLiveChatSessionResult } from "./types";

/**
 * Story 21: the anonymous, public entry point for the customer-facing widget —
 * `POST /api/public/live-chat/sessions` plus polling/reply on the session it returns. Same shared
 * `http` instance as every other call (see `publicWebFormApi.ts`) — the endpoints are
 * `[AllowAnonymous]` and ignore any bearer token a signed-in staff member happens to carry.
 */
export async function startSession(payload: StartLiveChatSessionPayload): Promise<StartLiveChatSessionResult> {
  const response = await http.post<StartLiveChatSessionResult>("/public/live-chat/sessions", payload);
  return response.data;
}

export async function getSession(sessionId: string, sessionToken: string): Promise<LiveChatSessionPublic> {
  const response = await http.get<LiveChatSessionPublic>(`/public/live-chat/sessions/${sessionId}/messages`, {
    params: { sessionToken },
  });
  return response.data;
}

export async function sendMessage(sessionId: string, sessionToken: string, body: string): Promise<LiveChatMessage> {
  const response = await http.post<LiveChatMessage>(`/public/live-chat/sessions/${sessionId}/messages`, { sessionToken, body });
  return response.data;
}
