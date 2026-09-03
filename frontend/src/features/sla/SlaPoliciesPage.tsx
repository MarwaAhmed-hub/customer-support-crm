import { Alert, Box, Button, CircularProgress, Paper, Snackbar, Switch, Table, TableBody, TableCell, TableContainer, TableHead, TableRow, TextField, Typography } from "@mui/material";
import axios from "axios";
import { useEffect, useState } from "react";
import { useAuth } from "../auth/useAuth";
import * as slaPoliciesApi from "./slaPoliciesApi";
import type { SlaPolicy } from "./types";

const GENERIC_FAILURE = "Something went wrong. Please try again.";

interface RowState {
  firstResponseMinutes: string;
  resolutionMinutes: string;
  isActive: boolean;
  saving: boolean;
  error: string | null;
}

function toRowState(policy: SlaPolicy): RowState {
  return {
    firstResponseMinutes: String(policy.firstResponseMinutes),
    resolutionMinutes: String(policy.resolutionMinutes),
    isActive: policy.isActive,
    saving: false,
    error: null,
  };
}

function isDirty(row: RowState, policy: SlaPolicy): boolean {
  return (
    row.firstResponseMinutes !== String(policy.firstResponseMinutes) ||
    row.resolutionMinutes !== String(policy.resolutionMinutes) ||
    row.isActive !== policy.isActive
  );
}

/**
 * Story 22: minimal admin surface over the SLA policies `ISlaService` applies at ticket creation —
 * list + edit only (`FirstResponseMinutes`/`ResolutionMinutes`/`IsActive`). No create/delete: the
 * seeded "Default SLA" row is the only one on a fresh install, and this story doesn't add priority-
 * specific policies — see `backend/src/CustomerSupportCrm.Api/Sla/SlaPoliciesController.cs`.
 */
