/** Mirrors the backend DTOs in `Api/Tickets/Collaboration/TicketCollaborationCommentDtos.cs` (camelCase — System.Text.Json's default). */

export interface TicketCollaborationComment {
  id: string;
  ticketId: string;
  body: string;
  authorUserId: string;
  authorDisplayName: string | null;
  createdAt: string;
}
