import { Alert, Box, Button, CircularProgress, MenuItem, Paper, TextField, Typography } from "@mui/material";
import axios from "axios";
import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { useNavigate, useParams } from "react-router-dom";
import * as branchesApi from "../branches/branchesApi";
import type { Branch } from "../branches/types";
import * as departmentsApi from "../departments/departmentsApi";
import type { Department } from "../departments/types";
import * as usersApi from "./usersApi";

const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const GENERIC_FAILURE = "Something went wrong. Please try again.";
const NONE_OPTION = "";

interface FieldErrors {
  email?: string;
  displayName?: string;
  password?: string;
  department?: string;
  branch?: string;
}

/**
 * Serves both `/users/new` (no `:id`) and `/users/:id/edit` (`:id` present) — mode is read from the
 * route, not passed as a prop.
 */
export function UserFormPage() {
  const { id } = useParams<{ id: string }>();
  const isEdit = id !== undefined;
  const navigate = useNavigate();

  const [email, setEmail] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [password, setPassword] = useState("");
  const [departmentId, setDepartmentId] = useState(NONE_OPTION);
  const [branchId, setBranchId] = useState(NONE_OPTION);

  // The department/branch a loaded user is *currently* assigned to, denormalised on the detail
  // response. Kept separately from the active-only `departments`/`branches` lists below so the
  // picker can still show — and keep — an assignment that has since been deactivated, rather than
  // silently dropping it because it fell out of the active list (see the matching backend fix in
  // UsersController.Update: an *unchanged* department/branch is never re-validated for active-ness).
  const [currentDepartmentName, setCurrentDepartmentName] = useState<string | null>(null);
  const [currentBranchName, setCurrentBranchName] = useState<string | null>(null);

  const [departments, setDepartments] = useState<Department[]>([]);
  const [branches, setBranches] = useState<Branch[]>([]);

  const [loading, setLoading] = useState(isEdit);
  const [submitting, setSubmitting] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState<string | null>(null);

  // Active-only pickers — same list for create and edit. Non-fatal on failure: the pickers simply
  // have nothing to offer beyond "— none —", which is not worth blocking the whole form over.
  useEffect(() => {
    let cancelled = false;
    Promise.all([departmentsApi.listDepartments({ includeInactive: false }), branchesApi.listBranches({ includeInactive: false })])
      .then(([fetchedDepartments, fetchedBranches]) => {
        if (cancelled) return;
        setDepartments(fetchedDepartments);
        setBranches(fetchedBranches);
      })
      .catch(() => undefined);

    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    if (!isEdit || id === undefined) return;

    let cancelled = false;
    usersApi
      .getUser(id)
      .then((user) => {
        if (cancelled) return;
        setEmail(user.email);
        setDisplayName(user.displayName);
        setDepartmentId(user.departmentId ?? NONE_OPTION);
        setBranchId(user.branchId ?? NONE_OPTION);
        setCurrentDepartmentName(user.departmentName);
        setCurrentBranchName(user.branchName);
      })
      .catch(() => {
        if (!cancelled) setFormError("Could not load this user. Please try again.");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [isEdit, id]);

  // If the user's current assignment isn't in the active-only list (deactivated since, or simply
  // not yet loaded), add it as a labelled extra option so re-selecting "no change" stays possible
  // and the select never silently blanks out a real assignment.
  const departmentOptions =
    departmentId !== NONE_OPTION && !departments.some((department) => department.id === departmentId)
      ? [...departments, { id: departmentId, name: `${currentDepartmentName ?? "Unknown"} (inactive)` }]
      : departments;
  const branchOptions =
    branchId !== NONE_OPTION && !branches.some((branch) => branch.id === branchId)
      ? [...branches, { id: branchId, name: `${currentBranchName ?? "Unknown"} (inactive)` }]
      : branches;

  function validate(): FieldErrors {
    const errors: FieldErrors = {};

    const trimmedEmail = email.trim();
    if (trimmedEmail.length === 0) {
      errors.email = "Email is required.";
    } else if (!EMAIL_PATTERN.test(trimmedEmail)) {
      errors.email = "Enter a valid email address.";
    } else if (trimmedEmail.length > 256) {
      errors.email = "Email must be 256 characters or fewer.";
    }

    const trimmedName = displayName.trim();
    if (trimmedName.length === 0) {
      errors.displayName = "Name is required.";
    } else if (trimmedName.length > 128) {
      errors.displayName = "Name must be 128 characters or fewer.";
    }

    if (!isEdit) {
      if (password.length === 0) {
        errors.password = "Password is required.";
      } else if (password.length < 8) {
        errors.password = "Password must be at least 8 characters.";
      }
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

    const departmentPayload = departmentId === NONE_OPTION ? null : departmentId;
    const branchPayload = branchId === NONE_OPTION ? null : branchId;

    try {
      if (isEdit && id !== undefined) {
        const updated = await usersApi.updateUser(id, {
          email: email.trim(),
          displayName: displayName.trim(),
          departmentId: departmentPayload,
          branchId: branchPayload,
        });
        navigate(`/users/${updated.id}`, { replace: true });
      } else {
        const created = await usersApi.createUser({
          email: email.trim(),
          displayName: displayName.trim(),
          password,
          departmentId: departmentPayload,
          branchId: branchPayload,
        });
        navigate(`/users/${created.id}`, { replace: true });
      }
    } catch (caught: unknown) {
      if (axios.isAxiosError(caught) && caught.response?.status === 409) {
        setFieldErrors((current) => ({ ...current, email: "This email is already in use." }));
      } else if (axios.isAxiosError(caught) && caught.response?.status === 400) {
        const errorCode: unknown = caught.response.data?.error;
        if (errorCode === "invalid_department") {
          setFieldErrors((current) => ({ ...current, department: "This department is no longer active." }));
        } else if (errorCode === "invalid_branch") {
          setFieldErrors((current) => ({ ...current, branch: "This branch is no longer active." }));
        } else {
          // The backend's uniform { "error": "invalid_request" } contract carries no per-field
          // detail for the generic case, so it surfaces as one form-level message rather than a
          // fabricated field mapping.
          setFormError(GENERIC_FAILURE);
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
        {isEdit ? "Edit user" : "New user"}
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
          id="email"
          label="Email"
          type="email"
          value={email}
          onChange={(event) => setEmail(event.target.value)}
          error={fieldErrors.email !== undefined}
          helperText={fieldErrors.email}
          fullWidth
          margin="normal"
        />

        <TextField
          id="displayName"
          label="Name"
          value={displayName}
          onChange={(event) => setDisplayName(event.target.value)}
          error={fieldErrors.displayName !== undefined}
          helperText={fieldErrors.displayName}
          fullWidth
          margin="normal"
        />

        {!isEdit && (
          <TextField
            id="password"
            label="Temporary password"
            type="password"
            autoComplete="new-password"
            value={password}
            onChange={(event) => setPassword(event.target.value)}
            error={fieldErrors.password !== undefined}
            helperText={fieldErrors.password}
            fullWidth
            margin="normal"
          />
        )}

        <TextField
          id="department"
          select
          label="Department"
          value={departmentId}
          onChange={(event) => setDepartmentId(event.target.value)}
          error={fieldErrors.department !== undefined}
          helperText={fieldErrors.department}
          fullWidth
          margin="normal"
          // displayEmpty: without it, MUI's Select renders the box blank rather than the "— none —"
          // MenuItem's label whenever the value is the empty string — see
          // https://mui.com/material-ui/react-select/#empty-value. inputLabel.shrink: without it,
          // the "Department" label sits centered over that same displayed text instead of floating
          // above it, since MUI otherwise only shrinks the label when it thinks there's a "real" value.
          slotProps={{ select: { displayEmpty: true }, inputLabel: { shrink: true } }}
        >
          <MenuItem value={NONE_OPTION}>— none —</MenuItem>
          {departmentOptions.map((department) => (
            <MenuItem key={department.id} value={department.id}>
              {department.name}
            </MenuItem>
          ))}
        </TextField>

        <TextField
          id="branch"
          select
          label="Branch"
          value={branchId}
          onChange={(event) => setBranchId(event.target.value)}
          error={fieldErrors.branch !== undefined}
          helperText={fieldErrors.branch}
          fullWidth
          margin="normal"
          slotProps={{ select: { displayEmpty: true }, inputLabel: { shrink: true } }}
        >
          <MenuItem value={NONE_OPTION}>— none —</MenuItem>
          {branchOptions.map((branch) => (
            <MenuItem key={branch.id} value={branch.id}>
              {branch.name}
            </MenuItem>
          ))}
        </TextField>

        <Box sx={{ display: "flex", gap: 1.5, mt: 3 }}>
          <Button type="submit" variant="contained" disabled={submitting}>
            {submitting ? "Saving…" : "Save"}
          </Button>
          <Button
            variant="text"
            onClick={() => navigate(isEdit && id !== undefined ? `/users/${id}` : "/users")}
          >
            Cancel
          </Button>
        </Box>
      </Paper>
    </Box>
  );
}
