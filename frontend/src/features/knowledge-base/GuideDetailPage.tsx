import { Alert, Box, Button, Chip, CircularProgress, Paper, Typography } from "@mui/material";
import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { useAuth } from "../auth/useAuth";
import * as knowledgeBaseApi from "./knowledgeBaseApi";
import type { KbGuide } from "./types";

export function GuideDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { hasPermission } = useAuth();
  const canManage = hasPermission("knowledgebase.guides.manage");
  const canPublish = hasPermission("knowledgebase.guides.publish");

  const [guide, setGuide] = useState<KbGuide | null>(null);
  const [loading, setLoading] = useState(true);
  const [notFound, setNotFound] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [actioning, setActioning] = useState(false);

  useEffect(() => {
    if (id === undefined) return;

    let cancelled = false;
    setLoading(true);
    setNotFound(false);
    setError(null);

    knowledgeBaseApi
      .getGuide(id)
      .then((result) => {
        if (!cancelled) setGuide(result);
      })
      .catch(() => {
        if (!cancelled) setNotFound(true);
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [id]);

  async function handlePublishToggle(): Promise<void> {
    if (guide === null || actioning) return;
    setActioning(true);
    setError(null);

    try {
      const updated = guide.status === "Published" ? await knowledgeBaseApi.unpublishGuide(guide.id) : await knowledgeBaseApi.publishGuide(guide.id);
      setGuide(updated);
    } catch {
      setError("Could not update this item's publication status. Please try again.");
    } finally {
      setActioning(false);
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

  if (notFound || guide === null) {
    return (
      <Box sx={{ maxWidth: 720 }}>
        <Alert severity="error">This guide could not be found.</Alert>
        <Button sx={{ mt: 2 }} onClick={() => navigate("/knowledge-base")}>
          Back to Knowledge Base
        </Button>
      </Box>
    );
  }

  const orderedSteps = [...guide.steps].sort((a, b) => a.order - b.order);

  return (
    <Box sx={{ maxWidth: 720 }}>
      <Button sx={{ mb: 2 }} onClick={() => navigate("/knowledge-base/guides")}>
        Back to Guides
      </Button>

      {error !== null && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      <Paper variant="outlined" sx={{ p: 3 }}>
        <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "flex-start", gap: 2, mb: 2 }}>
          <Box>
            <Typography variant="overline" color="text.secondary">
              Guide · {guide.categoryName}
            </Typography>
            <Typography variant="h5">{guide.title}</Typography>
          </Box>
          {canManage && (
            <Box sx={{ display: "flex", gap: 1, flexShrink: 0 }}>
              <Chip
                label={guide.status}
                color={guide.status === "Published" ? "success" : "default"}
                size="small"
                variant={guide.status === "Published" ? "filled" : "outlined"}
              />
              <Chip label={guide.audience === "Internal" ? "Internal" : "Customer-facing"} size="small" variant="outlined" />
            </Box>
          )}
        </Box>

        <Typography variant="body1" sx={{ whiteSpace: "pre-wrap", mb: 3 }}>
          {guide.description}
        </Typography>

        <Typography variant="subtitle2" color="text.secondary" sx={{ mb: 1 }}>
          Steps
        </Typography>
        <Box component="ol" sx={{ m: 0, pl: 3, display: "flex", flexDirection: "column", gap: 1.5 }}>
          {orderedSteps.map((step) => (
            <Box component="li" key={step.order}>
              <Typography variant="body1">{step.instruction}</Typography>
            </Box>
          ))}
        </Box>

        <Box sx={{ display: "flex", gap: 1.5, mt: 3 }}>
          {canManage && (
            <Button variant="outlined" onClick={() => navigate(`/knowledge-base/guides/${guide.id}/edit`)}>
              Edit
            </Button>
          )}
          {canPublish && (
            <Button variant="outlined" disabled={actioning} onClick={() => void handlePublishToggle()}>
              {guide.status === "Published" ? "Unpublish" : "Publish"}
            </Button>
          )}
        </Box>
      </Paper>
    </Box>
  );
}
