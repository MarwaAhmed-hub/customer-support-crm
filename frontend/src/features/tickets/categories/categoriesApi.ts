import { http } from "../../../lib/http";
import type { CreateTicketCategoryPayload, TicketCategory, UpdateTicketCategoryPayload } from "./types";

export interface ListTicketCategoriesParams {
  includeInactive?: boolean;
}

export async function listTicketCategories(params: ListTicketCategoriesParams = {}): Promise<TicketCategory[]> {
  const response = await http.get<TicketCategory[]>("/tickets/categories", { params });
  return response.data;
}

export async function getTicketCategory(id: string): Promise<TicketCategory> {
  const response = await http.get<TicketCategory>(`/tickets/categories/${id}`);
  return response.data;
}

export async function createTicketCategory(payload: CreateTicketCategoryPayload): Promise<TicketCategory> {
  const response = await http.post<TicketCategory>("/tickets/categories", payload);
  return response.data;
}

export async function updateTicketCategory(id: string, payload: UpdateTicketCategoryPayload): Promise<TicketCategory> {
  const response = await http.put<TicketCategory>(`/tickets/categories/${id}`, payload);
  return response.data;
}
