import AddIcon from "@mui/icons-material/Add";
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  FormControlLabel,
  Paper,
  Switch,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from "@mui/material";
import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../../auth/useAuth";
import * as prioritiesApi from "./prioritiesApi";
import type { TicketPriority } from "./types";

export function TicketPrioritiesListPage() {
  const { hasPermission } = useAuth();

  const [priorities, setPriorities] = useState<TicketPriority[]>([]);
  const [showInactive, setShowInactive] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    prioritiesApi
      .listTicketPriorities({ includeInactive: showInactive })
      .then((result) => {
        if (!cancelled) setPriorities(result);
      })
      .catch(() => {
        if (!cancelled) setError("Could not load ticket priorities. Please try again.");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [showInactive]);

  return (
    <Box sx={{ maxWidth: 900 }}>
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
          <Typography variant="h4">Ticket Priorities</Typography>
          <Typography variant="body2" color="text.secondary">
            Manage the urgency levels tickets can be assigned, in their natural order.
          </Typography>
        </Box>
        {hasPermission("tickets.priorities.manage") && (
          <Button component={Link} to="/tickets/priorities/new" variant="contained" startIcon={<AddIcon />}>
            New priority
          </Button>
        )}
      </Box>

      <FormControlLabel
        sx={{ mb: 2 }}
        control={<Switch checked={showInactive} onChange={(event) => setShowInactive(event.target.checked)} />}
        label="Show inactive"
      />

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
      ) : priorities.length === 0 ? (
        <Paper variant="outlined" sx={{ p: 4, textAlign: "center" }}>
          <Typography color="text.secondary">No ticket priorities found.</Typography>
        </Paper>
      ) : (
        <TableContainer component={Paper} variant="outlined">
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>Sort</TableCell>
                <TableCell>Name</TableCell>
                <TableCell>Description</TableCell>
                <TableCell>Status</TableCell>
                <TableCell>Updated</TableCell>
                <TableCell align="right">Actions</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {/* Ordering is guaranteed server-side (SortOrder ascending, then Name); this list must
                  not re-sort or hide any entries. */}
              {priorities.map((priority) => (
                <TableRow key={priority.id} hover>
                  <TableCell>{priority.sortOrder}</TableCell>
                  <TableCell>{priority.name}</TableCell>
                  <TableCell>{priority.description ?? "—"}</TableCell>
                  <TableCell>
                    <Chip
                      label={priority.isActive ? "Active" : "Inactive"}
                      color={priority.isActive ? "success" : "default"}
                      size="small"
                      variant={priority.isActive ? "filled" : "outlined"}
                    />
                  </TableCell>
                  <TableCell>{new Date(priority.updatedAt).toLocaleDateString()}</TableCell>
                  <TableCell align="right">
                    {hasPermission("tickets.priorities.manage") && (
                      <Button component={Link} to={`/tickets/priorities/${priority.id}/edit`} size="small">
                        Edit
                      </Button>
                    )}
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Box>
  );
}
