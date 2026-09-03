import { Alert, Box, Button, CircularProgress, MenuItem, Paper, TextField, Typography } from "@mui/material";
import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { useNavigate, useParams, useSearchParams } from "react-router-dom";
import * as ticketsApi from "../../tickets/tickets/ticketsApi";
import type { TicketListItem } from "../../tickets/tickets/types";
import * as tasksApi from "./tasksApi";

const GENERIC_FAILURE = "Something went wrong. Please try again.";
const NO_TICKET_OPTION = "";

interface FieldErrors {
  title?: string;
  description?: string;
}

/** `datetime-local` has no timezone of its own — the browser treats the value as local time, so this reads it as local and converts to a UTC ISO string for the API. */
function fromDatetimeLocalValue(value: string): string {
  return new Date(value).toISOString();
}

/** Inverse of `fromDatetimeLocalValue` — formats a UTC ISO string as the local `YYYY-MM-DDTHH:mm` the `datetime-local` input expects, so editing a task shows the reminder in the viewer's own time zone. */
function toDatetimeLocalValue(isoUtc: string): string {
  const date = new Date(isoUtc);
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

/**
 * Serves both `/agent-desk/tasks/new` (no `:id`) and `/agent-desk/tasks/:id/edit` (`:id` present) —
 * mode is read from the route, matching `TicketFormPage`'s pattern.
 *
 * Ticket linking has two entry points (see the story's correction):
 * - From `/agent-desk/tasks/new` directly: an optional "Related ticket" dropdown lets the Agent pick
 *   any ticket, or leave it unlinked.
 * - From a ticket's detail page via `/agent-desk/tasks/new?ticketId=<id>`: the ticket is already known,
 *   so no dropdown is shown at all — just a read-only "Linked ticket" line. The Agent never has to
 *   (and cannot, on this screen) select a different ticket in this flow.
 * Editing an existing task always shows the dropdown (editable, including re-linking or unlinking),
 * regardless of how the task was originally created.
 */
export function TaskFormPage() {
  const { id } = useParams<{ id: string }>();
  const [searchParams] = useSearchParams();
  const isEdit = id !== undefined;
  const navigate = useNavigate();

  // Only meaningful on create — the edit route has no ?ticketId= of its own; a task's existing link
  // is read from the loaded task instead, and is always editable via the dropdown.
  const lockedTicketId = !isEdit ? searchParams.get("ticketId") : null;

  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [reminderAt, setReminderAt] = useState("");
  const [selectedTicketId, setSelectedTicketId] = useState(lockedTicketId ?? NO_TICKET_OPTION);
  const [lockedTicketSubject, setLockedTicketSubject] = useState<string | null>(null);
  const [ticketOptions, setTicketOptions] = useState<TicketListItem[]>([]);

  const [loading, setLoading] = useState(isEdit);
  const [submitting, setSubmitting] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState<string | null>(null);

  // The picker's option source — not fetched at all when the ticket is locked (arrived via
  // ?ticketId=), since no dropdown renders in that case.
  useEffect(() => {
    if (lockedTicketId !== null) return;

    let cancelled = false;
    ticketsApi
      .listTickets({ pageSize: 100 })
      .then((result) => {
        if (!cancelled) setTicketOptions(result.items);
      })
      .catch(() => {
        // Non-fatal: the picker simply has nothing to offer, matching the resilience pattern used
        // by other optional-relationship pickers in this codebase (e.g. the assignee picker on
        // TicketDetailPage).
      });

    return () => {
      cancelled = true;
    };
  }, [lockedTicketId]);

  // Resolves the locked ticket's subject for display only — the id itself is already fixed.
  useEffect(() => {
    if (lockedTicketId === null) return;

    let cancelled = false;
    ticketsApi
      .getTicket(lockedTicketId)
      .then((ticket) => {
        if (!cancelled) setLockedTicketSubject(ticket.subject);
      })
      .catch(() => {
        // Non-fatal here too: if the id turns out to be invalid, Create surfaces that as a normal
        // form-level error on submit (the backend still validates it).
      });

    return () => {
      cancelled = true;
    };
  }, [lockedTicketId]);

  useEffect(() => {
    if (!isEdit || id === undefined) return;

    let cancelled = false;
    tasksApi
      .getTask(id)
      .then((task) => {
        if (cancelled) return;
        setTitle(task.title);
        setDescription(task.description ?? "");
        setReminderAt(task.reminderAt !== null ? toDatetimeLocalValue(task.reminderAt) : "");
        setSelectedTicketId(task.ticketId ?? NO_TICKET_OPTION);
      })
      .catch(() => {
        if (!cancelled) setFormError("Could not load this task. Please try again.");
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

    const trimmedTitle = title.trim();
    if (trimmedTitle.length === 0) {
      errors.title = "Title is required.";
    } else if (trimmedTitle.length > 200) {
      errors.title = "Title must be 200 characters or fewer.";
    }

    if (description.trim().length > 4000) {
      errors.description = "Description must be 4000 characters or fewer.";
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

    const payload = {
      title: title.trim(),
      description: description.trim().length > 0 ? description.trim() : null,
      reminderAt: reminderAt.length > 0 ? fromDatetimeLocalValue(reminderAt) : null,
      ticketId: selectedTicketId.length > 0 ? selectedTicketId : null,
    };

    try {
      if (isEdit && id !== undefined) {
        await tasksApi.updateTask(id, payload);
        navigate("/agent-desk/tasks", { replace: true });
      } else {
        await tasksApi.createTask(payload);
        // Landing on the ticket itself (not the general task list) keeps the "add a task from this
        // ticket" flow self-contained — the new task is visible there immediately either way.
        navigate(lockedTicketId !== null ? `/tickets/${lockedTicketId}` : "/agent-desk/tasks", { replace: true });
      }
    } catch {
      setFormError(GENERIC_FAILURE);
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
    <Box sx={{ maxWidth: 560 }}>
      <Typography variant="h4" sx={{ mb: 3 }}>
        {isEdit ? "Edit task" : "New task"}
      </Typography>

      <Paper component="form" onSubmit={handleSubmit} noValidate variant="outlined" sx={{ p: 3 }}>
        {formError !== null && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {formError}
          </Alert>
        )}

        <TextField
          id="title"
          label="Title"
          value={title}
          onChange={(event) => setTitle(event.target.value)}
          error={fieldErrors.title !== undefined}
          helperText={fieldErrors.title}
          fullWidth
          margin="normal"
          slotProps={{ htmlInput: { maxLength: 200 } }}
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
          minRows={3}
          margin="normal"
          slotProps={{ htmlInput: { maxLength: 4000 } }}
        />

        <TextField
          id="reminderAt"
          label="Reminder"
          type="datetime-local"
          value={reminderAt}
          onChange={(event) => setReminderAt(event.target.value)}
          fullWidth
          margin="normal"
          slotProps={{ inputLabel: { shrink: true } }}
        />

        {lockedTicketId !== null ? (
          <Box sx={{ mt: 2 }}>
            <Typography
              variant="caption"
              color="text.secondary"
              sx={{ display: "block", mb: 0.5, textTransform: "uppercase", letterSpacing: 0.4, fontWeight: 600 }}
            >
              Linked ticket
            </Typography>
            <Typography variant="body2" sx={{ fontWeight: 600 }}>
              {lockedTicketSubject ?? "Loading…"}
            </Typography>
          </Box>
        ) : (
          <TextField
            id="ticket"
            select
            label="Related ticket (optional)"
            value={selectedTicketId}
            onChange={(event) => setSelectedTicketId(event.target.value)}
            fullWidth
            margin="normal"
            slotProps={{ select: { displayEmpty: true }, inputLabel: { shrink: true } }}
          >
            <MenuItem value={NO_TICKET_OPTION}>— None —</MenuItem>
            {ticketOptions.map((ticket) => (
              <MenuItem key={ticket.id} value={ticket.id}>
                {ticket.subject}
              </MenuItem>
            ))}
          </TextField>
        )}

        <Box sx={{ display: "flex", gap: 1.5, mt: 3 }}>
          <Button type="submit" variant="contained" disabled={submitting}>
            {submitting ? "Saving…" : "Save"}
          </Button>
          <Button variant="text" onClick={() => navigate("/agent-desk/tasks")}>
            Cancel
          </Button>
        </Box>
      </Paper>
    </Box>
  );
}
