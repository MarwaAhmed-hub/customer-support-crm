import { Alert, Box, Chip, CircularProgress, Paper, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, Typography } from "@mui/material";
import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../../auth/useAuth";
import { priorityChipColor, statusChipColor, statusLabel } from "../../tickets/tickets/ticketDisplay";
import type { TicketListItem } from "../../tickets/tickets/types";
import * as agentDashboardApi from "./agentDashboardApi";

/** Read-only "my tickets" view (Story 15) — no create/edit/delete affordances, mirrors TicketsListPage's loading/empty/error pattern. */
export function AgentDashboardPage() {
  const { user } = useAuth();

  const [tickets, setTickets] = useState<TicketListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    // PermissionRoute already guarantees an authenticated caller before this page renders, so `user`
    // should never be null here in practice — this guard is defensive only, and skips the API call
    // rather than firing a request with nothing to scope it to.
    if (user === null) return;

    let cancelled = false;
    setLoading(true);
    setError(null);

    agentDashboardApi
      .fetchMyAssignedTickets()
      .then((result) => {
        if (!cancelled) setTickets(result.items);
      })
      .catch(() => {
        if (!cancelled) setError("Could not load your assigned tickets. Please try again.");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [user]);

  if (user === null) {
    return null;
  }

  return (
    <Box sx={{ maxWidth: 1200 }}>
      <Box sx={{ mb: 3 }}>
        <Typography variant="h4">Agent Dashboard</Typography>
        <Typography variant="body2" color="text.secondary">
          Tickets currently assigned to you.
        </Typography>
      </Box>

      {error !== null && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      {loading ? (
        <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, py: 4 }}>
          <CircularProgress size={22} />
          <Typography color="text.secondary">Loading…</Typography>
        </Box>
      ) : error !== null ? null : tickets.length === 0 ? (
        <Paper variant="outlined" sx={{ p: 4, textAlign: "center" }}>
          <Typography color="text.secondary">You have no assigned tickets.</Typography>
        </Paper>
      ) : (
        <TableContainer component={Paper} variant="outlined">
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>Subject</TableCell>
                <TableCell>Status</TableCell>
                <TableCell>Priority</TableCell>
                <TableCell>Category</TableCell>
                <TableCell>Customer</TableCell>
                <TableCell>Created</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {tickets.map((ticket) => (
                <TableRow key={ticket.id} hover>
                  <TableCell sx={{ fontWeight: 500 }}>
                    <Link to={`/tickets/${ticket.id}`}>{ticket.subject}</Link>
                  </TableCell>
                  <TableCell>
                    <Chip label={statusLabel(ticket.status)} size="small" color={statusChipColor(ticket.status)} />
                  </TableCell>
                  <TableCell>
                    <Chip
                      label={ticket.priorityName}
                      size="small"
                      color={priorityChipColor(ticket.priorityName)}
                      sx={{ textTransform: "capitalize" }}
                    />
                  </TableCell>
                  <TableCell>{ticket.categoryName}</TableCell>
                  <TableCell>
                    <Link to={`/customers/${ticket.customerId}`}>{ticket.customerName}</Link>
                  </TableCell>
                  <TableCell>{new Date(ticket.createdAt).toLocaleDateString()}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Box>
  );
}
