import { Alert, Box, Button, Chip, CircularProgress, MenuItem, Paper, TextField, ToggleButton, ToggleButtonGroup, Typography } from "@mui/material";
import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import * as knowledgeBaseApi from "../knowledgeBaseApi";
import type { KnowledgeBaseCategory } from "../types";
import * as knowledgeBaseSearchApi from "./knowledgeBaseSearchApi";
import type { KnowledgeBaseSearchContentType, KnowledgeBaseSearchResultItem } from "./types";

const PAGE_SIZE = 20;
const ALL_TYPES: KnowledgeBaseSearchContentType[] = ["Faq", "Article", "Solution", "Guide"];
const NO_CATEGORY = "";

const TYPE_LABEL: Record<KnowledgeBaseSearchContentType, string> = {
  Faq: "FAQ",
  Article: "Help Article",
  Solution: "Solution",
  Guide: "Guide",
};

function detailPath(item: KnowledgeBaseSearchResultItem): string {
  switch (item.type) {
    case "Faq":
      return `/knowledge-base/faqs/${item.id}`;
    case "Article":
      return `/knowledge-base/articles/${item.id}`;
    case "Solution":
      return `/knowledge-base/solutions/${item.id}`;
    case "Guide":
      return `/knowledge-base/guides/${item.id}`;
  }
}

export function KnowledgeBaseSearchPage() {
  const navigate = useNavigate();

  const [q, setQ] = useState("");
  const [types, setTypes] = useState<KnowledgeBaseSearchContentType[]>([]);
  const [categoryId, setCategoryId] = useState(NO_CATEGORY);
  const [categories, setCategories] = useState<KnowledgeBaseCategory[]>([]);
  const [page, setPage] = useState(1);

  const [items, setItems] = useState<KnowledgeBaseSearchResultItem[]>([]);
  const [total, setTotal] = useState(0);
  const [hasSearched, setHasSearched] = useState(false);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    knowledgeBaseApi
      .listCategories()
      .then(setCategories)
      .catch(() => undefined);
  }, []);

  const trimmedQ = q.trim();
  const hasAnyFilter = trimmedQ.length > 0 || types.length > 0 || categoryId !== NO_CATEGORY;

  useEffect(() => {
    if (!hasAnyFilter) {
      setItems([]);
      setTotal(0);
      setHasSearched(false);
      setLoading(false);
      return;
    }

    let cancelled = false;
    setLoading(true);
    setError(null);

    const params: knowledgeBaseSearchApi.SearchKnowledgeBaseParams = { page, pageSize: PAGE_SIZE };
    if (trimmedQ.length > 0) params.q = trimmedQ;
    if (types.length > 0) params.type = types;
    if (categoryId !== NO_CATEGORY) params.categoryId = categoryId;

    const timer = window.setTimeout(() => {
      knowledgeBaseSearchApi
        .searchKnowledgeBase(params)
        .then((result) => {
          if (cancelled) return;
          setItems(result.items);
          setTotal(result.total);
          setHasSearched(true);
        })
        .catch(() => {
          if (!cancelled) setError("Could not search the knowledge base. Please try again.");
        })
        .finally(() => {
          if (!cancelled) setLoading(false);
        });
    }, 300);

    return () => {
      cancelled = true;
      window.clearTimeout(timer);
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps -- hasAnyFilter/trimmedQ are derived from q; including them alongside q would re-run the effect twice per keystroke.
  }, [q, types, categoryId, page]);

  function toggleType(type: KnowledgeBaseSearchContentType): void {
    setPage(1);
    setTypes((current) => (current.includes(type) ? current.filter((t) => t !== type) : [...current, type]));
  }

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));

  return (
    <Box sx={{ maxWidth: 900 }}>
      <Typography variant="h4" sx={{ mb: 1 }}>
        Knowledge Base Search
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
        Search across FAQs, Help Articles, Solutions, and Guides.
      </Typography>

      <TextField
        label="Search"
        value={q}
        onChange={(event) => {
          setPage(1);
          setQ(event.target.value);
        }}
        fullWidth
        sx={{ mb: 2 }}
      />

      <Box sx={{ display: "flex", flexWrap: "wrap", gap: 2, mb: 3, alignItems: "center" }}>
        <ToggleButtonGroup size="small" value={types}>
          {ALL_TYPES.map((type) => (
            <ToggleButton key={type} value={type} selected={types.includes(type)} onClick={() => toggleType(type)}>
              {TYPE_LABEL[type]}
            </ToggleButton>
          ))}
        </ToggleButtonGroup>

        <TextField
          select
          label="Category"
          size="small"
          value={categoryId}
          onChange={(event) => {
            setPage(1);
            setCategoryId(event.target.value);
          }}
          sx={{ minWidth: 200 }}
        >
          <MenuItem value={NO_CATEGORY}>All categories</MenuItem>
          {categories.map((category) => (
            <MenuItem key={category.id} value={category.id}>
              {category.name}
            </MenuItem>
          ))}
        </TextField>
      </Box>

      {error !== null && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      {loading ? (
        <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, py: 4 }}>
          <CircularProgress size={22} />
          <Typography color="text.secondary">Searching…</Typography>
        </Box>
      ) : !hasAnyFilter ? (
        <Paper variant="outlined" sx={{ p: 4, textAlign: "center" }}>
          <Typography color="text.secondary">Start typing, or pick a type/category, to search the knowledge base.</Typography>
        </Paper>
      ) : hasSearched && items.length === 0 ? (
        <Paper variant="outlined" sx={{ p: 4, textAlign: "center" }}>
          <Typography color="text.secondary">No results found.</Typography>
        </Paper>
      ) : (
        <Box sx={{ display: "flex", flexDirection: "column", gap: 1.5 }}>
          {items.map((item) => (
            <Paper
              key={`${item.type}-${item.id}`}
              variant="outlined"
              sx={{ p: 2, cursor: "pointer", "&:hover": { borderColor: "primary.main" } }}
              onClick={() => navigate(detailPath(item))}
            >
              <Box sx={{ display: "flex", alignItems: "center", gap: 1, mb: 0.5, flexWrap: "wrap" }}>
                <Chip label={TYPE_LABEL[item.type]} size="small" color="primary" variant="outlined" />
                {item.categoryName !== null && <Chip label={item.categoryName} size="small" variant="outlined" />}
                <Typography variant="subtitle1" sx={{ fontWeight: 500 }}>
                  {item.title}
                </Typography>
              </Box>
              <Typography variant="body2" color="text.secondary">
                {item.excerpt}
              </Typography>
            </Paper>
          ))}
        </Box>
      )}

      {!loading && hasSearched && total > 0 && (
        <Box sx={{ display: "flex", alignItems: "center", justifyContent: "center", gap: 2, mt: 3 }}>
          <Button variant="outlined" size="small" disabled={page <= 1} onClick={() => setPage((current) => Math.max(1, current - 1))}>
            Previous
          </Button>
          <Typography variant="body2" color="text.secondary">
            Page {page} of {totalPages} ({total} result{total === 1 ? "" : "s"})
          </Typography>
          <Button variant="outlined" size="small" disabled={page >= totalPages} onClick={() => setPage((current) => current + 1)}>
            Next
          </Button>
        </Box>
      )}
    </Box>
  );
}
