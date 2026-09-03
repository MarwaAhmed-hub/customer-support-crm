import { http } from "../../lib/http";
import type { CreateQuickReplyPayload, QuickReply, UpdateQuickReplyPayload } from "./types";

export interface ListQuickRepliesParams {
  includeInactive?: boolean;
  search?: string;
}

export async function listQuickReplies(params: ListQuickRepliesParams = {}): Promise<QuickReply[]> {
  const response = await http.get<QuickReply[]>("/quick-replies", { params });
  return response.data;
}

export async function getQuickReply(id: string): Promise<QuickReply> {
  const response = await http.get<QuickReply>(`/quick-replies/${id}`);
  return response.data;
}

export async function createQuickReply(payload: CreateQuickReplyPayload): Promise<QuickReply> {
  const response = await http.post<QuickReply>("/quick-replies", payload);
  return response.data;
}

export async function updateQuickReply(id: string, payload: UpdateQuickReplyPayload): Promise<QuickReply> {
  const response = await http.put<QuickReply>(`/quick-replies/${id}`, payload);
  return response.data;
}

export async function deleteQuickReply(id: string): Promise<void> {
  await http.delete(`/quick-replies/${id}`);
}