export function SlaPoliciesPage() {
  const { hasPermission } = useAuth();
  const canEdit = hasPermission("system.update");

  const [policies, setPolicies] = useState<SlaPolicy[]>([]);
  const [rows, setRows] = useState<Record<string, RowState>>({});
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [savedOpen, setSavedOpen] = useState(false);

  useEffect(() => {
    let cancelled = false;
    slaPoliciesApi
      .listSlaPolicies()
      .then((result) => {
        if (cancelled) return;
        setPolicies(result);
        setRows(Object.fromEntries(result.map((policy) => [policy.id, toRowState(policy)])));
      })
      .catch(() => {
        if (!cancelled) setLoadError("Could not load SLA policies. Please try again.");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, []);

  function updateRow(id: string, patch: Partial<RowState>): void {
    setRows((current) => ({ ...current, [id]: { ...current[id]!, ...patch } }));
  }

  async function handleSave(policy: SlaPolicy): Promise<void> {
    const row = rows[policy.id];
    if (row === undefined || row.saving) return;

    const firstResponseMinutes = Number(row.firstResponseMinutes);
    const resolutionMinutes = Number(row.resolutionMinutes);
    if (!Number.isInteger(firstResponseMinutes) || firstResponseMinutes < 1) {
      updateRow(policy.id, { error: "First Response must be a whole number of minutes, at least 1." });
      return;
    }
    if (!Number.isInteger(resolutionMinutes) || resolutionMinutes < 1) {
      updateRow(policy.id, { error: "Resolution must be a whole number of minutes, at least 1." });
      return;
    }

    updateRow(policy.id, { saving: true, error: null });

    try {
      const updated = await slaPoliciesApi.updateSlaPolicy(policy.id, {
        firstResponseMinutes,
        resolutionMinutes,
        isActive: row.isActive,
      });
      setPolicies((current) => current.map((p) => (p.id === updated.id ? updated : p)));
      setRows((current) => ({ ...current, [updated.id]: toRowState(updated) }));
      setSavedOpen(true);
    } catch (caught: unknown) {
      if (axios.isAxiosError(caught) && caught.response?.status === 409) {
        updateRow(policy.id, { error: "Another policy is already active for this priority." });
      } else if (axios.isAxiosError(caught) && caught.response?.status === 400) {
        updateRow(policy.id, { error: "One of these values was rejected. Please check them and try again." });
      } else {
        updateRow(policy.id, { error: GENERIC_FAILURE });
      }
    } finally {
      updateRow(policy.id, { saving: false });
    }
  }

  return (
    <Box sx={{ maxWidth: 900 }}>
      <Typography variant="h4" sx={{ mb: 1 }}>
        SLA Policies
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
        First Response and Resolution targets applied to every new ticket. A ticket already in
        progress keeps the due times it started with — editing a policy here only affects tickets
        created afterward.
      </Typography>

      {loadError !== null && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {loadError}
        </Alert>
      )}

      {loading ? (
        <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, py: 4 }}>
          <CircularProgress size={22} />
          <Typography color="text.secondary">Loading…</Typography>
        </Box>
      ) : policies.length === 0 ? (
        <Paper variant="outlined" sx={{ p: 4, textAlign: "center" }}>
          <Typography color="text.secondary">No SLA policies found.</Typography>
        </Paper>
      ) : (
        <TableContainer component={Paper} variant="outlined">
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>Name</TableCell>
                <TableCell>Priority</TableCell>
                <TableCell>First Response (minutes)</TableCell>
                <TableCell>Resolution (minutes)</TableCell>
                <TableCell>Active</TableCell>
                {canEdit && <TableCell />}
              </TableRow>
            </TableHead>
            <TableBody>
              {policies.map((policy) => {
                const row = rows[policy.id];
                if (row === undefined) return null;
                const dirty = isDirty(row, policy);
                return (
                  <TableRow key={policy.id}>
                    <TableCell sx={{ fontWeight: 500 }}>{policy.name}</TableCell>
                    <TableCell>{policy.priorityName ?? "Default (every priority)"}</TableCell>
                    <TableCell>
                      {canEdit ? (
                        <TextField
                          value={row.firstResponseMinutes}
                          onChange={(event) => updateRow(policy.id, { firstResponseMinutes: event.target.value, error: null })}
                          size="small"
                          sx={{ width: 110 }}
                          slotProps={{ htmlInput: { inputMode: "numeric" } }}
                        />
                      ) : (
                        policy.firstResponseMinutes
                      )}
                    </TableCell>
                    <TableCell>
                      {canEdit ? (
                        <TextField
                          value={row.resolutionMinutes}
                          onChange={(event) => updateRow(policy.id, { resolutionMinutes: event.target.value, error: null })}
                          size="small"
                          sx={{ width: 110 }}
                          slotProps={{ htmlInput: { inputMode: "numeric" } }}
                        />
                      ) : (
                        policy.resolutionMinutes
                      )}
                    </TableCell>
                    <TableCell>
                      {canEdit ? (
                        <Switch
                          checked={row.isActive}
                          onChange={(event) => updateRow(policy.id, { isActive: event.target.checked, error: null })}
                        />
                      ) : (
                        <Typography variant="body2" color={policy.isActive ? "success.main" : "text.secondary"}>
                          {policy.isActive ? "Active" : "Inactive"}
                        </Typography>
                      )}
                    </TableCell>
                    {canEdit && (
                      <TableCell>
                        <Box sx={{ display: "flex", flexDirection: "column", alignItems: "flex-end", gap: 0.5 }}>
                          <Button
                            variant="contained"
                            size="small"
                            disabled={!dirty || row.saving}
                            onClick={() => void handleSave(policy)}
                          >
                            {row.saving ? "Saving…" : "Save"}
                          </Button>
                          {row.error !== null && (
                            <Typography variant="caption" color="error">
                              {row.error}
                            </Typography>
                          )}
                        </Box>
                      </TableCell>
                    )}
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      <Snackbar
        open={savedOpen}
        autoHideDuration={3000}
        onClose={() => setSavedOpen(false)}
        anchorOrigin={{ vertical: "bottom", horizontal: "center" }}
      >
        <Alert severity="success" onClose={() => setSavedOpen(false)} sx={{ width: "100%" }}>
          Policy saved.
        </Alert>
      </Snackbar>
    </Box>
  );
}
