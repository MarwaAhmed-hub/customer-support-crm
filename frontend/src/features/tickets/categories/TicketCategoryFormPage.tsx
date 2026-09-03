import { Alert, Box, Button, CircularProgress, FormControlLabel, MenuItem, Paper, Switch, TextField, Typography } from "@mui/material";
import axios from "axios";
import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { useNavigate, useParams } from "react-router-dom";
import * as departmentsApi from "../../departments/departmentsApi";
import type { Department } from "../../departments/types";
import * as categoriesApi from "./categoriesApi";

const GENERIC_FAILURE = "Something went wrong. Please try again.";
const NONE_OPTION = "";

interface FieldErrors {
  name?: string;
  description?: string;
}

/**
 * Serves both `/tickets/categories/new` (no `:id`) and `/tickets/categories/:id/edit` (`:id`
 * present) — mode is read from the route, matching `DepartmentFormPage`'s pattern.
 */
export function TicketCategoryFormPage() {
  const { id } = useParams<{ id: string }>();
  const isEdit = id !== undefined;
  const navigate = useNavigate();

  const [name, setName] = useState("");
  const [description, setDescription] = useState("");
  const [isActive, setIsActive] = useState(true);
  const [departmentId, setDepartmentId] = useState(NONE_OPTION);

  // The department a loaded category is *currently* linked to — kept separately from the
  // active-only `departments` list so the picker can still show (and keep) a link to a department
  // that has since been deactivated, matching UserFormPage's currentDepartmentName pattern.
  const [currentDepartmentName, setCurrentDepartmentName] = useState<string | null>(null);
  const [departments, setDepartments] = useState<Department[]>([]);

  const [loading, setLoading] = useState(isEdit);
  const [submitting, setSubmitting] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState<string | null>(null);

  // Active-only picker — same list for create and edit. Non-fatal on failure: the picker simply has
  // nothing to offer beyond "— none —", which is not worth blocking the whole form over.
  useEffect(() => {
    let cancelled = false;
    departmentsApi
      .listDepartments({ includeInactive: false })
      .then((fetched) => {
        if (!cancelled) setDepartments(fetched);
      })
      .catch(() => undefined);

    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    if (!isEdit || id === undefined) return;

    let cancelled = false;
    categoriesApi
      .getTicketCategory(id)
      .then((category) => {
        if (cancelled) return;
        setName(category.name);
        setDescription(category.description ?? "");
        setIsActive(category.isActive);
        setDepartmentId(category.departmentId ?? NONE_OPTION);
        setCurrentDepartmentName(category.departmentName);
      })
      .catch(() => {
        if (!cancelled) setFormError("Could not load this category. Please try again.");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [isEdit, id]);

  // If the category's current department isn't in the active-only list (deactivated since, or
  // simply not yet loaded), add it as a labelled extra option so re-selecting "no change" stays
  // possible and the select never silently blanks out a real link.
  const departmentOptions =
    departmentId !== NONE_OPTION && !departments.some((department) => department.id === departmentId)
      ? [...departments, { id: departmentId, name: `${currentDepartmentName ?? "Unknown"} (inactive)` }]
      : departments;

  function validate(): FieldErrors {
    const errors: FieldErrors = {};

    const trimmedName = name.trim();
    if (trimmedName.length === 0) {
      errors.name = "Name is required.";
    } else if (trimmedName.length > 128) {
      errors.name = "Name must be 128 characters or fewer.";
    }

    if (description.trim().length > 512) {
      errors.description = "Description must be 512 characters or fewer.";
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
    const descriptionPayload = trimmedDescription.length > 0 ? trimmedDescription : null;
    const departmentPayload = departmentId === NONE_OPTION ? null : departmentId;

    try {
      if (isEdit && id !== undefined) {
        await categoriesApi.updateTicketCategory(id, {
          name: name.trim(),
          description: descriptionPayload,
          isActive,
          departmentId: departmentPayload,
        });
      } else {
        await categoriesApi.createTicketCategory({ name: name.trim(), description: descriptionPayload, departmentId: departmentPayload });
      }
      navigate("/tickets/categories", { replace: true });
    } catch (caught: unknown) {
      if (axios.isAxiosError(caught) && caught.response?.status === 409) {
        setFieldErrors((current) => ({ ...current, name: "A category with this name already exists." }));
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
        {isEdit ? "Edit ticket category" : "New ticket category"}
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
          id="description"
          label="Description"
          value={description}
          onChange={(event) => setDescription(event.target.value)}
          error={fieldErrors.description !== undefined}
          helperText={fieldErrors.description}
          fullWidth
          multiline
          minRows={2}
          margin="normal"
        />

        <TextField
          id="department"
          select
          label="Department"
          value={departmentId}
          onChange={(event) => setDepartmentId(event.target.value)}
          helperText="Filters the assignee picker on tickets in this category to this department's agents."
          fullWidth
          margin="normal"
          slotProps={{ select: { displayEmpty: true }, inputLabel: { shrink: true } }}
        >
          <MenuItem value={NONE_OPTION}>— none —</MenuItem>
          {departmentOptions.map((department) => (
            <MenuItem key={department.id} value={department.id}>
              {department.name}
            </MenuItem>
          ))}
        </TextField>

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
          <Button variant="text" onClick={() => navigate("/tickets/categories")}>
            Cancel
          </Button>
        </Box>
      </Paper>
    </Box>
  );
}
