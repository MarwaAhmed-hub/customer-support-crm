import { http } from "../../../lib/http";
import type { CreateTicketPayload, PagedResult, TicketDetail, TicketListItem, UpdateTicketPayload } from "./types";

export interface ListTicketsParams {
  customerId?: string;
  categoryId?: string;
  priorityId?: string;
  status?: string;
  /** The escalated-queue filter — lets a Manager pull up every currently escalated ticket in one view. */
  isEscalated?: boolean;
  /** Story 23: the Unassigned Tickets Queue filter — tickets with no agent at all. */
  unassignedOnly?: boolean;
  search?: string;
  page?: number;
  pageSize?: number;
}

export async function listTickets(params: ListTicketsParams = {}): Promise<PagedResult<TicketListItem>> {
  const response = await http.get<PagedResult<TicketListItem>>("/tickets", { params });
  return response.data;
}

export async function getTicket(id: string): Promise<TicketDetail> {
  const response = await http.get<TicketDetail>(`/tickets/${id}`);
  return response.data;
}

export async function createTicket(payload: CreateTicketPayload): Promise<TicketDetail> {
  const response = await http.post<TicketDetail>("/tickets", payload);
  return response.data;
}

export async function updateTicket(id: string, payload: UpdateTicketPayload): Promise<TicketDetail> {
  const response = await http.put<TicketDetail>(`/tickets/${id}`, payload);
  return response.data;
}

/** `assignedUserId: null` unassigns the ticket — there is no separate "unassign" call. */
export async function updateTicketAssignment(id: string, assignedUserId: string | null): Promise<TicketDetail> {
  const response = await http.put<TicketDetail>(`/tickets/${id}/assignment`, { assignedUserId });
  return response.data;
}

/** Story 13: rejected with 400 if `status` is unknown or not reachable from the ticket's current status. */
export async function updateTicketStatus(id: string, status: string): Promise<TicketDetail> {
  const response = await http.put<TicketDetail>(`/tickets/${id}/status`, { status });
  return response.data;
}

/** Story 13: `reason` must be non-empty; rejected with 400 if the ticket is already escalated. */
export async function escalateTicket(id: string, reason: string): Promise<TicketDetail> {
  const response = await http.post<TicketDetail>(`/tickets/${id}/escalation`, { reason });
  return response.data;
}

/** Story 13: rejected with 400 if the ticket is not currently escalated. */
export async function deEscalateTicket(id: string): Promise<TicketDetail> {
  const response = await http.delete<TicketDetail>(`/tickets/${id}/escalation`);
  return response.data;
}

/** Story 19: 400 if the ticket isn't email-sourced or its customer has no email on file; 502 (no interaction persisted) if the configured `IEmailSender` reports failure. */
export async function sendEmailReply(id: string, body: string): Promise<TicketDetail> {
  const response = await http.post<TicketDetail>(`/tickets/${id}/email-replies`, { body });
  return response.data;
}

/** Story 20: 400 if the ticket's channel isn't sendable (WhatsApp/Sms) or there is no recipient phone number; 502 (no interaction persisted) if the configured `IChannelMessageDispatcher` reports failure. */
export async function sendChannelReply(id: string, body: string): Promise<TicketDetail> {
  const response = await http.post<TicketDetail>(`/tickets/${id}/channel-replies`, { body });
  return response.data;
}
