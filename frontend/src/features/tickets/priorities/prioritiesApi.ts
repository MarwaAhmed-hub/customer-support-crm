import { http } from "../../../lib/http";
import type { CreateTicketPriorityPayload, TicketPriority, UpdateTicketPriorityPayload } from "./types";

export interface ListTicketPrioritiesParams {
  includeInactive?: boolean;
}

export async function listTicketPriorities(params: ListTicketPrioritiesParams = {}): Promise<TicketPriority[]> {
  const response = await http.get<TicketPriority[]>("/tickets/priorities", { params });
  return response.data;
}

export async function getTicketPriority(id: string): Promise<TicketPriority> {
  const response = await http.get<TicketPriority>(`/tickets/priorities/${id}`);
  return response.data;
}

export async function createTicketPriority(payload: CreateTicketPriorityPayload): Promise<TicketPriority> {
  const response = await http.post<TicketPriority>("/tickets/priorities", payload);
  return response.data;
}

export async function updateTicketPriority(id: string, payload: UpdateTicketPriorityPayload): Promise<TicketPriority> {
  const response = await http.put<TicketPriority>(`/tickets/priorities/${id}`, payload);
  return response.data;
}
