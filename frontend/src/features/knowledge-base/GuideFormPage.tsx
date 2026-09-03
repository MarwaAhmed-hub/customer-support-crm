import AddIcon from "@mui/icons-material/Add";
import ArrowDownwardIcon from "@mui/icons-material/ArrowDownward";
import ArrowUpwardIcon from "@mui/icons-material/ArrowUpward";
import DeleteOutlineIcon from "@mui/icons-material/DeleteOutlined";
import {
  Alert,
  Box,
  Button,
  FormControlLabel,
  IconButton,
  MenuItem,
  Paper,
  Radio,
  RadioGroup,
  Stack,
  TextField,
  Typography,
  CircularProgress,
} from "@mui/material";
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
  description?: string;
  categoryId?: string;
  steps?: string;
}

export function GuideFormPage() {
  const { id } = useParams<{ id: string }>();
  const isEdit = id !== undefined;
  const navigate = useNavigate();
  const { hasPermission } = useAuth();
  const canPublish = hasPermission("knowledgebase.guides.publish");

  const [title, setTitle] = useState("");
  const [description, setDescription] = useState("");
  const [audience, setAudience] = useState<KnowledgeBaseAudience>("CustomerFacing");
  const [categoryId, setCategoryId] = useState("");
  const [categories, setCategories] = useState<KnowledgeBaseCategory[]>([]);
  const [steps, setSteps] = useState<string[]>([""]);
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
      .getGuide(id)
      .then((guide) => {
        if (cancelled) return;
        setTitle(guide.title);
        setDescription(guide.description);
        setAudience(guide.audience);
        setCategoryId(guide.categoryId);
        setSteps(guide.steps.length > 0 ? guide.steps.map((s) => s.instruction) : [""]);
        setStatus(guide.status);
      })
      .catch(() => {
        if (!cancelled) setFormError("Could not load this guide. Please try again.");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [isEdit, id]);

  function updateStep(index: number, value: string): void {
    setSteps((current) => current.map((step, i) => (i === index ? value : step)));
  }

  function addStep(): void {
    setSteps((current) => [...current, ""]);
  }

  function removeStep(index: number): void {
    setSteps((current) => (current.length <= 1 ? current : current.filter((_, i) => i !== index)));
  }

  function moveStep(index: number, direction: -1 | 1): void {
    setSteps((current) => {
      const target = index + direction;
      if (target < 0 || target >= current.length) return current;
      const next = [...current];
      [next[index], next[target]] = [next[target]!, next[index]!];
      return next;
    });
  }

  function validate(): FieldErrors {
    const errors: FieldErrors = {};

    if (title.trim().length === 0) errors.title = "Title is required.";
    if (description.trim().length === 0) errors.description = "Description is required.";
    if (categoryId.length === 0) errors.categoryId = "Category is required.";
    if (steps.every((step) => step.trim().length === 0)) errors.steps = "At least one step is required.";

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

    const stepPayload = steps
      .map((step) => step.trim())
      .filter((step) => step.length > 0)
      .map((instruction) => ({ instruction }));

    try {
      if (isEdit && id !== undefined) {
        await knowledgeBaseApi.updateGuide(id, {
          title: title.trim(),
          description: description.trim(),
          categoryId,
          audience,
          steps: stepPayload,
        });
      } else {
        await knowledgeBaseApi.createGuide({
          title: title.trim(),
          description: description.trim(),
          categoryId,
          audience,
          steps: stepPayload,
        });
      }
      navigate("/knowledge-base/guides", { replace: true });
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
      const updated = status === "Published" ? await knowledgeBaseApi.unpublishGuide(id) : await knowledgeBaseApi.publishGuide(id);
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
        {isEdit ? "Edit guide" : "New guide"}
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

        <Typography variant="body2" sx={{ mt: 2, mb: 1 }}>
          Steps
        </Typography>
        <Stack spacing={1}>
          {steps.map((step, index) => (
            // eslint-disable-next-line react/no-array-index-key -- steps have no stable id; reordering is by array position, which the key must track.
            <Box key={index} sx={{ display: "flex", alignItems: "center", gap: 1 }}>
              <TextField
                label={`Step ${index + 1}`}
                value={step}
                onChange={(event) => updateStep(index, event.target.value)}
                fullWidth
                size="small"
              />
              <IconButton aria-label={`Move step ${index + 1} up`} size="small" disabled={index === 0} onClick={() => moveStep(index, -1)}>
                <ArrowUpwardIcon fontSize="small" />
              </IconButton>
              <IconButton
                aria-label={`Move step ${index + 1} down`}
                size="small"
                disabled={index === steps.length - 1}
                onClick={() => moveStep(index, 1)}
              >
                <ArrowDownwardIcon fontSize="small" />
              </IconButton>
              <IconButton aria-label={`Remove step ${index + 1}`} size="small" disabled={steps.length <= 1} onClick={() => removeStep(index)}>
                <DeleteOutlineIcon fontSize="small" />
              </IconButton>
            </Box>
          ))}
        </Stack>
        {fieldErrors.steps !== undefined && (
          <Typography variant="caption" color="error" sx={{ display: "block", mt: 0.5 }}>
            {fieldErrors.steps}
          </Typography>
        )}
        <Button size="small" startIcon={<AddIcon />} onClick={addStep} sx={{ mt: 1 }}>
          Add step
        </Button>

        <Box sx={{ display: "flex", gap: 1.5, mt: 3 }}>
          <Button type="submit" variant="contained" disabled={submitting}>
            {submitting ? "Saving…" : "Save"}
          </Button>
          <Button variant="text" onClick={() => navigate("/knowledge-base/guides")}>
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
