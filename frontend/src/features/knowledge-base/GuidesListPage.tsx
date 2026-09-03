import AddIcon from "@mui/icons-material/Add";
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
  MenuItem,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  TextField,
  Typography,
} from "@mui/material";
import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useAuth } from "../auth/useAuth";
import * as knowledgeBaseApi from "./knowledgeBaseApi";
import type { KbGuide, KnowledgeBaseAudience, KnowledgeBaseCategory, KnowledgeBasePublicationStatus } from "./types";

const ALL_OPTION = "";

export function GuidesListPage() {
  const { hasPermission } = useAuth();
  const navigate = useNavigate();
  const canManage = hasPermission("knowledgebase.guides.manage");
  const canPublish = hasPermission("knowledgebase.guides.publish");

  const [guides, setGuides] = useState<KbGuide[]>([]);
  const [categories, setCategories] = useState<KnowledgeBaseCategory[]>([]);
  const [categoryFilter, setCategoryFilter] = useState(ALL_OPTION);
  const [statusFilter, setStatusFilter] = useState<KnowledgeBasePublicationStatus | typeof ALL_OPTION>(ALL_OPTION);
  const [audienceFilter, setAudienceFilter] = useState<KnowledgeBaseAudience | typeof ALL_OPTION>(ALL_OPTION);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actioningId, setActioningId] = useState<string | null>(null);

  useEffect(() => {
    knowledgeBaseApi
      .listCategories()
      .then(setCategories)
      .catch(() => undefined);
  }, []);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    const params: knowledgeBaseApi.ListGuidesParams = {};
    if (categoryFilter !== ALL_OPTION) params.categoryId = categoryFilter;
    if (canManage && statusFilter !== ALL_OPTION) params.status = statusFilter;
    if (canManage && audienceFilter !== ALL_OPTION) params.audience = audienceFilter;

    knowledgeBaseApi
      .listGuides(params)
      .then((result) => {
        if (!cancelled) setGuides(result);
      })
      .catch(() => {
        if (!cancelled) setError("Could not load guides. Please try again.");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [categoryFilter, statusFilter, audienceFilter, canManage]);

  async function handlePublishToggle(guide: KbGuide): Promise<void> {
    if (actioningId !== null) return;
    setActioningId(guide.id);
    setError(null);

    try {
      const updated = guide.status === "Published" ? await knowledgeBaseApi.unpublishGuide(guide.id) : await knowledgeBaseApi.publishGuide(guide.id);
      setGuides((current) => current.map((g) => (g.id === updated.id ? updated : g)));
    } catch {
      setError("Could not update this item's publication status. Please try again.");
    } finally {
      setActioningId(null);
    }
  }

  async function handleDelete(guide: KbGuide): Promise<void> {
    if (actioningId !== null) return;
    if (!window.confirm(`Delete "${guide.title}"? This cannot be undone.`)) return;

    setActioningId(guide.id);
    setError(null);

    try {
      await knowledgeBaseApi.deleteGuide(guide.id);
      setGuides((current) => current.filter((g) => g.id !== guide.id));
    } catch {
      setError("Could not delete this guide. Please try again.");
    } finally {
      setActioningId(null);
    }
  }

  return (
    <Box sx={{ maxWidth: 1100 }}>
      <Box
        sx={{
          display: "flex",
          flexDirection: { xs: "column", sm: "row" },
          justifyContent: "space-between",
          alignItems: { sm: "center" },
          gap: 2,
          mb: 3,
        }}
      >
        <Box>
          <Typography variant="h4">Guides</Typography>
          <Typography variant="body2" color="text.secondary">
            Step-by-step walkthroughs.
          </Typography>
        </Box>
        {canManage && (
          <Button component={Link} to="/knowledge-base/guides/new" variant="contained" startIcon={<AddIcon />}>
            New
          </Button>
        )}
      </Box>

      <Box sx={{ display: "flex", flexWrap: "wrap", gap: 2, mb: 2 }}>
        <TextField
          select
          label="Category"
          size="small"
          value={categoryFilter}
          onChange={(event) => setCategoryFilter(event.target.value)}
          sx={{ minWidth: 200 }}
        >
          <MenuItem value={ALL_OPTION}>All categories</MenuItem>
          {categories.map((category) => (
            <MenuItem key={category.id} value={category.id}>
              {category.name}
            </MenuItem>
          ))}
        </TextField>

        {canManage && (
          <TextField
            select
            label="Status"
            size="small"
            value={statusFilter}
            onChange={(event) => setStatusFilter(event.target.value as KnowledgeBasePublicationStatus | typeof ALL_OPTION)}
            sx={{ minWidth: 160 }}
          >
            <MenuItem value={ALL_OPTION}>All statuses</MenuItem>
            <MenuItem value="Draft">Draft</MenuItem>
            <MenuItem value="Published">Published</MenuItem>
          </TextField>
        )}

        {canManage && (
          <TextField
            select
            label="Audience"
            size="small"
            value={audienceFilter}
            onChange={(event) => setAudienceFilter(event.target.value as KnowledgeBaseAudience | typeof ALL_OPTION)}
            sx={{ minWidth: 180 }}
          >
            <MenuItem value={ALL_OPTION}>All audiences</MenuItem>
            <MenuItem value="CustomerFacing">Customer-facing</MenuItem>
            <MenuItem value="Internal">Internal</MenuItem>
          </TextField>
        )}
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
      ) : guides.length === 0 ? (
        <Paper variant="outlined" sx={{ p: 4, textAlign: "center" }}>
          <Typography color="text.secondary">Nothing here yet.</Typography>
        </Paper>
      ) : (
        <TableContainer component={Paper} variant="outlined">
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>Title</TableCell>
                <TableCell>Category</TableCell>
                <TableCell>Steps</TableCell>
                {canManage && <TableCell>Audience</TableCell>}
                {canManage && <TableCell>Status</TableCell>}
                <TableCell align="right">Actions</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {guides.map((guide) => (
                <TableRow key={guide.id} hover>
                  <TableCell
                    sx={{ fontWeight: 500, cursor: "pointer" }}
                    onClick={() => navigate(`/knowledge-base/guides/${guide.id}`)}
                  >
                    {guide.title}
                  </TableCell>
                  <TableCell>{guide.categoryName}</TableCell>
                  <TableCell>{guide.steps.length}</TableCell>
                  {canManage && (
                    <TableCell>
                      <Chip label={guide.audience === "Internal" ? "Internal" : "Customer-facing"} size="small" variant="outlined" />
                    </TableCell>
                  )}
                  {canManage && (
                    <TableCell>
                      <Chip
                        label={guide.status}
                        color={guide.status === "Published" ? "success" : "default"}
                        size="small"
                        variant={guide.status === "Published" ? "filled" : "outlined"}
                      />
                    </TableCell>
                  )}
                  <TableCell align="right">
                    <Box sx={{ display: "flex", gap: 0.5, justifyContent: "flex-end" }}>
                      <Button size="small" onClick={() => navigate(`/knowledge-base/guides/${guide.id}`)}>
                        View
                      </Button>
                      {canManage && (
                        <Button size="small" onClick={() => navigate(`/knowledge-base/guides/${guide.id}/edit`)}>
                          Edit
                        </Button>
                      )}
                      {canPublish && (
                        <Button size="small" disabled={actioningId === guide.id} onClick={() => void handlePublishToggle(guide)}>
                          {guide.status === "Published" ? "Unpublish" : "Publish"}
                        </Button>
                      )}
                      {canManage && (
                        <Button size="small" color="error" disabled={actioningId === guide.id} onClick={() => void handleDelete(guide)}>
                          Delete
                        </Button>
                      )}
                    </Box>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Box>
  );
}
