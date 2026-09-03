/** Mirrors the backend DTOs in `Api/QuickReplies/QuickReplyDtos.cs` (camelCase — System.Text.Json's default). */

export interface QuickReply {
  id: string;
  title: string;
  body: string;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreateQuickReplyPayload {
  title: string;
  body: string;
}

export interface UpdateQuickReplyPayload {
  title: string;
  body: string;
  isActive: boolean;
}
