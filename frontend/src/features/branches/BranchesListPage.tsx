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
import { useAuth } from "../auth/useAuth";
import * as branchesApi from "./branchesApi";
import type { Branch } from "./types";

export function BranchesListPage() {
  const { hasPermission } = useAuth();

  const [branches, setBranches] = useState<Branch[]>([]);
  const [showInactive, setShowInactive] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    branchesApi
      .listBranches({ includeInactive: showInactive })
      .then((result) => {
        if (!cancelled) setBranches(result);
      })
      .catch(() => {
        if (!cancelled) setError("Could not load branches. Please try again.");
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
          <Typography variant="h4">Branches</Typography>
          <Typography variant="body2" color="text.secondary">
            Manage the branches users can be assigned to.
          </Typography>
        </Box>
        {hasPermission("branches.create") && (
          <Button component={Link} to="/branches/new" variant="contained" startIcon={<AddIcon />}>
            New branch
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
      ) : branches.length === 0 ? (
        <Paper variant="outlined" sx={{ p: 4, textAlign: "center" }}>
          <Typography color="text.secondary">No branches found.</Typography>
        </Paper>
      ) : (
        <TableContainer component={Paper} variant="outlined">
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>Name</TableCell>
                <TableCell>Code</TableCell>
                <TableCell>Status</TableCell>
                <TableCell>Updated</TableCell>
                <TableCell align="right">Actions</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {branches.map((branch) => (
                <TableRow key={branch.id} hover>
                  <TableCell>{branch.name}</TableCell>
                  <TableCell>{branch.code ?? "—"}</TableCell>
                  <TableCell>
                    <Chip
                      label={branch.isActive ? "Active" : "Inactive"}
                      color={branch.isActive ? "success" : "default"}
                      size="small"
                      variant={branch.isActive ? "filled" : "outlined"}
                    />
                  </TableCell>
                  <TableCell>{new Date(branch.updatedAt).toLocaleDateString()}</TableCell>
                  <TableCell align="right">
                    {hasPermission("branches.update") && (
                      <Button component={Link} to={`/branches/${branch.id}/edit`} size="small">
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
