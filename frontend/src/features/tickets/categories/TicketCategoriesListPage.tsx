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
import * as categoriesApi from "./categoriesApi";
import type { TicketCategory } from "./types";

export function TicketCategoriesListPage() {
  const { hasPermission } = useAuth();

  const [categories, setCategories] = useState<TicketCategory[]>([]);
  const [showInactive, setShowInactive] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    categoriesApi
      .listTicketCategories({ includeInactive: showInactive })
      .then((result) => {
        if (!cancelled) setCategories(result);
      })
      .catch(() => {
        if (!cancelled) setError("Could not load ticket categories. Please try again.");
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
          <Typography variant="h4">Ticket Categories</Typography>
          <Typography variant="body2" color="text.secondary">
            Manage the categories tickets can be classified under.
          </Typography>
        </Box>
        {hasPermission("tickets.categories.manage") && (
          <Button component={Link} to="/tickets/categories/new" variant="contained" startIcon={<AddIcon />}>
            New category
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
      ) : categories.length === 0 ? (
        <Paper variant="outlined" sx={{ p: 4, textAlign: "center" }}>
          <Typography color="text.secondary">No ticket categories found.</Typography>
        </Paper>
      ) : (
        <TableContainer component={Paper} variant="outlined">
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>Name</TableCell>
                <TableCell>Description</TableCell>
                <TableCell>Department</TableCell>
                <TableCell>Status</TableCell>
                <TableCell>Updated</TableCell>
                <TableCell align="right">Actions</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {categories.map((category) => (
                <TableRow key={category.id} hover>
                  <TableCell>{category.name}</TableCell>
                  <TableCell>{category.description ?? "—"}</TableCell>
                  <TableCell>{category.departmentName ?? "—"}</TableCell>
                  <TableCell>
                    <Chip
                      label={category.isActive ? "Active" : "Inactive"}
                      color={category.isActive ? "success" : "default"}
                      size="small"
                      variant={category.isActive ? "filled" : "outlined"}
                    />
                  </TableCell>
                  <TableCell>{new Date(category.updatedAt).toLocaleDateString()}</TableCell>
                  <TableCell align="right">
                    {hasPermission("tickets.categories.manage") && (
                      <Button component={Link} to={`/tickets/categories/${category.id}/edit`} size="small">
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
