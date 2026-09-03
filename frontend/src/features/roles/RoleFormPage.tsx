import { Alert, Box, Button, CircularProgress, Paper, TextField, Typography } from "@mui/material";
import axios from "axios";
import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { useNavigate, useParams } from "react-router-dom";
import * as rolesApi from "./rolesApi";

const GENERIC_FAILURE = "Something went wrong. Please try again.";

interface FieldErrors {
  name?: string;
}

/**
 * Serves both `/roles/new` (no `:id`) and `/roles/:id/edit` (`:id` present) — mode is read from the
 * route, matching `UserFormPage`'s pattern.
 */
export function RoleFormPage() {
  const { id } = useParams<{ id: string }>();
  const isEdit = id !== undefined;
  const navigate = useNavigate();

  const [name, setName] = useState("");
  const [description, setDescription] = useState("");

  const [loading, setLoading] = useState(isEdit);
  const [submitting, setSubmitting] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState<string | null>(null);

  useEffect(() => {
    if (!isEdit || id === undefined) return;

    let cancelled = false;
    rolesApi
      .getRole(id)
      .then((role) => {
        if (cancelled) return;
        setName(role.name);
        setDescription(role.description ?? "");
      })
      .catch(() => {
        if (!cancelled) setFormError("Could not load this role. Please try again.");
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

    const trimmedDescription = description.trim();
    const payload = { name: name.trim(), description: trimmedDescription.length > 0 ? trimmedDescription : null };

    try {
      if (isEdit && id !== undefined) {
        await rolesApi.updateRole(id, payload);
        navigate("/roles", { replace: true });
      } else {
        await rolesApi.createRole(payload);
        navigate("/roles", { replace: true });
      }
    } catch (caught: unknown) {
      if (axios.isAxiosError(caught) && caught.response?.status === 409) {
        const errorCode: unknown = caught.response.data?.error;
        setFormError(
          errorCode === "administrator_role_protected"
            ? "The Administrator role cannot be renamed."
            : "A role with this name already exists.",
        );
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
        {isEdit ? "Edit role" : "New role"}
      </Typography>

      <Paper
        component="form"
        onSubmit={handleSubmit}
        noValidate
        variant="outlined"
        sx={{ p: 3 }}
      >
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
          id="description"
          label="Description"
          value={description}
          onChange={(event) => setDescription(event.target.value)}
          fullWidth
          multiline
          minRows={2}
          margin="normal"
        />

        <Box sx={{ display: "flex", gap: 1.5, mt: 3 }}>
          <Button type="submit" variant="contained" disabled={submitting}>
            {submitting ? "Saving…" : "Save"}
          </Button>
          <Button variant="text" onClick={() => navigate("/roles")}>
            Cancel
          </Button>
        </Box>
      </Paper>
    </Box>
  );
}
