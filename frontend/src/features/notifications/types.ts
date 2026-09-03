/** Mirrors the backend DTOs in `Api/Notifications/NotificationDtos.cs` (camelCase — System.Text.Json's default). */

/** "TicketAssigned" | "SlaWarning" | "SlaBreached". Mirrors `Domain.Notifications.NotificationEventType`. */
export type NotificationEventType = "TicketAssigned" | "SlaWarning" | "SlaBreached";

/** "FirstResponse" | "Resolution" — null for a TicketAssigned notification. Mirrors `Domain.Tickets.SlaType`. */
export type NotificationSlaType = "FirstResponse" | "Resolution" | null;

export interface Notification {
  id: string;
  eventType: NotificationEventType;
  slaType: NotificationSlaType;
  ticketId: string;
  subject: string;
  body: string;
  createdAtUtc: string;
  readAtUtc: string | null;
}

export interface NotificationListResponse {
  items: Notification[];
  total: number;
  page: number;
  pageSize: number;
}
