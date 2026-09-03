import { Alert, Box, Button, CircularProgress, FormControlLabel, MenuItem, Paper, Radio, RadioGroup, TextField, Typography } from "@mui/material";
import axios from "axios";
import { useEffect, useState } from "react";
import type { FormEvent } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { useAuth } from "../auth/useAuth";
import * as knowledgeBaseApi from "./knowledgeBaseApi";
import type { KnowledgeBaseAudience, KnowledgeBaseCategory, KnowledgeBasePublicationStatus } from "./types";

const GENERIC_FAILURE = "Something went wrong. Please try again.";

interface FieldErrors {
  title?: string;
  problem?: string;
  solutionBody?: string;
  categoryId?: string;
}

export function SolutionFormPage() {
  const { id } = useParams<{ id: string }>();
  const isEdit = id !== undefined;
  const navigate = useNavigate();
  const { hasPermission } = useAuth();
  const canPublish = hasPermission("knowledgebase.solutions.publish");

  const [title, setTitle] = useState("");
  const [problem, setProblem] = useState("");
  const [solutionBody, setSolutionBody] = useState("");
  const [audience, setAudience] = useState<KnowledgeBaseAudience>("CustomerFacing");
  const [categoryId, setCategoryId] = useState("");
  const [categories, setCategories] = useState<KnowledgeBaseCategory[]>([]);
  const [status, setStatus] = useState<KnowledgeBasePublicationStatus | null>(null);

  const [loading, setLoading] = useState(isEdit);
  const [submitting, setSubmitting] = useState(false);
  const [publishing, setPublishing] = useState(false);
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [formError, setFormError] = useState<string | null>(null);

  useEffect(() => {
    knowledgeBaseApi
      .listCategories()
      .then(setCategories)
      .catch(() => undefined);
  }, []);

  useEffect(() => {
    if (!isEdit || id === undefined) return;

    let cancelled = false;
    knowledgeBaseApi
      .getSolution(id)
      .then((solution) => {
        if (cancelled) return;
        setTitle(solution.title);
        setProblem(solution.problem);
        setSolutionBody(solution.solutionBody);
        setAudience(solution.audience);
        setCategoryId(solution.categoryId);
        setStatus(solution.status);
      })
      .catch(() => {
        if (!cancelled) setFormError("Could not load this solution. Please try again.");
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

    if (title.trim().length === 0) errors.title = "Title is required.";
    if (problem.trim().length === 0) errors.problem = "Problem is required.";
    if (solutionBody.trim().length === 0) errors.solutionBody = "Solution is required.";
    if (categoryId.length === 0) errors.categoryId = "Category is required.";

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
        await knowledgeBaseApi.updateSolution(id, {
          title: title.trim(),
          problem: problem.trim(),
          solutionBody: solutionBody.trim(),
          categoryId,
          audience,
        });
      } else {
        await knowledgeBaseApi.createSolution({
          title: title.trim(),
          problem: problem.trim(),
          solutionBody: solutionBody.trim(),
          categoryId,
          audience,
        });
      }
      navigate("/knowledge-base/solutions", { replace: true });
    } catch (caught: unknown) {
      if (axios.isAxiosError(caught) && caught.response?.status === 404) {
        setFieldErrors((current) => ({ ...current, categoryId: "This category no longer exists." }));
      } else {
        setFormError(GENERIC_FAILURE);
      }
    } finally {
      setSubmitting(false);
    }
  }

  async function handlePublishToggle(): Promise<void> {
    if (id === undefined || publishing) return;
    setPublishing(true);
    setFormError(null);

    try {
      const updated = status === "Published" ? await knowledgeBaseApi.unpublishSolution(id) : await knowledgeBaseApi.publishSolution(id);
      setStatus(updated.status);
    } catch {
      setFormError("Could not update this item's publication status. Please try again.");
    } finally {
      setPublishing(false);
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
    <Box sx={{ maxWidth: 640 }}>
      <Typography variant="h4" sx={{ mb: 3 }}>
        {isEdit ? "Edit solution" : "New solution"}
      </Typography>

      <Paper component="form" onSubmit={handleSubmit} noValidate variant="outlined" sx={{ p: 3 }}>
        {formError !== null && (
          <Alert severity="error" sx={{ mb: 2 }}>
            {formError}
          </Alert>
        )}

        {isEdit && status !== null && (
          <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
            Status: <strong>{status}</strong>
          </Typography>
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
          id="problem"
          label="Problem"
          value={problem}
          onChange={(event) => setProblem(event.target.value)}
          error={fieldErrors.problem !== undefined}
          helperText={fieldErrors.problem}
          fullWidth
          multiline
          minRows={2}
          margin="normal"
        />

        <TextField
          id="solutionBody"
          label="Solution"
          value={solutionBody}
          onChange={(event) => setSolutionBody(event.target.value)}
          error={fieldErrors.solutionBody !== undefined}
          helperText={fieldErrors.solutionBody}
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
        >
          {categories.map((category) => (
            <MenuItem key={category.id} value={category.id}>
              {category.name}
            </MenuItem>
          ))}
        </TextField>

        <Typography variant="body2" sx={{ mt: 2, mb: 0.5 }}>
          Audience
        </Typography>
        <RadioGroup row value={audience} onChange={(event) => setAudience(event.target.value as KnowledgeBaseAudience)}>
          <FormControlLabel value="CustomerFacing" control={<Radio />} label="Customer-facing" />
          <FormControlLabel value="Internal" control={<Radio />} label="Internal" />
        </RadioGroup>

        <Box sx={{ display: "flex", gap: 1.5, mt: 3 }}>
          <Button type="submit" variant="contained" disabled={submitting}>
            {submitting ? "Saving…" : "Save"}
          </Button>
          <Button variant="text" onClick={() => navigate("/knowledge-base/solutions")}>
            Cancel
          </Button>
          {isEdit && canPublish && status !== null && (
            <Button variant="outlined" disabled={publishing} onClick={() => void handlePublishToggle()}>
              {status === "Published" ? "Unpublish" : "Publish"}
            </Button>
          )}
        </Box>
      </Paper>
    </Box>
  );
}
