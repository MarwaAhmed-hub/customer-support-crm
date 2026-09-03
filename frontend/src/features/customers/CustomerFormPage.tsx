import { Alert, Box, Button, CircularProgress, Paper, TextField, Typography } from "@mui/material";
import axios from "axios";
import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { useNavigate, useParams } from "react-router-dom";
import * as customersApi from "./customersApi";

// Same pattern as UserFormPage — kept purely client-side; the server is the source of truth for the
// format check (CustomersService, reusing the same EmailAddressAttribute).
const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;
const GENERIC_FAILURE = "Something went wrong. Please try again.";

interface FieldErrors {
  firstName?: string;
  lastName?: string;
  companyName?: string;
  email?: string;
  phone?: string;
}

/**
 * Serves both `/customers/new` (no `:id`) and `/customers/:id/edit` (`:id` present) — mode is read
 * from the route, matching `DepartmentFormPage`'s pattern.
 */
export function CustomerFormPage() {
  const { id } = useParams<{ id: string }>();
  const isEdit = id !== undefined;
  const navigate = useNavigate();

  const [firstName, setFirstName] = useState("");
  const [lastName, setLastName] = useState("");
  const [companyName, setCompanyName] = useState("");
  const [email, setEmail] = useState("");
  const [phone, setPhone] = useState("");

  const [loading, setLoading] = useState(isEdit);
  const [submitting, setSubmitting] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState<string | null>(null);

  useEffect(() => {
    if (!isEdit || id === undefined) return;

    let cancelled = false;
    customersApi
      .getCustomer(id)
      .then((customer) => {
        if (cancelled) return;
        setFirstName(customer.firstName);
        setLastName(customer.lastName);
        setCompanyName(customer.companyName ?? "");
        setEmail(customer.email ?? "");
        setPhone(customer.phone ?? "");
      })
      .catch(() => {
        if (!cancelled) setFormError("Could not load this customer. Please try again.");
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

    const trimmedFirstName = firstName.trim();
    if (trimmedFirstName.length === 0) {
      errors.firstName = "First name is required.";
    } else if (trimmedFirstName.length > 128) {
      errors.firstName = "First name must be 128 characters or fewer.";
    }

    const trimmedLastName = lastName.trim();
    if (trimmedLastName.length === 0) {
      errors.lastName = "Last name is required.";
    } else if (trimmedLastName.length > 128) {
      errors.lastName = "Last name must be 128 characters or fewer.";
    }

    if (companyName.trim().length > 128) {
      errors.companyName = "Company must be 128 characters or fewer.";
    }

    const trimmedEmail = email.trim();
    if (trimmedEmail.length > 0) {
      if (!EMAIL_PATTERN.test(trimmedEmail)) {
        errors.email = "Enter a valid email address.";
      } else if (trimmedEmail.length > 256) {
        errors.email = "Email must be 256 characters or fewer.";
      }
    }

    if (phone.trim().length > 64) {
      errors.phone = "Phone must be 64 characters or fewer.";
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

    const trimmedCompanyName = companyName.trim();
    const trimmedEmail = email.trim();
    const trimmedPhone = phone.trim();

    const payload = {
      firstName: firstName.trim(),
      lastName: lastName.trim(),
      companyName: trimmedCompanyName.length > 0 ? trimmedCompanyName : null,
      email: trimmedEmail.length > 0 ? trimmedEmail : null,
      phone: trimmedPhone.length > 0 ? trimmedPhone : null,
    };

    try {
      if (isEdit && id !== undefined) {
        const updated = await customersApi.updateCustomer(id, payload);
        navigate(`/customers/${updated.id}`, { replace: true });
      } else {
        const created = await customersApi.createCustomer(payload);
        navigate(`/customers/${created.id}`, { replace: true });
      }
    } catch (caught: unknown) {
      if (axios.isAxiosError(caught) && caught.response?.status === 400) {
        const errorCode: unknown = caught.response.data?.error;
        if (errorCode === "invalid_email") {
          setFieldErrors((current) => ({ ...current, email: "Enter a valid email address." }));
        } else if (errorCode === "invalid_name") {
          setFieldErrors((current) => ({ ...current, firstName: "First and last name are required." }));
        } else {
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
        {isEdit ? "Edit customer" : "New customer"}
      </Typography>

      <Paper component="form" onSubmit={handleSubmit} noValidate variant="outlined" sx={{ p: 3 }}>
        {formError !== null && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {formError}
          </Alert>
        )}

        <TextField
          id="firstName"
          label="First name"
          value={firstName}
          onChange={(event) => setFirstName(event.target.value)}
          error={fieldErrors.firstName !== undefined}
          helperText={fieldErrors.firstName}
          fullWidth
          margin="normal"
        />

        <TextField
          id="lastName"
          label="Last name"
          value={lastName}
          onChange={(event) => setLastName(event.target.value)}
          error={fieldErrors.lastName !== undefined}
          helperText={fieldErrors.lastName}
          fullWidth
          margin="normal"
        />

        <TextField
          id="companyName"
          label="Company"
          value={companyName}
          onChange={(event) => setCompanyName(event.target.value)}
          error={fieldErrors.companyName !== undefined}
          helperText={fieldErrors.companyName}
          fullWidth
          margin="normal"
        />

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
          id="phone"
          label="Phone"
          value={phone}
          onChange={(event) => setPhone(event.target.value)}
          error={fieldErrors.phone !== undefined}
          helperText={fieldErrors.phone}
          fullWidth
          margin="normal"
        />

        <Box sx={{ display: "flex", gap: 1.5, mt: 3 }}>
          <Button type="submit" variant="contained" disabled={submitting}>
            {submitting ? "Saving…" : "Save"}
          </Button>
          <Button
            variant="text"
            onClick={() => navigate(isEdit && id !== undefined ? `/customers/${id}` : "/customers")}
          >
            Cancel
          </Button>
        </Box>
      </Paper>
    </Box>
  );
}
