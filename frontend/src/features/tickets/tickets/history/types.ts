/** Mirrors the backend DTO in `Api/Tickets/History/TicketHistoryDtos.cs` (camelCase — System.Text.Json's default). */

export type TicketHistoryEventType =
  | "Created"
  | "Updated"
  | "Assigned"
  | "Reassigned"
  | "StatusChanged"
  | "PriorityChanged"
  | "CategoryChanged";

export interface TicketHistoryEntry {
  id: string;
  ticketId: string;
  eventType: TicketHistoryEventType | string;
  field: string | null;
  previousValue: string | null;
  newValue: string | null;
  summary: string;
  performedByUserId: string | null;
  performedByUserName: string | null;
  createdAt: string;
}
