import { Alert, Box, Button, CircularProgress, FormControlLabel, Paper, Switch, TextField, Typography } from "@mui/material";
import axios from "axios";
import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { useNavigate, useParams } from "react-router-dom";
import * as quickRepliesApi from "./quickRepliesApi";

const GENERIC_FAILURE = "Something went wrong. Please try again.";

interface FieldErrors {
  title?: string;
  body?: string;
}

/**
 * Serves both `/quick-replies/new` (no `:id`) and `/quick-replies/:id/edit` (`:id` present) — mode
 * is read from the route, matching `TicketCategoryFormPage`'s pattern.
 */
export function QuickReplyFormPage() {
  const { id } = useParams<{ id: string }>();
  const isEdit = id !== undefined;
  const navigate = useNavigate();

  const [title, setTitle] = useState("");
  const [body, setBody] = useState("");
  const [isActive, setIsActive] = useState(true);

  const [loading, setLoading] = useState(isEdit);
  const [submitting, setSubmitting] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState<string | null>(null);

  useEffect(() => {
    if (!isEdit || id === undefined) return;

    let cancelled = false;
    quickRepliesApi
      .getQuickReply(id)
      .then((quickReply) => {
        if (cancelled) return;
        setTitle(quickReply.title);
        setBody(quickReply.body);
        setIsActive(quickReply.isActive);
      })
      .catch(() => {
        if (!cancelled) setFormError("Could not load this quick reply. Please try again.");
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

    const trimmedBody = body.trim();
    if (trimmedBody.length === 0) {
      errors.body = "Body is required.";
    } else if (trimmedBody.length > 5000) {
      errors.body = "Body must be 5000 characters or fewer.";
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
        await quickRepliesApi.updateQuickReply(id, { title: title.trim(), body: body.trim(), isActive });
      } else {
        await quickRepliesApi.createQuickReply({ title: title.trim(), body: body.trim() });
      }
      navigate("/quick-replies", { replace: true });
    } catch (caught: unknown) {
      if (axios.isAxiosError(caught) && caught.response?.status === 409) {
        setFieldErrors((current) => ({ ...current, title: "A quick reply with this title already exists." }));
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
    <Box sx={{ maxWidth: 560 }}>
      <Typography variant="h4" sx={{ mb: 3 }}>
        {isEdit ? "Edit quick reply" : "New quick reply"}
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
        />

        <TextField
          id="body"
          label="Body"
          value={body}
          onChange={(event) => setBody(event.target.value)}
          error={fieldErrors.body !== undefined}
          helperText={fieldErrors.body}
          fullWidth
          multiline
          minRows={5}
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
          <Button variant="text" onClick={() => navigate("/quick-replies")}>
            Cancel
          </Button>
        </Box>
      </Paper>
    </Box>
  );
}
