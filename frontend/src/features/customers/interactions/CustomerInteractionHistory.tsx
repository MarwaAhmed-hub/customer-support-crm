import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tooltip,
  Typography,
} from "@mui/material";
import { useEffect, useState } from "react";
import * as interactionsApi from "./interactionsApi";
import type { CustomerInteraction } from "./types";

const PAGE_SIZE = 25;

/** Same date format as AuditLogsPage — converts the UTC `occurredAt` to the viewer's local time. */
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
 * Presentational + data-loading section for a customer's interaction history. Mounted from
 * `CustomerDetailPage` (the customer's full history) and from `TicketDetailPage` (narrowed to one
 * ticket via `ticketId`) — both gated by the `customers.interactions.read` permission check; this
 * component assumes it is only rendered when the caller may call the endpoint.
 */
export function CustomerInteractionHistory({ customerId, ticketId }: { customerId: string; ticketId?: string }) {
  const [interactions, setInteractions] = useState<CustomerInteraction[]>([]);
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [reloadToken, setReloadToken] = useState(0);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    interactionsApi
      .listCustomerInteractions(customerId, ticketId === undefined ? { page, pageSize: PAGE_SIZE } : { page, pageSize: PAGE_SIZE, ticketId })
      .then((result) => {
        if (cancelled) return;
        setInteractions(result.items);
        setTotal(result.total);
      })
      .catch(() => {
        if (!cancelled) setError("Could not load interaction history. Please try again.");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [customerId, ticketId, page, reloadToken]);

  return (
    <Box>
      <Typography variant="subtitle1" sx={{ mb: 1.5 }}>
        Interaction History
      </Typography>

      {error !== null && (
        <Alert
          severity="error"
          sx={{ mb: 2 }}
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
      ) : error !== null ? null : interactions.length === 0 ? (
        <Paper variant="outlined" sx={{ p: 3, textAlign: "center" }}>
          <Typography color="text.secondary">
            {ticketId === undefined ? "No interactions recorded for this customer yet." : "No interactions recorded for this ticket yet."}
          </Typography>
        </Paper>
      ) : (
        <>
          <TableContainer component={Paper} variant="outlined">
            <Table size="small">
              <TableHead>
                <TableRow>
                  <TableCell>Date</TableCell>
                  <TableCell>Type</TableCell>
                  <TableCell>Summary</TableCell>
                  <TableCell>Agent</TableCell>
                </TableRow>
              </TableHead>
              <TableBody>
                {interactions.map((interaction) => (
                  <TableRow key={interaction.id} hover>
                    <TableCell sx={{ whiteSpace: "nowrap" }}>{formatDate(interaction.occurredAt)}</TableCell>
                    <TableCell>
                      <Chip label={interaction.interactionType} size="small" variant="outlined" />
                    </TableCell>
                    <TableCell sx={{ maxWidth: 400 }}>
                      {interaction.details !== null && interaction.details.length > 0 ? (
                        <Tooltip title={interaction.details}>
                          <Typography variant="body2" sx={{ cursor: "default" }}>
                            {interaction.summary ?? "—"}
                          </Typography>
                        </Tooltip>
                      ) : (
                        <Typography variant="body2">{interaction.summary ?? "—"}</Typography>
                      )}
                    </TableCell>
                    <TableCell>{interaction.userDisplayName ?? "System"}</TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          </TableContainer>

          <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mt: 2 }}>
            <Typography variant="caption" color="text.secondary">
              Showing {(page - 1) * PAGE_SIZE + 1} to {Math.min(page * PAGE_SIZE, total)} of {total}
            </Typography>
            <Box sx={{ display: "flex", gap: 1 }}>
              <Button
                size="small"
                variant="outlined"
                disabled={page === 1}
                onClick={() => setPage((current) => Math.max(1, current - 1))}
              >
                Previous
              </Button>
              <Button
                size="small"
                variant="outlined"
                disabled={page * PAGE_SIZE >= total}
                onClick={() => setPage((current) => (current * PAGE_SIZE < total ? current + 1 : current))}
              >
                Next
              </Button>
            </Box>
          </Box>
        </>
      )}
    </Box>
  );
}
