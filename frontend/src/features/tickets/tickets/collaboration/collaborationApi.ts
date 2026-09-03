import { http } from "../../../../lib/http";
import type { TicketCollaborationComment } from "./types";

/** Chronological (oldest first), matching the backend's ordering — never re-sorted client-side. */
export async function listCollaborationComments(ticketId: string): Promise<TicketCollaborationComment[]> {
  const response = await http.get<TicketCollaborationComment[]>(`/tickets/${ticketId}/collaboration-comments`);
  return response.data;
}

export async function createCollaborationComment(ticketId: string, body: string): Promise<TicketCollaborationComment> {
  const response = await http.post<TicketCollaborationComment>(`/tickets/${ticketId}/collaboration-comments`, { body });
  return response.data;
}
