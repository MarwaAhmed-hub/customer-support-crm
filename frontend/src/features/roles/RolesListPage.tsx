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
import { Link } from "react-router-dom";
import { useAuth } from "../auth/useAuth";
import * as rolesApi from "./rolesApi";
import type { Role } from "./types";

export function RolesListPage() {
  const { hasPermission } = useAuth();

  const [roles, setRoles] = useState<Role[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    rolesApi
      .listRoles()
      .then((result) => {
        if (!cancelled) setRoles(result);
      })
      .catch(() => {
        if (!cancelled) setError("Could not load roles. Please try again.");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, []);

  return (
    <Box sx={{ maxWidth: 1100, mx: "auto" }}>
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
          <Typography variant="h4">Roles</Typography>
          <Typography variant="body2" color="text.secondary">
            Manage roles and the permissions granted to each one.
          </Typography>
        </Box>
        {hasPermission("roles.create") && (
          <Button component={Link} to="/roles/new" variant="contained" startIcon={<AddIcon />}>
            New role
          </Button>
        )}
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
      ) : roles.length === 0 ? (
        <Paper variant="outlined" sx={{ p: 4, textAlign: "center" }}>
          <Typography color="text.secondary">No roles found.</Typography>
        </Paper>
      ) : (
        <TableContainer component={Paper} variant="outlined">
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>Name</TableCell>
                <TableCell>Description</TableCell>
                <TableCell>Permissions Count</TableCell>
                <TableCell align="right">Actions</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {roles.map((role) => (
                <TableRow key={role.id} hover>
                  <TableCell>{role.name}</TableCell>
                  <TableCell>{role.description ?? "—"}</TableCell>
                  <TableCell>
                    <Chip
                      label={`${role.permissions.length} permission${role.permissions.length !== 1 ? "s" : ""}`}
                      size="small"
                      variant="outlined"
                      color={role.permissions.length > 0 ? "primary" : "default"}
                    />
                  </TableCell>
                  <TableCell align="right">
                    <Box sx={{ display: "flex", gap: 1, justifyContent: "flex-end" }}>
                      {hasPermission("roles.update") && (
                        <Button component={Link} to={`/roles/${role.id}/edit`} size="small">
                          Edit
                        </Button>
                      )}
                      {hasPermission("permissions.assign") && (
                        <Button component={Link} to={`/roles/${role.id}/permissions`} size="small">
                          Permissions
                        </Button>
                      )}
                    </Box>
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
