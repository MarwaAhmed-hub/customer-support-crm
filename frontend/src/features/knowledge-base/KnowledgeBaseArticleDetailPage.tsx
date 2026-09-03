import { Alert, Box, Button, Chip, CircularProgress, Paper, Typography } from "@mui/material";
import { useEffect, useState } from "react";
import { useNavigate, useParams } from "react-router-dom";
import { useAuth } from "../auth/useAuth";
import * as knowledgeBaseApi from "./knowledgeBaseApi";
import type { KnowledgeBaseArticle } from "./types";

/**
 * Shared detail view for both FAQs and Help Articles — which one this is comes from the fetched
 * article's own `contentType`, not the route, since both `/knowledge-base/faqs/:id` and
 * `/knowledge-base/articles/:id` resolve to the same underlying table and id space.
 */
export function KnowledgeBaseArticleDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const { hasPermission } = useAuth();
  const canManage = hasPermission("knowledgebase.articles.manage");
  const canPublish = hasPermission("knowledgebase.articles.publish");

  const [article, setArticle] = useState<KnowledgeBaseArticle | null>(null);
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
      .getArticle(id)
      .then((result) => {
        if (!cancelled) setArticle(result);
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
    if (article === null || actioning) return;
    setActioning(true);
    setError(null);

    try {
      const updated = article.status === "Published" ? await knowledgeBaseApi.unpublishArticle(article.id) : await knowledgeBaseApi.publishArticle(article.id);
      setArticle(updated);
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

  if (notFound || article === null) {
    return (
      <Box sx={{ maxWidth: 720 }}>
        <Alert severity="error">This item could not be found.</Alert>
        <Button sx={{ mt: 2 }} onClick={() => navigate("/knowledge-base")}>
          Back to Knowledge Base
        </Button>
      </Box>
    );
  }

  const basePath = article.contentType === "Faq" ? "/knowledge-base/faqs" : "/knowledge-base/articles";
  const kindLabel = article.contentType === "Faq" ? "FAQ" : "Help Article";

  return (
    <Box sx={{ maxWidth: 720 }}>
      <Button sx={{ mb: 2 }} onClick={() => navigate(basePath)}>
        Back to {kindLabel === "FAQ" ? "FAQs" : "Help Articles"}
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
              {kindLabel} · {article.categoryName}
            </Typography>
            <Typography variant="h5">{article.title}</Typography>
          </Box>
          {canManage && (
            <Box sx={{ display: "flex", gap: 1, flexShrink: 0 }}>
              <Chip
                label={article.status}
                color={article.status === "Published" ? "success" : "default"}
                size="small"
                variant={article.status === "Published" ? "filled" : "outlined"}
              />
              <Chip label={article.audience === "Internal" ? "Internal" : "Customer-facing"} size="small" variant="outlined" />
            </Box>
          )}
        </Box>

        <Typography variant="body1" sx={{ whiteSpace: "pre-wrap", mb: 3 }}>
          {article.body}
        </Typography>

        <Box sx={{ display: "flex", gap: 1.5 }}>
          {canManage && (
            <Button variant="outlined" onClick={() => navigate(`${basePath}/${article.id}/edit`)}>
              Edit
            </Button>
          )}
          {canPublish && (
            <Button variant="outlined" disabled={actioning} onClick={() => void handlePublishToggle()}>
              {article.status === "Published" ? "Unpublish" : "Publish"}
            </Button>
          )}
        </Box>
      </Paper>
    </Box>
  );
}
