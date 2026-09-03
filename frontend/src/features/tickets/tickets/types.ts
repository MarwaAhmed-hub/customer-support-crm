/** Mirrors the backend DTOs in `Api/Tickets/Tickets/TicketDtos.cs` (camelCase — System.Text.Json's default). */

/** Story 13: the agreed ticket status lifecycle. Mirrors `Domain.Tickets.TicketStatuses`. */
export type TicketStatus = "open" | "in_progress" | "pending" | "resolved" | "closed";

/**
 * The directed transition graph the backend enforces (`TicketStatuses.CanTransition`) — the UI reads
 * this to only ever offer a status change the API will actually accept.
 */
export const TICKET_STATUS_TRANSITIONS: Record<TicketStatus, TicketStatus[]> = {
  open: ["in_progress", "closed"],
  in_progress: ["pending", "resolved"],
  pending: ["in_progress"],
  resolved: ["closed", "in_progress"],
  closed: ["in_progress"],
};

/** Story 22: "running" | "met" | "breached". Mirrors `Domain.Sla.SlaStatuses`. */
export type SlaStatus = "running" | "met" | "breached";

/** Story 22: null when the ticket has no `TicketSla` row (pre-dates the migration, or its policy was missing at creation). Statuses are lazily evaluated as of the moment the ticket was read, so a still-`running` clock past its due time already reads `breached` here. */
export interface TicketSla {
  startedAt: string;
  firstResponseDueAt: string;
  resolutionDueAt: string;
  firstResponseStatus: SlaStatus;
  resolutionStatus: SlaStatus;
  firstResponseAt: string | null;
  resolvedAt: string | null;
}

export interface TicketListItem {
  id: string;
  customerId: string;
  customerName: string;
  subject: string;
  categoryId: string;
  categoryName: string;
  priorityId: string;
  priorityName: string;
  status: string;
  createdByUserId: string;
  createdByUserName: string | null;
  assignedUserId: string | null;
  assignedUserName: string | null;
  isEscalated: boolean;
  createdAt: string;
  updatedAt: string;
  /** Story 19/20: "Email" | "WebForm" | "WhatsApp" | "Sms" | null — null for every manually/internally created ticket. */
  sourceChannel: string | null;
  sla: TicketSla | null;
}

/** The detail response is the list-item shape plus the full `description` and escalation detail fields (omitted from the list to keep it lightweight). */
export interface TicketDetail extends TicketListItem {
  description: string;
  escalatedAt: string | null;
  escalatedByUserId: string | null;
  escalatedByUserName: string | null;
  escalationReason: string | null;
  /** The ticket's category's department, null if the category has none — drives the assignee picker's department filter. */
  categoryDepartmentId: string | null;
}

export interface CreateTicketPayload {
  customerId: string;
  subject: string;
  description: string;
  categoryId: string;
  priorityId: string;
}

export interface UpdateTicketPayload {
  subject: string;
  description: string;
  categoryId: string;
  priorityId: string;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
}
