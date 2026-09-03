/** Mirrors the backend DTOs in `Api/Tickets/Priorities/TicketPriorityDtos.cs` (camelCase — System.Text.Json's default). */

export interface TicketPriority {
  id: string;
  name: string;
  sortOrder: number;
  description: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreateTicketPriorityPayload {
  name: string;
  sortOrder: number;
  description: string | null;
}

export interface UpdateTicketPriorityPayload {
  name: string;
  sortOrder: number;
  description: string | null;
  isActive: boolean;
}
