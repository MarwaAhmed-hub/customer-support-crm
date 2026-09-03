import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Checkbox,
  CircularProgress,
  Divider,
  FormControlLabel,
  FormGroup,
  Typography,
} from "@mui/material";
import axios from "axios";
import { useEffect, useState } from "react";
import { Link, useNavigate, useParams } from "react-router-dom";
import * as rolesApi from "./rolesApi";
import type { PermissionCategory, Role } from "./types";

export function RolePermissionsPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();

  const [role, setRole] = useState<Role | null>(null);
  const [categories, setCategories] = useState<PermissionCategory[]>([]);
  const [selected, setSelected] = useState<Set<string>>(new Set());

  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (id === undefined) return;

    let cancelled = false;
    setLoading(true);
    setError(null);

    // listEligiblePermissions (not listPermissions) so this page only ever renders the checkboxes a
    // role of this type is allowed to hold — e.g. Customer sees just `portal.access` and its three
    // `tickets.*` rows, never a `users.*`/`roles.*` row to hide. Administrator and custom roles still
    // get the full catalogue back from the same call; the backend decides the breadth.
    Promise.all([rolesApi.getRole(id), rolesApi.listEligiblePermissions(id)])
      .then(([fetchedRole, fetchedCategories]) => {
        if (cancelled) return;
        const eligibleCodes = new Set(fetchedCategories.flatMap((c) => c.permissions.map((p) => p.code)));
        setRole(fetchedRole);
        setCategories(fetchedCategories);
        // Intersect with what's actually rendered: a code the role held before this role's eligible
        // set narrowed (or before this feature existed) has no checkbox to represent it, so it must
        // not be silently re-submitted on the next Save.
        setSelected(new Set(fetchedRole.permissions.filter((code) => eligibleCodes.has(code))));
      })
      .catch(() => {
        if (!cancelled) setError("Could not load this role's permissions. Please try again.");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [id]);

  function toggle(code: string): void {
    setSelected((current) => {
      const next = new Set(current);
      if (next.has(code)) {
        next.delete(code);
      } else {
        next.add(code);
      }
      return next;
    });
  }

  async function handleSave(): Promise<void> {
    if (id === undefined || role === null || saving) return;

    setSaving(true);
    setError(null);

    try {
      const updated = await rolesApi.replaceRolePermissions(id, [...selected]);
      setRole(updated);
      setSelected(new Set(updated.permissions));
    } catch (caught: unknown) {
      const message = axios.isAxiosError(caught)
        ? "Could not save this role's permissions. Please try again."
        : "Something went wrong. Please try again.";
      setError(message);
    } finally {
      setSaving(false);
    }
  }

  const backLink = (
    <Button component={Link} to="/roles" startIcon={<ArrowBackIcon />} size="small" sx={{ mb: 2 }}>
      Back to roles
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

  if (role === null) {
    return (
      <Box sx={{ maxWidth: 600 }}>
        <Alert severity="error" sx={{ mb: 2 }}>
          {error ?? "Role not found."}
        </Alert>
        {backLink}
      </Box>
    );
  }

  // The Administrator role always has every permission — replacing its set is rejected by the
  // backend (409), so the picker is read-only rather than letting the caller submit and fail.
  const readOnly = role.isSystem && role.name === "Administrator";

  return (
    <Box sx={{ maxWidth: 720 }}>
      {backLink}

      <Card variant="outlined">
        <CardContent>
          <Typography variant="h4" component="h1" sx={{ mb: 0.5 }}>
            {role.name}
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            Choose which permissions this role grants.
          </Typography>

          {error !== null && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {error}
            </Alert>
          )}

          {readOnly && (
            <Alert severity="info" sx={{ mb: 2 }}>
              The Administrator role always has every permission and cannot be changed.
            </Alert>
          )}

          {categories.map((category, index) => (
            <Box key={category.category} sx={{ mb: 2 }}>
              {index > 0 && <Divider sx={{ mb: 2 }} />}
              <Typography variant="subtitle2" sx={{ mb: 1, textTransform: "capitalize" }}>
                {category.category}
              </Typography>
              <FormGroup>
                {category.permissions.map((permission) => (
                  <FormControlLabel
                    key={permission.code}
                    control={
                      <Checkbox
                        checked={readOnly || selected.has(permission.code)}
                        disabled={readOnly}
                        onChange={() => toggle(permission.code)}
                      />
                    }
                    label={permission.displayName}
                  />
                ))}
              </FormGroup>
            </Box>
          ))}

          <Divider sx={{ my: 2 }} />

          <Box sx={{ display: "flex", gap: 1.5 }}>
            <Button
              variant="contained"
              disabled={readOnly || saving}
              onClick={() => void handleSave()}
            >
              {saving ? "Saving…" : "Save"}
            </Button>
            <Button variant="text" onClick={() => navigate("/roles")}>
              Cancel
            </Button>
          </Box>
        </CardContent>
      </Card>
    </Box>
  );
}
