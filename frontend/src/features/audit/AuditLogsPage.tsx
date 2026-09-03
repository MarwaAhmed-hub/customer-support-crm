import {
  Alert,
  Box,
  CircularProgress,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from "@mui/material";
import { useEffect, useState } from "react";
import * as auditApi from "./auditApi";
import type { AuditLogListItem } from "./types";

const PAGE_SIZE = 25;

/** Format action codes into user-friendly display names */
function getDisplayAction(action: string): string {
  const actionMap: Record<string, string> = {
    login: "Login Success",
    create: "Created",
    update: "Updated",
    activate: "Activated",
    deactivate: "Deactivated",
    "role.permissions.update": "Permissions Updated",
    "user.role.assign": "Role Assigned",
    "user.role.remove": "Role Removed",
  };
  return actionMap[action] ?? action;
}

/** Format the audit log date to readable format like "Aug 29, 2026, 8:20 PM" */
function formatDate(isoString: string): string {
  const date = new Date(isoString);
  return date.toLocaleDateString("en-US", {
    month: "short",
    day: "numeric",
    year: "numeric",
    hour: "numeric",
    minute: "2-digit",
    second: "2-digit",
    hour12: true,
  });
}

export function AuditLogsPage() {
  const [logs, setLogs] = useState<AuditLogListItem[]>([]);
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const [actionFilter, setActionFilter] = useState("");
  const [entityTypeFilter, setEntityTypeFilter] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    const query = {
      page,
      pageSize: PAGE_SIZE,
      ...(actionFilter ? { action: actionFilter } : {}),
      ...(entityTypeFilter ? { entityType: entityTypeFilter } : {}),
    };

    auditApi
      .listAuditLogs(query)
      .then((result) => {
        if (!cancelled) {
          setLogs(result.items);
          setTotal(result.totalCount);
        }
      })
      .catch(() => {
        if (!cancelled) setError("Could not load audit logs. Please try again.");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [page, actionFilter, entityTypeFilter]);

  const handleFilterChange = () => {
    setPage(1);
  };

  const handleActionFilterChange = (value: string) => {
    setActionFilter(value);
    handleFilterChange();
  };

  const handleEntityTypeFilterChange = (value: string) => {
    setEntityTypeFilter(value);
    handleFilterChange();
  };

  return (
    <Box sx={{ maxWidth: 1200 }}>
      <Typography variant="h4" sx={{ mb: 1 }}>
        Audit Logs
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
        Review system activity and user actions.
      </Typography>

      <Box sx={{ display: "flex", gap: 2, mb: 3, alignItems: "flex-end" }}>
        <TextField
          label="Filter by Action"
          value={actionFilter}
          onChange={(e) => handleActionFilterChange(e.target.value)}
          size="small"
          placeholder="e.g., login, create, update"
          sx={{ minWidth: 240 }}
        />
        <TextField
          label="Filter by Entity Type"
          value={entityTypeFilter}
          onChange={(e) => handleEntityTypeFilterChange(e.target.value)}
          size="small"
          placeholder="e.g., User, Role"
          sx={{ minWidth: 240 }}
        />
        {(actionFilter || entityTypeFilter) && (
          <Typography
            variant="body2"
            sx={{ cursor: "pointer", color: "primary.main", textDecoration: "underline" }}
            onClick={() => {
              setActionFilter("");
              setEntityTypeFilter("");
              setPage(1);
            }}
          >
            Clear filters
          </Typography>
        )}
      </Box>

      {error && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      {loading ? (
        <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, py: 4 }}>
          <CircularProgress size={22} />
          <Typography color="text.secondary">Loading…</Typography>
        </Box>
      ) : logs.length === 0 ? (
        <Paper variant="outlined" sx={{ p: 4, textAlign: "center" }}>
          <Typography color="text.secondary">No audit records match the current filters.</Typography>
        </Paper>
      ) : (
        <TableContainer component={Paper} variant="outlined" sx={{ overflowX: "auto" }}>
          {/* table-layout: fixed makes each column actually honor its declared width below — with the
              default "auto" layout, a maxWidth on a <td> is not a hard constraint, so a long
              break-point-free string (e.g. "ticket.collaboration.comment.created") would overflow its
              cell and visually overlap the next column instead of wrapping. */}
          <Table size="small" sx={{ tableLayout: "fixed", minWidth: 900 }}>
            <TableHead>
              <TableRow sx={{ backgroundColor: "#f5f5f5" }}>
                <TableCell sx={{ fontWeight: 600, width: "16%" }}>Actor</TableCell>
                <TableCell sx={{ fontWeight: 600, width: "16%" }}>Action</TableCell>
                <TableCell sx={{ fontWeight: 600, width: "14%" }}>Entity</TableCell>
                <TableCell sx={{ fontWeight: 600, width: "38%" }}>Description</TableCell>
                <TableCell sx={{ fontWeight: 600, width: "16%" }}>Creation Date</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {logs.map((log) => (
                <TableRow key={log.id}>
                  <TableCell sx={{ overflowWrap: "anywhere" }}>
                    {log.actorEmail ? (
                      <Typography variant="body2">{log.actorEmail}</Typography>
                    ) : (
                      <Typography variant="body2" sx={{ fontStyle: "italic", color: "text.secondary" }}>
                        System
                      </Typography>
                    )}
                  </TableCell>
                  <TableCell sx={{ overflowWrap: "anywhere" }}>
                    <Typography variant="body2" sx={{ fontWeight: 500 }}>
                      {getDisplayAction(log.action)}
                    </Typography>
                  </TableCell>
                  <TableCell sx={{ overflowWrap: "anywhere" }}>
                    <Typography variant="body2">{log.entityType ?? "—"}</Typography>
                  </TableCell>
                  <TableCell sx={{ overflowWrap: "anywhere" }}>
                    <Typography variant="body2">{log.summary}</Typography>
                  </TableCell>
                  <TableCell sx={{ overflowWrap: "anywhere" }}>
                    <Typography variant="body2" sx={{ color: "text.secondary" }}>
                      {formatDate(log.occurredAtUtc)}
                    </Typography>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {!loading && logs.length > 0 && (
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mt: 2 }}>
          <Typography variant="caption" color="text.secondary">
            Showing {(page - 1) * PAGE_SIZE + 1} to {Math.min(page * PAGE_SIZE, total)} of {total}
          </Typography>
          <Box sx={{ display: "flex", gap: 1 }}>
            <button
              onClick={() => setPage((p) => Math.max(1, p - 1))}
              disabled={page === 1}
              style={{ padding: "8px 16px", cursor: page === 1 ? "default" : "pointer" }}
            >
              Previous
            </button>
            <button
              onClick={() => setPage((p) => (p * PAGE_SIZE < total ? p + 1 : p))}
              disabled={page * PAGE_SIZE >= total}
              style={{ padding: "8px 16px", cursor: page * PAGE_SIZE >= total ? "default" : "pointer" }}
            >
              Next
            </button>
          </Box>
        </Box>
      )}
    </Box>
  );
}
