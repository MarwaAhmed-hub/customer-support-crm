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
import * as departmentsApi from "./departmentsApi";
import type { Department } from "./types";

export function DepartmentsListPage() {
  const { hasPermission } = useAuth();

  const [departments, setDepartments] = useState<Department[]>([]);
  const [showInactive, setShowInactive] = useState(false);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    departmentsApi
      .listDepartments({ includeInactive: showInactive })
      .then((result) => {
        if (!cancelled) setDepartments(result);
      })
      .catch(() => {
        if (!cancelled) setError("Could not load departments. Please try again.");
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
          <Typography variant="h4">Departments</Typography>
          <Typography variant="body2" color="text.secondary">
            Manage the departments users can be assigned to.
          </Typography>
        </Box>
        {hasPermission("departments.create") && (
          <Button component={Link} to="/departments/new" variant="contained" startIcon={<AddIcon />}>
            New department
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
      ) : departments.length === 0 ? (
        <Paper variant="outlined" sx={{ p: 4, textAlign: "center" }}>
          <Typography color="text.secondary">No departments found.</Typography>
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
              {departments.map((department) => (
                <TableRow key={department.id} hover>
                  <TableCell>{department.name}</TableCell>
                  <TableCell>{department.code ?? "—"}</TableCell>
                  <TableCell>
                    <Chip
                      label={department.isActive ? "Active" : "Inactive"}
                      color={department.isActive ? "success" : "default"}
                      size="small"
                      variant={department.isActive ? "filled" : "outlined"}
                    />
                  </TableCell>
                  <TableCell>{new Date(department.updatedAt).toLocaleDateString()}</TableCell>
                  <TableCell align="right">
                    {hasPermission("departments.update") && (
                      <Button component={Link} to={`/departments/${department.id}/edit`} size="small">
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
