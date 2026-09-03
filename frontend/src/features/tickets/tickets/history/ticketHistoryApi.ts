import { http } from "../../../../lib/http";
import type { TicketHistoryEntry } from "./types";

/** Chronological (oldest first), matching the backend's ordering — never re-sorted client-side. */
export async function getTicketHistory(ticketId: string): Promise<TicketHistoryEntry[]> {
  const response = await http.get<TicketHistoryEntry[]>(`/tickets/${ticketId}/history`);
  return response.data;
}
