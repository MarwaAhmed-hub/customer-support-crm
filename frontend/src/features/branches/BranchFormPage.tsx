import { Alert, Box, Button, CircularProgress, FormControlLabel, Paper, Switch, TextField, Typography } from "@mui/material";
import axios from "axios";
import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { useNavigate, useParams } from "react-router-dom";
import * as branchesApi from "./branchesApi";

const GENERIC_FAILURE = "Something went wrong. Please try again.";

interface FieldErrors {
  name?: string;
  code?: string;
}

/**
 * Serves both `/branches/new` (no `:id`) and `/branches/:id/edit` (`:id` present) — mode is read
 * from the route, matching `DepartmentFormPage`'s pattern.
 */
export function BranchFormPage() {
  const { id } = useParams<{ id: string }>();
  const isEdit = id !== undefined;
  const navigate = useNavigate();

  const [name, setName] = useState("");
  const [code, setCode] = useState("");
  const [isActive, setIsActive] = useState(true);

  const [loading, setLoading] = useState(isEdit);
  const [submitting, setSubmitting] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState<string | null>(null);

  useEffect(() => {
    if (!isEdit || id === undefined) return;

    let cancelled = false;
    branchesApi
      .getBranch(id)
      .then((branch) => {
        if (cancelled) return;
        setName(branch.name);
        setCode(branch.code ?? "");
        setIsActive(branch.isActive);
      })
      .catch(() => {
        if (!cancelled) setFormError("Could not load this branch. Please try again.");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [isEdit, id]);

  function validate(): FieldErrors {
    const errors: FieldErrors = {};

    const trimmedName = name.trim();
    if (trimmedName.length === 0) {
      errors.name = "Name is required.";
    } else if (trimmedName.length > 128) {
      errors.name = "Name must be 128 characters or fewer.";
    }

    if (code.trim().length > 32) {
      errors.code = "Code must be 32 characters or fewer.";
    }

    return errors;
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    if (submitting) return;

    const errors = validate();
    setFieldErrors(errors);
    if (Object.keys(errors).length > 0) return;

    setSubmitting(true);
    setFormError(null);

    const trimmedCode = code.trim();
    const codePayload = trimmedCode.length > 0 ? trimmedCode : null;

    try {
      if (isEdit && id !== undefined) {
        await branchesApi.updateBranch(id, { name: name.trim(), code: codePayload, isActive });
      } else {
        await branchesApi.createBranch({ name: name.trim(), code: codePayload });
      }
      navigate("/branches", { replace: true });
    } catch (caught: unknown) {
      if (axios.isAxiosError(caught) && caught.response?.status === 409) {
        const errorCode: unknown = caught.response.data?.error;
        if (errorCode === "duplicate_branch_code") {
          setFieldErrors((current) => ({ ...current, code: "A branch with this code already exists." }));
        } else {
          setFieldErrors((current) => ({ ...current, name: "A branch with this name already exists." }));
        }
      } else {
        setFormError(GENERIC_FAILURE);
      }
    } finally {
      setSubmitting(false);
    }
  }

  if (loading) {
    return (
      <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, py: 4 }}>
        <CircularProgress size={22} />
        <Typography color="text.secondary">Loading…</Typography>
      </Box>
    );
  }

  return (
    <Box sx={{ maxWidth: 480 }}>
      <Typography variant="h4" sx={{ mb: 3 }}>
        {isEdit ? "Edit branch" : "New branch"}
      </Typography>

      <Paper component="form" onSubmit={handleSubmit} noValidate variant="outlined" sx={{ p: 3 }}>
        {formError !== null && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {formError}
          </Alert>
        )}

        <TextField
          id="name"
          label="Name"
          value={name}
          onChange={(event) => setName(event.target.value)}
          error={fieldErrors.name !== undefined}
          helperText={fieldErrors.name}
          fullWidth
          margin="normal"
        />

        <TextField
          id="code"
          label="Code"
          value={code}
          onChange={(event) => setCode(event.target.value)}
          error={fieldErrors.code !== undefined}
          helperText={fieldErrors.code}
          fullWidth
          margin="normal"
        />

        {isEdit && (
          <FormControlLabel
            sx={{ mt: 1 }}
            control={<Switch checked={isActive} onChange={(event) => setIsActive(event.target.checked)} />}
            label="Active"
          />
        )}

        <Box sx={{ display: "flex", gap: 1.5, mt: 3 }}>
          <Button type="submit" variant="contained" disabled={submitting}>
            {submitting ? "Saving…" : "Save"}
          </Button>
          <Button variant="text" onClick={() => navigate("/branches")}>
            Cancel
          </Button>
        </Box>
      </Paper>
    </Box>
  );
}
