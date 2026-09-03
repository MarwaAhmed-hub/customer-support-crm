import { Alert, Box, Button, CircularProgress, Divider, Paper, Stack, TextField, Typography } from "@mui/material";
import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { useAuth } from "../../auth/useAuth";
import * as notesApi from "./notesApi";
import type { CustomerNote } from "./types";

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

export function CustomerNotesPanel({ customerId }: { customerId: string }) {
  const { hasPermission } = useAuth();
  const canCreate = hasPermission("customers.notes.create");
  const canUpdate = hasPermission("customers.notes.update");
  const canDelete = hasPermission("customers.notes.delete");

  const [notes, setNotes] = useState<CustomerNote[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [newBody, setNewBody] = useState("");
  const [submitting, setSubmitting] = useState(false);

  const [editingId, setEditingId] = useState<string | null>(null);
  const [editBody, setEditBody] = useState("");
  const [savingEdit, setSavingEdit] = useState(false);

  const [deletingId, setDeletingId] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    notesApi
      .listNotes(customerId)
      .then((result) => {
        if (!cancelled) setNotes(result);
      })
      .catch(() => {
        if (!cancelled) setError("Could not load notes. Please try again.");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [customerId]);

  async function handleAdd(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    if (submitting || newBody.trim().length === 0) return;

    setSubmitting(true);
    setError(null);

    try {
      const created = await notesApi.createNote(customerId, newBody.trim());
      setNotes((current) => [created, ...current]);
      setNewBody("");
    } catch {
      setError("Could not add this note. Please try again.");
    } finally {
      setSubmitting(false);
    }
  }

  function startEdit(note: CustomerNote): void {
    setEditingId(note.id);
    setEditBody(note.body);
  }

  function cancelEdit(): void {
    setEditingId(null);
    setEditBody("");
  }

  async function saveEdit(noteId: string): Promise<void> {
    if (savingEdit || editBody.trim().length === 0) return;

    setSavingEdit(true);
    setError(null);

    try {
      const updated = await notesApi.updateNote(customerId, noteId, editBody.trim());
      setNotes((current) => current.map((note) => (note.id === noteId ? updated : note)));
      cancelEdit();
    } catch {
      setError("Could not save this note. Please try again.");
    } finally {
      setSavingEdit(false);
    }
  }

  async function handleDelete(noteId: string): Promise<void> {
    if (deletingId !== null) return;
    if (!window.confirm("Delete this note? This cannot be undone.")) return;

    setDeletingId(noteId);
    setError(null);

    try {
      await notesApi.deleteNote(customerId, noteId);
      setNotes((current) => current.filter((note) => note.id !== noteId));
    } catch {
      setError("Could not delete this note. Please try again.");
    } finally {
      setDeletingId(null);
    }
  }

  return (
    <Box>
      <Typography variant="subtitle1" sx={{ mb: 1.5 }}>
        Notes
      </Typography>

      {error !== null && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      {canCreate && (
        <Box component="form" onSubmit={handleAdd} sx={{ display: "flex", gap: 1.5, mb: 2, alignItems: "flex-start" }}>
          <TextField
            placeholder="Add a note…"
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
      ) : notes.length === 0 ? (
        <Paper variant="outlined" sx={{ p: 3, textAlign: "center" }}>
          <Typography color="text.secondary">No notes yet.</Typography>
        </Paper>
      ) : (
        <Stack spacing={1.5}>
          {notes.map((note, index) => (
            <Box key={note.id}>
              {index > 0 && <Divider sx={{ mb: 1.5 }} />}
              {editingId === note.id ? (
                <Box sx={{ display: "flex", flexDirection: "column", gap: 1 }}>
                  <TextField
                    value={editBody}
                    onChange={(event) => setEditBody(event.target.value)}
                    size="small"
                    fullWidth
                    multiline
                    minRows={2}
                    autoFocus
                    slotProps={{ htmlInput: { maxLength: 4000 } }}
                  />
                  <Box sx={{ display: "flex", gap: 1 }}>
                    <Button
                      size="small"
                      variant="contained"
                      disabled={savingEdit || editBody.trim().length === 0}
                      onClick={() => void saveEdit(note.id)}
                    >
                      {savingEdit ? "Saving…" : "Save"}
                    </Button>
                    <Button size="small" variant="text" onClick={cancelEdit} disabled={savingEdit}>
                      Cancel
                    </Button>
                  </Box>
                </Box>
              ) : (
                <Box>
                  <Typography variant="body2" sx={{ whiteSpace: "pre-wrap" }}>
                    {note.body}
                  </Typography>
                  <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mt: 0.5 }}>
                    <Typography variant="caption" color="text.secondary">
                      {note.createdByDisplayName ?? "Unknown user"} · {formatDate(note.createdAt)}
                      {note.updatedAt !== null && ` (edited ${formatDate(note.updatedAt)})`}
                    </Typography>
                    <Box sx={{ display: "flex", gap: 0.5 }}>
                      {canUpdate && (
                        <Button size="small" onClick={() => startEdit(note)}>
                          Edit
                        </Button>
                      )}
                      {canDelete && (
                        <Button
                          size="small"
                          color="error"
                          disabled={deletingId === note.id}
                          onClick={() => void handleDelete(note.id)}
                        >
                          {deletingId === note.id ? "Deleting…" : "Delete"}
                        </Button>
                      )}
                    </Box>
                  </Box>
                </Box>
              )}
            </Box>
          ))}
        </Stack>
      )}
    </Box>
  );
}
