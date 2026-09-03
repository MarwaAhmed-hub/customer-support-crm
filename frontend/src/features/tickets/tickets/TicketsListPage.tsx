import AddIcon from "@mui/icons-material/Add";
import PriorityHighIcon from "@mui/icons-material/PriorityHigh";
import SearchIcon from "@mui/icons-material/Search";
import {
  Alert,
  Box,
  Button,
  Card,
  Chip,
  CircularProgress,
  InputAdornment,
  MenuItem,
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
import { useEffect, useRef, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useAuth } from "../../auth/useAuth";
import * as categoriesApi from "../categories/categoriesApi";
import type { TicketCategory } from "../categories/types";
import * as prioritiesApi from "../priorities/prioritiesApi";
import type { TicketPriority } from "../priorities/types";
import * as ticketsApi from "./ticketsApi";
import type { TicketListItem } from "./types";
import { channelLabel, priorityChipColor, slaStatusChipColor, slaStatusLabel, statusChipColor, statusLabel, worstSlaStatus } from "./ticketDisplay";

const PAGE_SIZE = 20;
const SEARCH_DEBOUNCE_MS = 300;
const ALL_OPTION = "";

export function TicketsListPage() {
  const { hasPermission } = useAuth();
  const navigate = useNavigate();

  const [searchInput, setSearchInput] = useState("");
  const [search, setSearch] = useState("");
  const [categoryFilter, setCategoryFilter] = useState(ALL_OPTION);
  const [priorityFilter, setPriorityFilter] = useState(ALL_OPTION);
  // The escalated-queue filter — the missing link that lets a Manager pull up every currently
  // escalated ticket in one view instead of hunting for the red badge row by row.
  const [escalatedFilter, setEscalatedFilter] = useState(ALL_OPTION);
  // Story 23: the Unassigned Tickets Queue — every channel-created ticket lands here until an admin
  // classifies it into a business category (which may auto-assign it away).
  const [assignmentFilter, setAssignmentFilter] = useState(ALL_OPTION);
  const [page, setPage] = useState(1);

  const [items, setItems] = useState<TicketListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [categories, setCategories] = useState<TicketCategory[]>([]);
  const [priorities, setPriorities] = useState<TicketPriority[]>([]);

  // Filter dropdown options. Non-fatal on failure: worst case the filters have nothing to offer.
  useEffect(() => {
    let cancelled = false;
    Promise.all([categoriesApi.listTicketCategories(), prioritiesApi.listTicketPriorities()])
      .then(([fetchedCategories, fetchedPriorities]) => {
        if (cancelled) return;
        setCategories(fetchedCategories);
        setPriorities(fetchedPriorities);
      })
      .catch(() => undefined);

    return () => {
      cancelled = true;
    };
  }, []);

  // Debounce the raw input into the value that actually drives the fetch — same pattern as
  // UsersListPage, including the same first-render guard below.
  useEffect(() => {
    const handle = setTimeout(() => {
      setSearch((current) => {
        const next = searchInput.trim();
        return next === current ? current : next;
      });
    }, SEARCH_DEBOUNCE_MS);
    return () => clearTimeout(handle);
  }, [searchInput]);

  const isFirstRender = useRef(true);
  useEffect(() => {
    if (isFirstRender.current) {
      isFirstRender.current = false;
      return;
    }
    setPage(1);
  }, [search, categoryFilter, priorityFilter, escalatedFilter, assignmentFilter]);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    const params: ticketsApi.ListTicketsParams = { page, pageSize: PAGE_SIZE };
    if (search.length > 0) params.search = search;
    if (categoryFilter !== ALL_OPTION) params.categoryId = categoryFilter;
    if (priorityFilter !== ALL_OPTION) params.priorityId = priorityFilter;
    if (escalatedFilter !== ALL_OPTION) params.isEscalated = escalatedFilter === "escalated";
    if (assignmentFilter !== ALL_OPTION) params.unassignedOnly = assignmentFilter === "unassigned";

    ticketsApi
      .listTickets(params)
      .then((result) => {
        if (cancelled) return;
        setItems(result.items);
        setTotal(result.total);
      })
      .catch(() => {
        if (cancelled) return;
        setError("Could not load tickets. Please try again.");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [page, search, categoryFilter, priorityFilter, escalatedFilter, assignmentFilter]);

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));

  return (
    <Box sx={{ maxWidth: 1200 }}>
      <Box
        sx={{
          display: "flex",
          flexDirection: { xs: "column", sm: "row" },
          justifyContent: "space-between",
          alignItems: { sm: "center" },
          gap: 2,
          mb: 3,
        }}
      >
        <Box>
          <Typography variant="h4">Tickets</Typography>
          <Typography variant="body2" color="text.secondary">
            Track support tickets raised for customers.
          </Typography>
        </Box>
        {hasPermission("tickets.create") && (
          <Button component={Link} to="/tickets/new" variant="contained" startIcon={<AddIcon />}>
            New ticket
          </Button>
        )}
      </Box>

      <Card variant="outlined" sx={{ mb: 2, p: 2 }}>
        <Box sx={{ display: "flex", flexDirection: { xs: "column", sm: "row" }, gap: 2 }}>
          <TextField
            placeholder="Search by subject…"
            value={searchInput}
            onChange={(event) => setSearchInput(event.target.value)}
            size="small"
            fullWidth
            slotProps={{
              htmlInput: { "aria-label": "Search tickets" },
              input: {
                startAdornment: (
                  <InputAdornment position="start">
                    <SearchIcon fontSize="small" color="action" />
                  </InputAdornment>
                ),
              },
            }}
          />
          <TextField
            select
            label="Category"
            value={categoryFilter}
            onChange={(event) => setCategoryFilter(event.target.value)}
            size="small"
            sx={{ minWidth: 180 }}
            slotProps={{ select: { displayEmpty: true }, inputLabel: { shrink: true } }}
          >
            <MenuItem value={ALL_OPTION}>All categories</MenuItem>
            {categories.map((category) => (
              <MenuItem key={category.id} value={category.id}>
                {category.name}
              </MenuItem>
            ))}
          </TextField>
          <TextField
            select
            label="Priority"
            value={priorityFilter}
            onChange={(event) => setPriorityFilter(event.target.value)}
            size="small"
            sx={{ minWidth: 180 }}
            slotProps={{ select: { displayEmpty: true }, inputLabel: { shrink: true } }}
          >
            <MenuItem value={ALL_OPTION}>All priorities</MenuItem>
            {priorities.map((priority) => (
              <MenuItem key={priority.id} value={priority.id}>
                {priority.name}
              </MenuItem>
            ))}
          </TextField>
          <TextField
            select
            label="Escalation"
            value={escalatedFilter}
            onChange={(event) => setEscalatedFilter(event.target.value)}
            size="small"
            sx={{ minWidth: 180 }}
            slotProps={{ select: { displayEmpty: true }, inputLabel: { shrink: true } }}
          >
            <MenuItem value={ALL_OPTION}>All tickets</MenuItem>
            <MenuItem value="escalated">Escalated only</MenuItem>
          </TextField>
          <TextField
            select
            label="Assignment"
            value={assignmentFilter}
            onChange={(event) => setAssignmentFilter(event.target.value)}
            size="small"
            sx={{ minWidth: 180 }}
            slotProps={{ select: { displayEmpty: true }, inputLabel: { shrink: true } }}
          >
            <MenuItem value={ALL_OPTION}>All tickets</MenuItem>
            <MenuItem value="unassigned">Unassigned only</MenuItem>
          </TextField>
        </Box>
      </Card>

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
      ) : items.length === 0 ? (
        <Paper variant="outlined" sx={{ p: 4, textAlign: "center" }}>
          <Typography color="text.secondary">No tickets found.</Typography>
        </Paper>
      ) : (
        <TableContainer component={Paper} variant="outlined">
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>Subject</TableCell>
                <TableCell>Customer</TableCell>
                <TableCell>Category</TableCell>
                <TableCell>Priority</TableCell>
                <TableCell>Status</TableCell>
                <TableCell>SLA</TableCell>
                <TableCell>Assignee</TableCell>
                <TableCell>Created by</TableCell>
                <TableCell>Created</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {items.map((ticket) => (
                <TableRow key={ticket.id} hover onClick={() => navigate(`/tickets/${ticket.id}`)} sx={{ cursor: "pointer" }}>
                  <TableCell sx={{ fontWeight: 500 }}>
                    <Box sx={{ display: "flex", alignItems: "center", gap: 0.75 }}>
                      {ticket.isEscalated && (
                        <PriorityHighIcon fontSize="small" color="error" titleAccess="Escalated" />
                      )}
                      {ticket.subject}
                      {ticket.sourceChannel !== null && (
                        <Chip label={channelLabel(ticket.sourceChannel)} size="small" variant="outlined" sx={{ fontWeight: 500 }} />
                      )}
                    </Box>
                  </TableCell>
                  <TableCell>{ticket.customerName}</TableCell>
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
                    <Chip label={statusLabel(ticket.status)} size="small" color={statusChipColor(ticket.status)} />
                  </TableCell>
                  <TableCell>
                    {ticket.sla !== null ? (
                      <Chip
                        label={slaStatusLabel(worstSlaStatus(ticket.sla))}
                        size="small"
                        color={slaStatusChipColor(worstSlaStatus(ticket.sla))}
                      />
                    ) : (
                      <Typography component="span" variant="body2" color="text.secondary">
                        —
                      </Typography>
                    )}
                  </TableCell>
                  <TableCell>
                    {ticket.assignedUserName ?? (
                      <Typography component="span" variant="body2" color="text.secondary">
                        Unassigned
                      </Typography>
                    )}
                  </TableCell>
                  <TableCell>{ticket.createdByUserName ?? "—"}</TableCell>
                  <TableCell>{new Date(ticket.createdAt).toLocaleDateString()}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      <Box sx={{ display: "flex", alignItems: "center", justifyContent: "center", gap: 2, mt: 3 }}>
        <Button
          variant="outlined"
          size="small"
          disabled={page <= 1}
          onClick={() => setPage((current) => Math.max(1, current - 1))}
        >
          Previous
        </Button>
        <Typography variant="body2" color="text.secondary">
          Page {page} of {totalPages}
        </Typography>
        <Button
          variant="outlined"
          size="small"
          disabled={page >= totalPages}
          onClick={() => setPage((current) => current + 1)}
        >
          Next
        </Button>
      </Box>
    </Box>
  );
}
