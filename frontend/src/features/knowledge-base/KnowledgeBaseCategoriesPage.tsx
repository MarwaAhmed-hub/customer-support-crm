import AddIcon from "@mui/icons-material/Add";
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  Dialog,
  DialogActions,
  DialogContent,
  DialogTitle,
  FormControlLabel,
  Paper,
  Switch,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from "@mui/material";
import axios from "axios";
import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import * as knowledgeBaseApi from "./knowledgeBaseApi";
import type { KnowledgeBaseCategory } from "./types";

const GENERIC_FAILURE = "Something went wrong. Please try again.";

/** Simple list + a create/rename dialog — categories are lightweight master data, not worth a separate route. */
export function KnowledgeBaseCategoriesPage() {
  const [categories, setCategories] = useState<KnowledgeBaseCategory[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const [dialogTarget, setDialogTarget] = useState<KnowledgeBaseCategory | "new" | null>(null);
  const [name, setName] = useState("");
  const [isActive, setIsActive] = useState(true);
  const [nameError, setNameError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  function reload(): void {
    setLoading(true);
    setError(null);
    knowledgeBaseApi
      .listCategories({ includeInactive: true })
      .then(setCategories)
      .catch(() => setError("Could not load categories. Please try again."))
      .finally(() => setLoading(false));
  }

  useEffect(reload, []);

  function openCreate(): void {
    setName("");
    setIsActive(true);
    setNameError(null);
    setDialogTarget("new");
  }

  function openEdit(category: KnowledgeBaseCategory): void {
    setName(category.name);
    setIsActive(category.isActive);
    setNameError(null);
    setDialogTarget(category);
  }

  async function handleSubmit(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    if (submitting || dialogTarget === null) return;

    const trimmed = name.trim();
    if (trimmed.length === 0) {
      setNameError("Name is required.");
      return;
    }

    setSubmitting(true);
    setNameError(null);

    try {
      if (dialogTarget === "new") {
        await knowledgeBaseApi.createCategory({ name: trimmed });
      } else {
        await knowledgeBaseApi.updateCategory(dialogTarget.id, { name: trimmed, isActive });
      }
      setDialogTarget(null);
      reload();
    } catch (caught: unknown) {
      if (axios.isAxiosError(caught) && caught.response?.status === 409) {
        setNameError("A category with this name already exists.");
      } else {
        setNameError(GENERIC_FAILURE);
      }
    } finally {
      setSubmitting(false);
    }
  }

  async function handleDelete(category: KnowledgeBaseCategory): Promise<void> {
    if (!window.confirm(`Delete "${category.name}"? This cannot be undone.`)) return;

    setError(null);
    try {
      await knowledgeBaseApi.deleteCategory(category.id);
      reload();
    } catch (caught: unknown) {
      if (axios.isAxiosError(caught) && caught.response?.status === 409) {
        setError(`"${category.name}" is still used by one or more knowledge base items and cannot be deleted.`);
      } else {
        setError("Could not delete this category. Please try again.");
      }
    }
  }

  return (
    <Box sx={{ maxWidth: 700 }}>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", gap: 2, mb: 3 }}>
        <Box>
          <Typography variant="h4">Knowledge Base Categories</Typography>
          <Typography variant="body2" color="text.secondary">
            Organize FAQs and Help Articles into categories.
          </Typography>
        </Box>
        <Button variant="contained" startIcon={<AddIcon />} onClick={openCreate}>
          New category
        </Button>
      </Box>

      {error !== null && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      {loading ? (
        <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, py: 4 }}>
          <CircularProgress size={22} />
          <Typography color="text.secondary">Loading…</Typography>
        </Box>
      ) : categories.length === 0 ? (
        <Paper variant="outlined" sx={{ p: 4, textAlign: "center" }}>
          <Typography color="text.secondary">No categories found.</Typography>
        </Paper>
      ) : (
        <TableContainer component={Paper} variant="outlined">
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>Name</TableCell>
                <TableCell>Status</TableCell>
                <TableCell align="right">Actions</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {categories.map((category) => (
                <TableRow key={category.id} hover>
                  <TableCell>{category.name}</TableCell>
                  <TableCell>
                    <Chip
                      label={category.isActive ? "Active" : "Inactive"}
                      color={category.isActive ? "success" : "default"}
                      size="small"
                      variant={category.isActive ? "filled" : "outlined"}
                    />
                  </TableCell>
                  <TableCell align="right">
                    <Box sx={{ display: "flex", gap: 0.5, justifyContent: "flex-end" }}>
                      <Button size="small" onClick={() => openEdit(category)}>
                        Edit
                      </Button>
                      <Button size="small" color="error" onClick={() => void handleDelete(category)}>
                        Delete
                      </Button>
                    </Box>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      <Dialog open={dialogTarget !== null} onClose={() => setDialogTarget(null)} fullWidth maxWidth="xs">
        <Box component="form" onSubmit={handleSubmit} noValidate>
          <DialogTitle>{dialogTarget === "new" ? "New category" : "Edit category"}</DialogTitle>
          <DialogContent>
            <TextField
              autoFocus
              label="Name"
              value={name}
              onChange={(event) => setName(event.target.value)}
              error={nameError !== null}
              helperText={nameError}
              fullWidth
              margin="normal"
            />
            {dialogTarget !== "new" && (
              <FormControlLabel
                control={<Switch checked={isActive} onChange={(event) => setIsActive(event.target.checked)} />}
                label="Active"
              />
            )}
          </DialogContent>
          <DialogActions>
            <Button onClick={() => setDialogTarget(null)}>Cancel</Button>
            <Button type="submit" variant="contained" disabled={submitting}>
              {submitting ? "Saving…" : "Save"}
            </Button>
          </DialogActions>
        </Box>
      </Dialog>
    </Box>
  );
}
