import { Alert, Box, Button, Chip, CircularProgress, Paper, Typography } from "@mui/material";
import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { useAuth } from "../auth/useAuth";
import * as knowledgeBaseApi from "./knowledgeBaseApi";
import type { KbSolution } from "./types";

export function SolutionDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { hasPermission } = useAuth();
  const canManage = hasPermission("knowledgebase.solutions.manage");
  const canPublish = hasPermission("knowledgebase.solutions.publish");

  const [solution, setSolution] = useState<KbSolution | null>(null);
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
      .getSolution(id)
      .then((result) => {
        if (!cancelled) setSolution(result);
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
    if (solution === null || actioning) return;
    setActioning(true);
    setError(null);

    try {
      const updated = solution.status === "Published" ? await knowledgeBaseApi.unpublishSolution(solution.id) : await knowledgeBaseApi.publishSolution(solution.id);
      setSolution(updated);
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

  if (notFound || solution === null) {
    return (
      <Box sx={{ maxWidth: 720 }}>
        <Alert severity="error">This solution could not be found.</Alert>
        <Button sx={{ mt: 2 }} onClick={() => navigate("/knowledge-base")}>
          Back to Knowledge Base
        </Button>
      </Box>
    );
  }

  return (
    <Box sx={{ maxWidth: 720 }}>
      <Button sx={{ mb: 2 }} onClick={() => navigate("/knowledge-base/solutions")}>
        Back to Solutions
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
              Solution · {solution.categoryName}
            </Typography>
            <Typography variant="h5">{solution.title}</Typography>
          </Box>
          {canManage && (
            <Box sx={{ display: "flex", gap: 1, flexShrink: 0 }}>
              <Chip
                label={solution.status}
                color={solution.status === "Published" ? "success" : "default"}
                size="small"
                variant={solution.status === "Published" ? "filled" : "outlined"}
              />
              <Chip label={solution.audience === "Internal" ? "Internal" : "Customer-facing"} size="small" variant="outlined" />
            </Box>
          )}
        </Box>

        <Typography variant="subtitle2" color="text.secondary">
          Problem
        </Typography>
        <Typography variant="body1" sx={{ whiteSpace: "pre-wrap", mb: 2 }}>
          {solution.problem}
        </Typography>

        <Typography variant="subtitle2" color="text.secondary">
          Solution
        </Typography>
        <Typography variant="body1" sx={{ whiteSpace: "pre-wrap", mb: 3 }}>
          {solution.solutionBody}
        </Typography>

        <Box sx={{ display: "flex", gap: 1.5 }}>
          {canManage && (
            <Button variant="outlined" onClick={() => navigate(`/knowledge-base/solutions/${solution.id}/edit`)}>
              Edit
            </Button>
          )}
          {canPublish && (
            <Button variant="outlined" disabled={actioning} onClick={() => void handlePublishToggle()}>
              {solution.status === "Published" ? "Unpublish" : "Publish"}
            </Button>
          )}
        </Box>
      </Paper>
    </Box>
  );
}
