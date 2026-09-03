import { http } from "../../../lib/http";
import type { CustomerNote } from "./types";

export async function listNotes(customerId: string): Promise<CustomerNote[]> {
  const response = await http.get<CustomerNote[]>(`/customers/${customerId}/notes`);
  return response.data;
}

export async function createNote(customerId: string, body: string): Promise<CustomerNote> {
  const response = await http.post<CustomerNote>(`/customers/${customerId}/notes`, { body });
  return response.data;
}

export async function updateNote(customerId: string, noteId: string, body: string): Promise<CustomerNote> {
  const response = await http.put<CustomerNote>(`/customers/${customerId}/notes/${noteId}`, { body });
  return response.data;
}

export async function deleteNote(customerId: string, noteId: string): Promise<void> {
  await http.delete(`/customers/${customerId}/notes/${noteId}`);
}
