import AddIcon from "@mui/icons-material/Add";
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
  Typography,
} from "@mui/material";
import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useAuth } from "../auth/useAuth";
import * as ticketsApi from "../tickets/tickets/ticketsApi";
import type { TicketListItem } from "../tickets/tickets/types";
import { priorityChipColor, statusChipColor } from "../tickets/tickets/ticketDisplay";

/**
 * The ticket list scoped to a single customer, embedded in `CustomerDetailPage`. Read-only here —
 * "Create ticket" hands off to `TicketFormPage` (`/tickets/new?customerId=`), which owns the actual
 * create form; this panel does not duplicate it. Does not touch Story 08 Interaction History — the
 * ticket-created interaction that create flow produces appears there automatically, on its own panel.
 */
export function CustomerTicketsPanel({ customerId }: { customerId: string }) {
  const { hasPermission } = useAuth();
  const navigate = useNavigate();
  const canCreate = hasPermission("tickets.create");

  const [tickets, setTickets] = useState<TicketListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    ticketsApi
      .listTickets({ customerId, pageSize: 20 })
      .then((result) => {
        if (!cancelled) setTickets(result.items);
      })
      .catch(() => {
        if (!cancelled) setError("Could not load tickets. Please try again.");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [customerId]);

  return (
    <Box>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1.5 }}>
        <Typography variant="subtitle1">Tickets</Typography>
        {canCreate && (
          <Button
            size="small"
            variant="outlined"
            startIcon={<AddIcon />}
            component={Link}
            to={`/tickets/new?customerId=${customerId}`}
          >
            Create ticket
          </Button>
        )}
      </Box>

      {error !== null && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      {loading ? (
        <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, py: 3 }}>
          <CircularProgress size={22} />
          <Typography color="text.secondary">Loading…</Typography>
        </Box>
      ) : tickets.length === 0 ? (
        <Paper variant="outlined" sx={{ p: 3, textAlign: "center" }}>
          <Typography color="text.secondary">No tickets raised for this customer yet.</Typography>
        </Paper>
      ) : (
        <TableContainer component={Paper} variant="outlined">
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Subject</TableCell>
                <TableCell>Category</TableCell>
                <TableCell>Priority</TableCell>
                <TableCell>Status</TableCell>
                <TableCell>Assignee</TableCell>
                <TableCell>Created</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {tickets.map((ticket) => (
                <TableRow key={ticket.id} hover onClick={() => navigate(`/tickets/${ticket.id}`)} sx={{ cursor: "pointer" }}>
                  <TableCell sx={{ fontWeight: 500 }}>{ticket.subject}</TableCell>
                  <TableCell>{ticket.categoryName}</TableCell>
                  <TableCell>
                    <Chip
                      label={ticket.priorityName}
                      size="small"
                      color={priorityChipColor(ticket.priorityName)}
                      sx={{ textTransform: "capitalize" }}
                    />
                  </TableCell>
                  <TableCell>
                    <Chip
                      label={ticket.status}
                      size="small"
                      color={statusChipColor(ticket.status)}
                      sx={{ textTransform: "capitalize" }}
                    />
                  </TableCell>
                  <TableCell>
                    {ticket.assignedUserName ?? (
                      <Typography component="span" variant="body2" color="text.secondary">
                        Unassigned
                      </Typography>
                    )}
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
