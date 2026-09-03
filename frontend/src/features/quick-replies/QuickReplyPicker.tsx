import ContentPasteIcon from "@mui/icons-material/ContentPaste";
import {
  Box,
  Button,
  CircularProgress,
  List,
  ListItemButton,
  ListItemText,
  Popover,
  TextField,
  Typography,
} from "@mui/material";
import { useEffect, useState } from "react";
import type { MouseEvent } from "react";
import * as quickRepliesApi from "./quickRepliesApi";
import type { QuickReply } from "./types";

/**
 * Picker button + popover listing active quick replies with search. Selecting one calls
 * `onInsert(body)` and closes — it never sends anything itself, matching the story's
 * "insert, agent edits, never auto-send" intent.
 */
export function QuickReplyPicker({ onInsert }: { onInsert: (body: string) => void }) {
  const [anchorEl, setAnchorEl] = useState<HTMLElement | null>(null);
  const [quickReplies, setQuickReplies] = useState<QuickReply[]>([]);
  const [search, setSearch] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const open = anchorEl !== null;

  useEffect(() => {
    if (!open) return;

    let cancelled = false;
    setLoading(true);
    setError(null);

    const params: quickRepliesApi.ListQuickRepliesParams = {};
    const trimmedSearch = search.trim();
    if (trimmedSearch.length > 0) params.search = trimmedSearch;

    const timer = window.setTimeout(() => {
      quickRepliesApi
        .listQuickReplies(params)
        .then((result) => {
          if (!cancelled) setQuickReplies(result);
        })
        .catch(() => {
          if (!cancelled) setError("Could not load quick replies.");
        })
        .finally(() => {
          if (!cancelled) setLoading(false);
        });
    }, 250);

    return () => {
      cancelled = true;
      window.clearTimeout(timer);
    };
  }, [open, search]);

  function handleOpen(event: MouseEvent<HTMLElement>): void {
    setAnchorEl(event.currentTarget);
    setSearch("");
  }

  function handleClose(): void {
    setAnchorEl(null);
  }

  function handleSelect(quickReply: QuickReply): void {
    onInsert(quickReply.body);
    handleClose();
  }

  return (
    <>
      <Button size="small" variant="outlined" startIcon={<ContentPasteIcon />} onClick={handleOpen}>
        Quick reply
      </Button>
      <Popover
        open={open}
        anchorEl={anchorEl}
        onClose={handleClose}
        anchorOrigin={{ vertical: "bottom", horizontal: "left" }}
      >
        <Box sx={{ width: 360, p: 1.5 }}>
          <TextField
            autoFocus
            size="small"
            placeholder="Search quick replies…"
            fullWidth
            value={search}
            onChange={(event) => setSearch(event.target.value)}
          />

          <Box sx={{ mt: 1, maxHeight: 320, overflowY: "auto" }}>
            {loading ? (
              <Box sx={{ display: "flex", alignItems: "center", gap: 1, py: 2, px: 1 }}>
                <CircularProgress size={16} />
                <Typography variant="body2" color="text.secondary">
                  Loading…
                </Typography>
              </Box>
            ) : error !== null ? (
              <Typography variant="body2" color="error" sx={{ p: 1 }}>
                {error}
              </Typography>
            ) : quickReplies.length === 0 ? (
              <Typography variant="body2" color="text.secondary" sx={{ p: 1 }}>
                No quick replies found.
              </Typography>
            ) : (
              <List dense disablePadding>
                {quickReplies.map((quickReply) => (
                  <ListItemButton key={quickReply.id} onClick={() => handleSelect(quickReply)} sx={{ alignItems: "flex-start" }}>
                    <ListItemText
                      primary={quickReply.title}
                      secondary={quickReply.body}
                      slotProps={{
                        secondary: {
                          sx: {
                            overflow: "hidden",
                            textOverflow: "ellipsis",
                            display: "-webkit-box",
                            WebkitLineClamp: 2,
                            WebkitBoxOrient: "vertical",
                          },
                        },
                      }}
                    />
                  </ListItemButton>
                ))}
              </List>
            )}
          </Box>
        </Box>
      </Popover>
    </>
  );
}
