import {
  Alert,
  Box,
  Chip,
  CircularProgress,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Tab,
  Tabs,
  Typography,
} from "@mui/material";
import type { ChipProps } from "@mui/material";
import { useEffect, useState } from "react";
import { useNavigate } from "react-router-dom";
import * as liveChatApi from "./liveChatApi";
import type { LiveChatSessionListItem, LiveChatStatusValue } from "./types";

const POLL_INTERVAL_MS = 10000;
const ALL_OPTION = "all";

const TABS: { value: LiveChatStatusValue | typeof ALL_OPTION; label: string }[] = [
  { value: ALL_OPTION, label: "All" },
  { value: "Waiting", label: "Waiting" },
  { value: "Active", label: "Active" },
  { value: "Closed", label: "Closed" },
];

function statusChipColor(status: LiveChatStatusValue): ChipProps["color"] {
  switch (status) {
    case "Waiting":
      return "warning";
    case "Active":
      return "info";
    case "Closed":
      return "success";
    default:
      return "default";
  }
}

/**
 * Story 21: agent-facing live chat inbox — `GET /api/live-chat/conversations`, gated `livechat.view`.
 * Polls on an interval rather than pushing over a socket (same "no real-time infra, an abstraction
 * only" philosophy Stories 19/20 established for their own channels).
 */
export function LiveChatInboxPage() {
  const navigate = useNavigate();
  const [tab, setTab] = useState<LiveChatStatusValue | typeof ALL_OPTION>("Waiting");
  const [items, setItems] = useState<LiveChatSessionListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    function load(): void {
      liveChatApi
        .listConversations(tab === ALL_OPTION ? undefined : tab)
        .then((result) => {
          if (!cancelled) setItems(result);
        })
        .catch(() => {
          if (!cancelled) setError("Could not load conversations. Please try again.");
        })
        .finally(() => {
          if (!cancelled) setLoading(false);
        });
    }

    load();
    const timer = window.setInterval(load, POLL_INTERVAL_MS);
    return () => {
      cancelled = true;
      window.clearInterval(timer);
    };
  }, [tab]);

  return (
    <Box sx={{ maxWidth: 1100 }}>
      <Box sx={{ mb: 3 }}>
        <Typography variant="h4">Live Chat</Typography>
        <Typography variant="body2" color="text.secondary">
          Conversations started from the live chat widget.
        </Typography>
      </Box>

      <Paper variant="outlined" sx={{ mb: 2 }}>
        <Tabs value={tab} onChange={(_, value: LiveChatStatusValue | typeof ALL_OPTION) => setTab(value)} sx={{ borderBottom: 1, borderColor: "divider" }}>
          {TABS.map((t) => (
            <Tab key={t.value} value={t.value} label={t.label} />
          ))}
        </Tabs>
      </Paper>

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
      ) : items.length === 0 ? (
        <Paper variant="outlined" sx={{ p: 4, textAlign: "center" }}>
          <Typography color="text.secondary">No conversations here.</Typography>
        </Paper>
      ) : (
        <TableContainer component={Paper} variant="outlined">
          <Table>
            <TableHead>
              <TableRow>
                <TableCell>Customer</TableCell>
                <TableCell>Subject</TableCell>
                <TableCell>Status</TableCell>
                <TableCell>Assignee</TableCell>
                <TableCell>Last message</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {items.map((session) => (
                <TableRow
                  key={session.sessionId}
                  hover
                  onClick={() => navigate(`/agent-desk/live-chat/${session.sessionId}`)}
                  sx={{ cursor: "pointer" }}
                >
                  <TableCell sx={{ fontWeight: 500 }}>{session.customerName}</TableCell>
                  <TableCell>{session.subject}</TableCell>
                  <TableCell>
                    <Chip label={session.status} size="small" color={statusChipColor(session.status)} />
                  </TableCell>
                  <TableCell>
                    {session.assignedUserName ?? (
                      <Typography component="span" variant="body2" color="text.secondary">
                        Unassigned
                      </Typography>
                    )}
                  </TableCell>
                  <TableCell>{new Date(session.lastMessageAt).toLocaleString()}</TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        </TableContainer>
      )}
    </Box>
  );
}
