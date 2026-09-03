import { http } from "../../lib/http";
import type { NotificationListResponse } from "./types";

export interface ListMyNotificationsParams {
  unreadOnly?: boolean;
  page?: number;
  pageSize?: number;
}

export async function listMyNotifications(params: ListMyNotificationsParams = {}): Promise<NotificationListResponse> {
  const response = await http.get<NotificationListResponse>("/notifications/me", {
    params: { unreadOnly: params.unreadOnly ?? false, page: params.page ?? 1, pageSize: params.pageSize ?? 20 },
  });
  return response.data;
}

export async function markNotificationRead(id: string): Promise<void> {
  await http.post(`/notifications/${id}/read`);
}

export async function markAllNotificationsRead(): Promise<void> {
  await http.post("/notifications/read-all");
}
