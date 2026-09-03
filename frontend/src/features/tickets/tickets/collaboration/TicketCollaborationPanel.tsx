import { Alert, Box, Button, Chip, CircularProgress, Divider, Paper, Stack, TextField, Typography } from "@mui/material";
import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { useAuth } from "../../../auth/useAuth";
import * as collaborationApi from "./collaborationApi";
import type { TicketCollaborationComment } from "./types";

function formatDate(isoString: string): string {
  return new Date(isoString).toLocaleDateString("en-US", {
    month: "short",
    day: "numeric",
    year: "numeric",
    hour: "numeric",
    minute: "2-digit",
    hour12: true,
  });
}

/**
 * Internal, staff-only discussion thread on a ticket (Story 18) — never visible to the customer, and
 * never touches ticket status/assignee. Modeled on `CustomerNotesPanel`, minus edit/delete (out of
 * scope for this story).
 */
export function TicketCollaborationPanel({ ticketId }: { ticketId: string }) {
  const { hasPermission } = useAuth();
  const canView = hasPermission("tickets.collaboration.view");
  const canCreate = hasPermission("tickets.collaboration.create");

  const [comments, setComments] = useState<TicketCollaborationComment[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [newBody, setNewBody] = useState("");
  const [submitting, setSubmitting] = useState(false);

  useEffect(() => {
    if (!canView) return;

    let cancelled = false;
    setLoading(true);
    setError(null);

    collaborationApi
      .listCollaborationComments(ticketId)
      .then((result) => {
        if (!cancelled) setComments(result);
      })
      .catch(() => {
        if (!cancelled) setError("Could not load internal comments. Please try again.");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [ticketId, canView]);

  async function handleAdd(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    if (submitting || newBody.trim().length === 0) return;

    setSubmitting(true);
    setError(null);

    try {
      const created = await collaborationApi.createCollaborationComment(ticketId, newBody.trim());
      setComments((current) => [...current, created]);
      setNewBody("");
    } catch {
      setError("Could not add this comment. Please try again.");
    } finally {
      setSubmitting(false);
    }
  }

  if (!canView) {
    return null;
  }

  return (
    <Box>
      <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, mb: 1.5, flexWrap: "wrap" }}>
        <Typography variant="overline" color="text.secondary" sx={{ letterSpacing: 0.4, fontWeight: 700 }}>
          Internal Collaboration
        </Typography>
        <Chip label="Internal — not visible to the customer" size="small" color="warning" variant="outlined" />
      </Box>

      {error !== null && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      {canCreate && (
        <Box component="form" onSubmit={handleAdd} sx={{ display: "flex", gap: 1.5, mb: 2, alignItems: "flex-start" }}>
          <TextField
            placeholder="Add an internal comment…"
            value={newBody}
            onChange={(event) => setNewBody(event.target.value)}
            size="small"
            fullWidth
            multiline
            minRows={2}
            slotProps={{ htmlInput: { maxLength: 4000 } }}
          />
          <Button type="submit" variant="contained" disabled={submitting || newBody.trim().length === 0}>
            {submitting ? "Adding…" : "Add"}
          </Button>
        </Box>
      )}

      {loading ? (
        <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, py: 3 }}>
          <CircularProgress size={22} />
          <Typography color="text.secondary">Loading…</Typography>
        </Box>
      ) : comments.length === 0 ? (
        <Paper variant="outlined" sx={{ p: 3, textAlign: "center" }}>
          <Typography color="text.secondary">No internal comments yet.</Typography>
        </Paper>
      ) : (
        <Stack spacing={1.5}>
          {comments.map((comment, index) => (
            <Box key={comment.id}>
              {index > 0 && <Divider sx={{ mb: 1.5 }} />}
              <Typography variant="body2" sx={{ whiteSpace: "pre-wrap" }}>
                {comment.body}
              </Typography>
              <Typography variant="caption" color="text.secondary" sx={{ display: "block", mt: 0.5 }}>
                <strong>{comment.authorDisplayName ?? "Unknown user"}</strong> · {formatDate(comment.createdAt)}
              </Typography>
            </Box>
          ))}
        </Stack>
      )}
    </Box>
  );
}
