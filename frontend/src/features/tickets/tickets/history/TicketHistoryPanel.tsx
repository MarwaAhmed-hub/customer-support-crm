import { Alert, Box, Button, CircularProgress, Paper, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from "@mui/material";
import { useEffect, useState } from "react";
import * as ticketHistoryApi from "./ticketHistoryApi";
import type { TicketHistoryEntry, TicketHistoryEventType } from "./types";

const EVENT_TYPE_LABELS: Record<TicketHistoryEventType, string> = {
  Created: "Created",
  Updated: "Updated",
  Assigned: "Assigned",
  Reassigned: "Reassigned",
  StatusChanged: "Status Changed",
  PriorityChanged: "Priority Changed",
  CategoryChanged: "Category Changed",
};

function eventTypeLabel(eventType: string): string {
  return EVENT_TYPE_LABELS[eventType as TicketHistoryEventType] ?? eventType;
}

/** Same date format as CustomerInteractionHistory/AuditLogsPage — converts the UTC timestamp to the viewer's local time. */
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

/**
 * Presentational + data-loading section for a single ticket's business-lifecycle history (Story 14).
 * Mounted from `TicketDetailPage`, which only renders it once the ticket itself has loaded — this
 * component assumes it is only rendered for a caller who may already view the ticket (same
 * `tickets.view` permission gates both).
 */
export function TicketHistoryPanel({ ticketId }: { ticketId: string }) {
  const [entries, setEntries] = useState<TicketHistoryEntry[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [reloadToken, setReloadToken] = useState(0);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    ticketHistoryApi
      .getTicketHistory(ticketId)
      .then((result) => {
        if (!cancelled) setEntries(result);
      })
      .catch(() => {
        if (!cancelled) setError("Could not load ticket history. Please try again.");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [ticketId, reloadToken]);

  return (
    <Box>
      <Typography variant="overline" color="text.secondary" sx={{ letterSpacing: 0.4, fontWeight: 700 }}>
        History
      </Typography>

      {error !== null && (
        <Alert
          severity="error"
          sx={{ mt: 1.5, mb: 2 }}
          action={
            <Button color="inherit" size="small" onClick={() => setReloadToken((current) => current + 1)}>
              Retry
            </Button>
          }
        >
          {error}
        </Alert>
      )}

      {loading ? (
        <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, py: 3 }}>
          <CircularProgress size={22} />
          <Typography color="text.secondary">Loading…</Typography>
        </Box>
      ) : error !== null ? null : entries.length === 0 ? (
        <Paper variant="outlined" sx={{ p: 3, mt: 1.5, textAlign: "center" }}>
          <Typography color="text.secondary">No history recorded for this ticket yet.</Typography>
        </Paper>
      ) : (
        <TableContainer component={Paper} variant="outlined" sx={{ mt: 1.5 }}>
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Date</TableCell>
                <TableCell>Event</TableCell>
                <TableCell>Change</TableCell>
                <TableCell>By</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {entries.map((entry) => (
                <TableRow key={entry.id} hover>
                  <TableCell sx={{ whiteSpace: "nowrap" }}>{formatDate(entry.createdAt)}</TableCell>
                  <TableCell>{eventTypeLabel(entry.eventType)}</TableCell>
                  <TableCell sx={{ maxWidth: 400 }}>
                    <Typography variant="body2">{entry.summary}</Typography>
                    {(entry.previousValue !== null || entry.newValue !== null) && (
                      <Typography variant="caption" color="text.secondary" sx={{ display: "block" }}>
                        {entry.previousValue ?? "—"} → {entry.newValue ?? "—"}
                      </Typography>
                    )}
                  </TableCell>
                  <TableCell>{entry.performedByUserName ?? "System"}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Box>
  );
}
