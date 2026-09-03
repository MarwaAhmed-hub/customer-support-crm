export interface AuditLogListItem {
  id: string;
  occurredAtUtc: string;
  actorUserId: string | null;
  actorEmail: string | null;
  action: string;
  entityType: string | null;
  entityId: string | null;
  summary: string;
  ipAddress: string | null;
}

export interface AuditLogPage {
  items: AuditLogListItem[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface AuditLogQuery {
  page?: number;
  pageSize?: number;
  action?: string;
  entityType?: string;
  actorUserId?: string;
  fromUtc?: string;
  toUtc?: string;
}
