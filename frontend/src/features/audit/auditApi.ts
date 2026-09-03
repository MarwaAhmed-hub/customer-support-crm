import { http } from "../../lib/http";
import type { AuditLogPage, AuditLogQuery } from "./types";

export async function listAuditLogs(query: AuditLogQuery = {}): Promise<AuditLogPage> {
  const params = {
    page: query.page ?? 1,
    pageSize: query.pageSize ?? 25,
    ...(query.action ? { action: query.action } : {}),
    ...(query.entityType ? { entityType: query.entityType } : {}),
    ...(query.actorUserId ? { actorUserId: query.actorUserId } : {}),
    ...(query.fromUtc ? { fromUtc: query.fromUtc } : {}),
    ...(query.toUtc ? { toUtc: query.toUtc } : {}),
  };
  const response = await http.get<AuditLogPage>("/audit-logs", { params });
  return response.data;
}
