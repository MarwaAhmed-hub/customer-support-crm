import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  Divider,
  MenuItem,
  Stack,
  TextField,
  Typography,
} from "@mui/material";
import axios from "axios";
import { useEffect, useState } from "react";
import type { ReactNode } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import { useAuth } from "../auth/useAuth";
import * as rolesApi from "../roles/rolesApi";
import type { Role } from "../roles/types";
import * as usersApi from "./usersApi";
import type { UserDetail } from "./types";

function Field({ label, value }: { label: string; value: ReactNode }) {
  return (
    <Box>
      <Typography variant="caption" color="text.secondary">
        {label}
      </Typography>
      <Typography variant="body1">{value}</Typography>
    </Box>
  );
}

export function UserDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { hasPermission } = useAuth();
  const canAssignRoles = hasPermission("permissions.assign");

  const [user, setUser] = useState<UserDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [notFound, setNotFound] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [allRoles, setAllRoles] = useState<Role[]>([]);
  const [selectedRoleId, setSelectedRoleId] = useState("");
  const [roleActionPending, setRoleActionPending] = useState(false);

  useEffect(() => {
    if (id === undefined) return;

    let cancelled = false;
    setLoading(true);
    setNotFound(false);
    setError(null);

    usersApi
      .getUser(id)
      .then((current) => {
        if (!cancelled) setUser(current);
      })
      .catch((caught: unknown) => {
        if (cancelled) return;
        if (axios.isAxiosError(caught) && caught.response?.status === 404) {
          setNotFound(true);
        } else {
          setError("Could not load this user. Please try again.");
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [id]);

  // Only fetched for callers who can actually assign roles — no point loading the role catalogue
  // for a viewer who cannot act on it.
  useEffect(() => {
    if (!canAssignRoles) return;

    let cancelled = false;
    rolesApi
      .listRoles()
      .then((roles) => {
        if (!cancelled) setAllRoles(roles);
      })
      .catch(() => {
        // Non-fatal: the assign control simply has nothing to offer.
      });

    return () => {
      cancelled = true;
    };
  }, [canAssignRoles]);

  async function toggleActive(): Promise<void> {
    if (user === null) return;
    try {
      const updated = await usersApi.setUserActive(user.id, !user.isActive);
      setUser(updated);
    } catch {
      setError("Could not update this user's status. Please try again.");
    }
  }

  async function assignRole(): Promise<void> {
    if (user === null || selectedRoleId === "" || roleActionPending) return;

    setRoleActionPending(true);
    setError(null);

    try {
      const roles = await rolesApi.assignRoleToUser(user.id, selectedRoleId);
      setUser({ ...user, roles });
      setSelectedRoleId("");
    } catch {
      setError("Could not assign this role. Please try again.");
    } finally {
      setRoleActionPending(false);
    }
  }

  async function removeRole(roleId: string): Promise<void> {
    if (user === null || roleActionPending) return;

    setRoleActionPending(true);
    setError(null);

    try {
      const roles = await rolesApi.removeRoleFromUser(user.id, roleId);
      setUser({ ...user, roles });
    } catch (caught: unknown) {
      if (axios.isAxiosError(caught) && caught.response?.data?.error === "last_administrator") {
        setError("This is the last administrator — assign Administrator to another user first.");
      } else {
        setError("Could not remove this role. Please try again.");
      }
    } finally {
      setRoleActionPending(false);
    }
  }

  const backLink = (
    <Button component={Link} to="/users" startIcon={<ArrowBackIcon />} size="small" sx={{ mb: 2 }}>
      Back to users
    </Button>
  );

  if (loading) {
    return (
      <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, py: 4 }}>
        <CircularProgress size={22} />
        <Typography color="text.secondary">Loading…</Typography>
      </Box>
    );
  }

  if (notFound) {
    return (
      <Box sx={{ maxWidth: 600 }}>
        <Alert severity="warning" sx={{ mb: 2 }}>
          User not found.
        </Alert>
        {backLink}
      </Box>
    );
  }

  // A non-404 failure also leaves `user` at null (the fetch never populated it) — checked after
  // `notFound` specifically so a generic error doesn't fall into the "not found" branch above.
  if (error !== null && user === null) {
    return (
      <Box sx={{ maxWidth: 600 }}>
        <Alert severity="error">{error}</Alert>
      </Box>
    );
  }

  if (user === null) {
    // Defensive fallback; not reachable once loading/notFound/error are exhausted.
    return null;
  }

  const assignableRoles = allRoles.filter((role) => !user.roles.some((assigned) => assigned.id === role.id));

  return (
    <Box sx={{ maxWidth: 600 }}>
      {backLink}

      <Card variant="outlined">
        <CardContent>
          <Box sx={{ display: "flex", alignItems: "center", justifyContent: "space-between", mb: 2 }}>
            <Typography variant="h4" component="h1">
              {user.displayName}
            </Typography>
            <Chip
              label={user.isActive ? "Active" : "Inactive"}
              color={user.isActive ? "success" : "default"}
              variant={user.isActive ? "filled" : "outlined"}
            />
          </Box>

          {error !== null && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {error}
            </Alert>
          )}

          <Box sx={{ display: "flex", flexDirection: "column", gap: 2, mb: 3 }}>
            <Field label="Email" value={user.email} />
            <Field label="Department" value={user.departmentName ?? "—"} />
            <Field label="Branch" value={user.branchName ?? "—"} />
            <Field label="Created" value={new Date(user.createdAt).toLocaleString()} />
          </Box>

          <Divider sx={{ mb: 2 }} />

          <Box sx={{ display: "flex", gap: 1.5, mb: 3 }}>
            <Button variant="outlined" onClick={() => navigate(`/users/${user.id}/edit`)}>
              Edit
            </Button>
            <Button
              variant="outlined"
              color={user.isActive ? "error" : "success"}
              onClick={() => void toggleActive()}
            >
              {user.isActive ? "Deactivate" : "Activate"}
            </Button>
          </Box>

          <Divider sx={{ mb: 2 }} />

          <Typography variant="subtitle1" sx={{ mb: 1.5 }}>
            Roles
          </Typography>

          {user.roles.length === 0 ? (
            <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
              No roles assigned.
            </Typography>
          ) : (
            <Stack direction="row" spacing={1} sx={{ mb: 2, flexWrap: "wrap", gap: 1 }}>
              {user.roles.map((role) => (
                <Chip
                  key={role.id}
                  label={role.name}
                  onDelete={canAssignRoles ? () => void removeRole(role.id) : undefined}
                  disabled={roleActionPending}
                />
              ))}
            </Stack>
          )}

          {canAssignRoles && (
            <Box sx={{ display: "flex", gap: 1.5, alignItems: "center" }}>
              <TextField
                select
                label="Assign role"
                size="small"
                value={selectedRoleId}
                onChange={(event) => setSelectedRoleId(event.target.value)}
                disabled={assignableRoles.length === 0}
                sx={{ minWidth: 220 }}
              >
                {assignableRoles.map((role) => (
                  <MenuItem key={role.id} value={role.id}>
                    {role.name}
                  </MenuItem>
                ))}
              </TextField>
              <Button
                variant="outlined"
                disabled={selectedRoleId === "" || roleActionPending}
                onClick={() => void assignRole()}
              >
                Assign
              </Button>
            </Box>
          )}
        </CardContent>
      </Card>
    </Box>
  );
}
