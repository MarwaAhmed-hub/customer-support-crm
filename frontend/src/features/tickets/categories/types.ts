/** Mirrors the backend DTOs in `Api/Tickets/Categories/TicketCategoryDtos.cs` (camelCase — System.Text.Json's default). */

export interface TicketCategory {
  id: string;
  name: string;
  description: string | null;
  isActive: boolean;
  departmentId: string | null;
  departmentName: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface CreateTicketCategoryPayload {
  name: string;
  description: string | null;
  departmentId?: string | null;
}

export interface UpdateTicketCategoryPayload {
  name: string;
  description: string | null;
  isActive: boolean;
  departmentId?: string | null;
}
