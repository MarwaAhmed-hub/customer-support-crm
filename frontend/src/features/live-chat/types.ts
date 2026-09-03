/** Mirrors the backend DTOs in `Api/LiveChat/LiveChatDtos.cs` (camelCase — System.Text.Json's default). */

/** "Waiting" | "Active" | "Closed" — derived server-side from the linked ticket's assignment/status, never stored. Mirrors `Api.LiveChat.LiveChatStatus`. */
export type LiveChatStatusValue = "Waiting" | "Active" | "Closed";

export interface LiveChatMessage {
  id: string;
  /** "Customer" | "Agent" */
  sender: string;
  senderUserId: string | null;
  senderName: string | null;
  body: string;
  occurredAt: string;
}

export interface StartLiveChatSessionPayload {
  name?: string;
  email?: string;
  phone?: string;
  message: string;
}

export interface StartLiveChatSessionResult {
  sessionId: string;
  sessionToken: string;
  ticketId: string;
  customerId: string;
  status: LiveChatStatusValue;
}

export interface LiveChatSessionPublic {
  sessionId: string;
  ticketId: string;
  status: LiveChatStatusValue;
  messages: LiveChatMessage[];
}

export interface LiveChatSessionListItem {
  sessionId: string;
  ticketId: string;
  status: LiveChatStatusValue;
  customerId: string;
  customerName: string;
  subject: string;
  assignedUserId: string | null;
  assignedUserName: string | null;
  createdAt: string;
  lastMessageAt: string;
}

export interface LiveChatSessionDetail {
  sessionId: string;
  ticketId: string;
  status: LiveChatStatusValue;
  customerId: string;
  customerName: string;
  subject: string;
  assignedUserId: string | null;
  assignedUserName: string | null;
  messages: LiveChatMessage[];
}
