import { http } from "../../lib/http";
import type { LiveChatMessage, LiveChatSessionDetail, LiveChatSessionListItem, LiveChatStatusValue } from "./types";

/** Story 21: the authenticated, agent-facing side — `api/live-chat/conversations*`, gated by `livechat.view`/`livechat.send`. */

export async function listConversations(status?: LiveChatStatusValue): Promise<LiveChatSessionListItem[]> {
  const response = await http.get<LiveChatSessionListItem[]>("/live-chat/conversations", {
    params: status !== undefined ? { status } : undefined,
  });
  return response.data;
}

export async function getConversation(sessionId: string): Promise<LiveChatSessionDetail> {
  const response = await http.get<LiveChatSessionDetail>(`/live-chat/conversations/${sessionId}`);
  return response.data;
}

export async function sendReply(sessionId: string, body: string): Promise<LiveChatMessage> {
  const response = await http.post<LiveChatMessage>(`/live-chat/conversations/${sessionId}/messages`, { body });
  return response.data;
}
