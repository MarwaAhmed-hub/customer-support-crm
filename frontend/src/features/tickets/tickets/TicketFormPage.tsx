import { Alert, Box, Button, CircularProgress, MenuItem, Paper, TextField, Typography } from "@mui/material";
import axios from "axios";
import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { useNavigate, useParams, useSearchParams } from "react-router-dom";
import * as categoriesApi from "../categories/categoriesApi";
import type { TicketCategory } from "../categories/types";
import * as prioritiesApi from "../priorities/prioritiesApi";
import type { TicketPriority } from "../priorities/types";
import * as customersApi from "../../customers/customersApi";
import type { Customer } from "../../customers/types";
import * as ticketsApi from "./ticketsApi";

const GENERIC_FAILURE = "Something went wrong. Please try again.";

interface FieldErrors {
  customerId?: string;
  subject?: string;
  description?: string;
  categoryId?: string;
  priorityId?: string;
}

/**
 * Serves both `/tickets/new` (no `:id`) and `/tickets/:id/edit` (`:id` present) — mode is read from
 * the route, matching `CustomerFormPage`'s pattern. On create, `?customerId=` pre-selects (and locks)
 * the customer — reached from `CustomerDetailPage`'s "Create ticket" button.
 */
export function TicketFormPage() {
  const { id } = useParams<{ id: string }>();
  const [searchParams] = useSearchParams();
  const isEdit = id !== undefined;
  const navigate = useNavigate();

  const prefilledCustomerId = searchParams.get("customerId") ?? "";

  const [customerId, setCustomerId] = useState(prefilledCustomerId);
  const [subject, setSubject] = useState("");
  const [description, setDescription] = useState("");
  const [categoryId, setCategoryId] = useState("");
  const [priorityId, setPriorityId] = useState("");

  // Story 23: the ticket's assignee as loaded, before this edit — compared against the response after
  // a successful save to detect a null -> agent transition caused by automatic assignment (never set
  // directly by this form, which has no assignee field of its own).
  const [originalAssignedUserId, setOriginalAssignedUserId] = useState<string | null>(null);

  const [customers, setCustomers] = useState<Customer[]>([]);
  const [categories, setCategories] = useState<TicketCategory[]>([]);
  const [priorities, setPriorities] = useState<TicketPriority[]>([]);

  const [loading, setLoading] = useState(isEdit);
  const [submitting, setSubmitting] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState<string | null>(null);

  // Picker options. Non-fatal on failure: worst case a picker has nothing to offer.
  useEffect(() => {
    let cancelled = false;
    Promise.all([
      customersApi.listCustomers(),
      categoriesApi.listTicketCategories({ includeInactive: false }),
      prioritiesApi.listTicketPriorities({ includeInactive: false }),
    ])
      .then(([fetchedCustomers, fetchedCategories, fetchedPriorities]) => {
        if (cancelled) return;
        setCustomers(fetchedCustomers);
        setCategories(fetchedCategories);
        setPriorities(fetchedPriorities);
      })
      .catch(() => undefined);

    return () => {
      cancelled = true;
    };
  }, []);

  useEffect(() => {
    if (!isEdit || id === undefined) return;

    let cancelled = false;
    ticketsApi
      .getTicket(id)
      .then((ticket) => {
        if (cancelled) return;
        setCustomerId(ticket.customerId);
        setSubject(ticket.subject);
        setDescription(ticket.description);
        setCategoryId(ticket.categoryId);
        setPriorityId(ticket.priorityId);
        setOriginalAssignedUserId(ticket.assignedUserId);
      })
      .catch(() => {
        if (!cancelled) setFormError("Could not load this ticket. Please try again.");
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

    if (!isEdit && customerId.length === 0) {
      errors.customerId = "Customer is required.";
    }

    const trimmedSubject = subject.trim();
    if (trimmedSubject.length === 0) {
      errors.subject = "Subject is required.";
    } else if (trimmedSubject.length > 200) {
      errors.subject = "Subject must be 200 characters or fewer.";
    }

    const trimmedDescription = description.trim();
    if (trimmedDescription.length === 0) {
      errors.description = "Description is required.";
    } else if (trimmedDescription.length > 4000) {
      errors.description = "Description must be 4000 characters or fewer.";
    }

    if (categoryId.length === 0) {
      errors.categoryId = "Category is required.";
    }

    if (priorityId.length === 0) {
      errors.priorityId = "Priority is required.";
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

    try {
      if (isEdit && id !== undefined) {
        const updated = await ticketsApi.updateTicket(id, {
          subject: subject.trim(),
          description: description.trim(),
          categoryId,
          priorityId,
        });
        // Story 23: this form never sets AssignedUserId itself — a null -> agent transition here can
        // only be TicketAssignmentService's doing, triggered by the category change just saved above.
        const autoAssignNotice =
          originalAssignedUserId === null && updated.assignedUserId !== null
            ? `Ticket auto-assigned to ${updated.assignedUserName ?? "an agent"}.`
            : undefined;
        navigate(`/tickets/${updated.id}`, { replace: true, state: autoAssignNotice !== undefined ? { autoAssignNotice } : undefined });
      } else {
        const created = await ticketsApi.createTicket({
          customerId,
          subject: subject.trim(),
          description: description.trim(),
          categoryId,
          priorityId,
        });
        navigate(`/tickets/${created.id}`, { replace: true });
      }
    } catch (caught: unknown) {
      if (axios.isAxiosError(caught) && caught.response?.status === 404) {
        const errorCode: unknown = caught.response.data?.error;
        if (errorCode === "customer_not_found") {
          setFieldErrors((current) => ({ ...current, customerId: "This customer no longer exists." }));
        } else if (errorCode === "category_not_found") {
          setFieldErrors((current) => ({ ...current, categoryId: "This category is no longer available." }));
        } else if (errorCode === "priority_not_found") {
          setFieldErrors((current) => ({ ...current, priorityId: "This priority is no longer available." }));
        } else {
          setFormError(GENERIC_FAILURE);
        }
      } else if (axios.isAxiosError(caught) && caught.response?.status === 400) {
        const errorCode: unknown = caught.response.data?.error;
        if (errorCode === "invalid_subject") {
          setFieldErrors((current) => ({ ...current, subject: "Subject is required." }));
        } else if (errorCode === "invalid_description") {
          setFieldErrors((current) => ({ ...current, description: "Description is required." }));
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

  const selectedCustomerStillActive = customers.some((customer) => customer.id === customerId);

  return (
    <Box sx={{ maxWidth: 560 }}>
      <Typography variant="h4" sx={{ mb: 3 }}>
        {isEdit ? "Edit ticket" : "New ticket"}
      </Typography>

      <Paper component="form" onSubmit={handleSubmit} noValidate variant="outlined" sx={{ p: 3 }}>
        {formError !== null && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {formError}
          </Alert>
        )}

        <TextField
          id="customer"
          select
          label="Customer"
          value={customerId}
          onChange={(event) => setCustomerId(event.target.value)}
          error={fieldErrors.customerId !== undefined}
          helperText={fieldErrors.customerId}
          fullWidth
          margin="normal"
          disabled={isEdit}
          slotProps={{ select: { displayEmpty: true }, inputLabel: { shrink: true } }}
        >
          <MenuItem value="" disabled>
            — select a customer —
          </MenuItem>
          {customerId.length > 0 && !selectedCustomerStillActive && (
            <MenuItem value={customerId}>(current customer)</MenuItem>
          )}
          {customers.map((customer) => (
            <MenuItem key={customer.id} value={customer.id}>
              {customer.firstName} {customer.lastName}
              {customer.companyName !== null ? ` — ${customer.companyName}` : ""}
            </MenuItem>
          ))}
        </TextField>

        <TextField
          id="subject"
          label="Subject"
          value={subject}
          onChange={(event) => setSubject(event.target.value)}
          error={fieldErrors.subject !== undefined}
          helperText={fieldErrors.subject}
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
          minRows={4}
          margin="normal"
        />

        <TextField
          id="category"
          select
          label="Category"
          value={categoryId}
          onChange={(event) => setCategoryId(event.target.value)}
          error={fieldErrors.categoryId !== undefined}
          helperText={fieldErrors.categoryId}
          fullWidth
          margin="normal"
          slotProps={{ select: { displayEmpty: true }, inputLabel: { shrink: true } }}
        >
          <MenuItem value="" disabled>
            — select a category —
          </MenuItem>
          {categories.map((category) => (
            <MenuItem key={category.id} value={category.id}>
              {category.name}
            </MenuItem>
          ))}
        </TextField>

        <TextField
          id="priority"
          select
          label="Priority"
          value={priorityId}
          onChange={(event) => setPriorityId(event.target.value)}
          error={fieldErrors.priorityId !== undefined}
          helperText={fieldErrors.priorityId}
          fullWidth
          margin="normal"
          slotProps={{ select: { displayEmpty: true }, inputLabel: { shrink: true } }}
        >
          <MenuItem value="" disabled>
            — select a priority —
          </MenuItem>
          {priorities.map((priority) => (
            <MenuItem key={priority.id} value={priority.id}>
              {priority.name}
            </MenuItem>
          ))}
        </TextField>

        <Box sx={{ display: "flex", gap: 1.5, mt: 3 }}>
          <Button type="submit" variant="contained" disabled={submitting}>
            {submitting ? "Saving…" : "Save"}
          </Button>
          <Button
            variant="text"
            onClick={() => navigate(isEdit && id !== undefined ? `/tickets/${id}` : "/tickets")}
          >
            Cancel
          </Button>
        </Box>
      </Paper>
    </Box>
  );
}
