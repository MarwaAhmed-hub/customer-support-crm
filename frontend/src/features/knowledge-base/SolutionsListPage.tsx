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
import type { KbSolution, KnowledgeBaseAudience, KnowledgeBaseCategory, KnowledgeBasePublicationStatus } from "./types";

const ALL_OPTION = "";

export function SolutionsListPage() {
  const { hasPermission } = useAuth();
  const navigate = useNavigate();
  const canManage = hasPermission("knowledgebase.solutions.manage");
  const canPublish = hasPermission("knowledgebase.solutions.publish");

  const [solutions, setSolutions] = useState<KbSolution[]>([]);
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

    const params: knowledgeBaseApi.ListSolutionsParams = {};
    if (categoryFilter !== ALL_OPTION) params.categoryId = categoryFilter;
    if (canManage && statusFilter !== ALL_OPTION) params.status = statusFilter;
    if (canManage && audienceFilter !== ALL_OPTION) params.audience = audienceFilter;

    knowledgeBaseApi
      .listSolutions(params)
      .then((result) => {
        if (!cancelled) setSolutions(result);
      })
      .catch(() => {
        if (!cancelled) setError("Could not load solutions. Please try again.");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [categoryFilter, statusFilter, audienceFilter, canManage]);

  async function handlePublishToggle(solution: KbSolution): Promise<void> {
    if (actioningId !== null) return;
    setActioningId(solution.id);
    setError(null);

    try {
      const updated = solution.status === "Published" ? await knowledgeBaseApi.unpublishSolution(solution.id) : await knowledgeBaseApi.publishSolution(solution.id);
      setSolutions((current) => current.map((s) => (s.id === updated.id ? updated : s)));
    } catch {
      setError("Could not update this item's publication status. Please try again.");
    } finally {
      setActioningId(null);
    }
  }

  async function handleDelete(solution: KbSolution): Promise<void> {
    if (actioningId !== null) return;
    if (!window.confirm(`Delete "${solution.title}"? This cannot be undone.`)) return;

    setActioningId(solution.id);
    setError(null);

    try {
      await knowledgeBaseApi.deleteSolution(solution.id);
      setSolutions((current) => current.filter((s) => s.id !== solution.id));
    } catch {
      setError("Could not delete this solution. Please try again.");
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
          <Typography variant="h4">Solutions</Typography>
          <Typography variant="body2" color="text.secondary">
            Known problems and their fixes.
          </Typography>
        </Box>
        {canManage && (
          <Button component={Link} to="/knowledge-base/solutions/new" variant="contained" startIcon={<AddIcon />}>
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
      ) : solutions.length === 0 ? (
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
                {canManage && <TableCell>Audience</TableCell>}
                {canManage && <TableCell>Status</TableCell>}
                <TableCell align="right">Actions</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {solutions.map((solution) => (
                <TableRow key={solution.id} hover>
                  <TableCell
                    sx={{ fontWeight: 500, cursor: "pointer" }}
                    onClick={() => navigate(`/knowledge-base/solutions/${solution.id}`)}
                  >
                    {solution.title}
                  </TableCell>
                  <TableCell>{solution.categoryName}</TableCell>
                  {canManage && (
                    <TableCell>
                      <Chip label={solution.audience === "Internal" ? "Internal" : "Customer-facing"} size="small" variant="outlined" />
                    </TableCell>
                  )}
                  {canManage && (
                    <TableCell>
                      <Chip
                        label={solution.status}
                        color={solution.status === "Published" ? "success" : "default"}
                        size="small"
                        variant={solution.status === "Published" ? "filled" : "outlined"}
                      />
                    </TableCell>
                  )}
                  <TableCell align="right">
                    <Box sx={{ display: "flex", gap: 0.5, justifyContent: "flex-end" }}>
                      <Button size="small" onClick={() => navigate(`/knowledge-base/solutions/${solution.id}`)}>
                        View
                      </Button>
                      {canManage && (
                        <Button size="small" onClick={() => navigate(`/knowledge-base/solutions/${solution.id}/edit`)}>
                          Edit
                        </Button>
                      )}
                      {canPublish && (
                        <Button size="small" disabled={actioningId === solution.id} onClick={() => void handlePublishToggle(solution)}>
                          {solution.status === "Published" ? "Unpublish" : "Publish"}
                        </Button>
                      )}
                      {canManage && (
                        <Button size="small" color="error" disabled={actioningId === solution.id} onClick={() => void handleDelete(solution)}>
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
