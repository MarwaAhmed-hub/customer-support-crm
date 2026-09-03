import AddIcon from "@mui/icons-material/Add";
import {
  Alert,
  Box,
  Button,
  Chip,
  CircularProgress,
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
import { useEffect, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useAuth } from "../auth/useAuth";
import * as quickRepliesApi from "./quickRepliesApi";
import type { QuickReply } from "./types";

export function QuickRepliesListPage() {
  const { hasPermission } = useAuth();
  const navigate = useNavigate();
  const canManage = hasPermission("quickreplies.manage");

  const [quickReplies, setQuickReplies] = useState<QuickReply[]>([]);
  const [showInactive, setShowInactive] = useState(false);
  const [search, setSearch] = useState("");
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [actioningId, setActioningId] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    // Debounced so every keystroke in the search box doesn't fire a request. exactOptionalPropertyTypes
    // forbids `search: undefined`, so the key is omitted entirely rather than present-but-undefined.
    const params: quickRepliesApi.ListQuickRepliesParams = { includeInactive: showInactive };
    const trimmedSearch = search.trim();
    if (trimmedSearch.length > 0) params.search = trimmedSearch;

    const timer = window.setTimeout(() => {
      quickRepliesApi
        .listQuickReplies(params)
        .then((result) => {
          if (!cancelled) setQuickReplies(result);
        })
        .catch(() => {
          if (!cancelled) setError("Could not load quick replies. Please try again.");
        })
        .finally(() => {
          if (!cancelled) setLoading(false);
        });
    }, 300);

    return () => {
      cancelled = true;
      window.clearTimeout(timer);
    };
  }, [showInactive, search]);

  async function handleDelete(quickReply: QuickReply): Promise<void> {
    if (actioningId !== null) return;
    if (!window.confirm(`Delete "${quickReply.title}"? This cannot be undone.`)) return;

    setActioningId(quickReply.id);
    setError(null);

    try {
      await quickRepliesApi.deleteQuickReply(quickReply.id);
      setQuickReplies((current) => current.filter((q) => q.id !== quickReply.id));
    } catch {
      setError("Could not delete this quick reply. Please try again.");
    } finally {
      setActioningId(null);
    }
  }

  return (
    <Box sx={{ maxWidth: 1000 }}>
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
          <Typography variant="h4">Quick Replies</Typography>
          <Typography variant="body2" color="text.secondary">
            Reusable response templates you can insert into a ticket reply.
          </Typography>
        </Box>
        {canManage && (
          <Button component={Link} to="/quick-replies/new" variant="contained" startIcon={<AddIcon />}>
            New quick reply
          </Button>
        )}
      </Box>

      <Box sx={{ display: "flex", flexDirection: { xs: "column", sm: "row" }, gap: 2, alignItems: { sm: "center" }, mb: 2 }}>
        <TextField
          label="Search"
          size="small"
          value={search}
          onChange={(event) => setSearch(event.target.value)}
          sx={{ minWidth: 260 }}
        />
        {canManage && (
          <FormControlLabel
            control={<Switch checked={showInactive} onChange={(event) => setShowInactive(event.target.checked)} />}
            label="Show inactive"
          />
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
      ) : quickReplies.length === 0 ? (
        <Paper variant="outlined" sx={{ p: 4, textAlign: "center" }}>
          <Typography color="text.secondary">No quick replies found.</Typography>
        </Paper>
      ) : (
        <TableContainer component={Paper} variant="outlined">
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>Title</TableCell>
                <TableCell>Body</TableCell>
                {canManage && <TableCell>Status</TableCell>}
                <TableCell>Updated</TableCell>
                {canManage && <TableCell align="right">Actions</TableCell>}
              </TableRow>
            </TableHead>
            <TableBody>
              {quickReplies.map((quickReply) => (
                <TableRow key={quickReply.id} hover>
                  <TableCell sx={{ fontWeight: 500 }}>{quickReply.title}</TableCell>
                  <TableCell sx={{ maxWidth: 360, overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" }}>
                    {quickReply.body}
                  </TableCell>
                  {canManage && (
                    <TableCell>
                      <Chip
                        label={quickReply.isActive ? "Active" : "Inactive"}
                        color={quickReply.isActive ? "success" : "default"}
                        size="small"
                        variant={quickReply.isActive ? "filled" : "outlined"}
                      />
                    </TableCell>
                  )}
                  <TableCell>{new Date(quickReply.updatedAt).toLocaleDateString()}</TableCell>
                  {canManage && (
                    <TableCell align="right">
                      <Box sx={{ display: "flex", gap: 0.5, justifyContent: "flex-end" }}>
                        <Button size="small" onClick={() => navigate(`/quick-replies/${quickReply.id}/edit`)}>
                          Edit
                        </Button>
                        <Button
                          size="small"
                          color="error"
                          disabled={actioningId === quickReply.id}
                          onClick={() => void handleDelete(quickReply)}
                        >
                          Delete
                        </Button>
                      </Box>
                    </TableCell>
                  )}
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Box>
  );
}
