import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import SendIcon from "@mui/icons-material/Send";
import { Alert, Avatar, Box, Button, Chip, CircularProgress, IconButton, Paper, TextField, Typography } from "@mui/material";
import type { ChipProps } from "@mui/material";
import axios from "axios";
import { useEffect, useRef, useState } from "react";
import type { KeyboardEvent } from "react";
import { Link, useParams } from "react-router-dom";
import { useAuth } from "../auth/useAuth";
import { QuickReplyPicker } from "../quick-replies/QuickReplyPicker";
import * as liveChatApi from "./liveChatApi";
import type { LiveChatSessionDetail, LiveChatStatusValue } from "./types";

const POLL_INTERVAL_MS = 4000;

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
 * Story 21: agent-facing conversation view — `GET/POST /api/live-chat/conversations/{id}`.
 * Closing/reopening is not done here: it is the linked ticket's own status transition, reached via
 * "View ticket" — same design decision as the backend `LiveChatController`.
 */
export function LiveChatConversationPage() {
  const { id } = useParams<{ id: string }>();
  const { hasPermission } = useAuth();
  const canSend = hasPermission("livechat.send");

  const [session, setSession] = useState<LiveChatSessionDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [notFound, setNotFound] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [draft, setDraft] = useState("");
  const [sending, setSending] = useState(false);
  const [sendError, setSendError] = useState<string | null>(null);

  const messagesEndRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    if (id === undefined) return;

    let cancelled = false;

    function load(): void {
      liveChatApi
        .getConversation(id!)
        .then((current) => {
          if (cancelled) return;
          setSession(current);
          setNotFound(false);
        })
        .catch((caught: unknown) => {
          if (cancelled) return;
          if (axios.isAxiosError(caught) && caught.response?.status === 404) {
            setNotFound(true);
          } else {
            setError("Could not load this conversation. Please try again.");
          }
        })
        .finally(() => {
          if (!cancelled) setLoading(false);
        });
    }

    setLoading(true);
    setNotFound(false);
    setError(null);
    load();
    const timer = window.setInterval(load, POLL_INTERVAL_MS);
    return () => {
      cancelled = true;
      window.clearInterval(timer);
    };
  }, [id]);

  useEffect(() => {
    // jsdom (unit tests) has no scrollIntoView implementation at all — guard rather than polyfill.
    messagesEndRef.current?.scrollIntoView?.({ behavior: "smooth" });
  }, [session?.messages.length]);

  async function handleSend(): Promise<void> {
    if (session === null || sending || draft.trim().length === 0) return;

    setSending(true);
    setSendError(null);

    try {
      const message = await liveChatApi.sendReply(session.sessionId, draft.trim());
      setSession((current) => (current === null ? current : { ...current, messages: [...current.messages, message] }));
      setDraft("");
    } catch (caught: unknown) {
      if (axios.isAxiosError(caught) && caught.response?.status === 409) {
        setSendError("This conversation has been closed.");
      } else if (axios.isAxiosError(caught) && caught.response?.status === 400) {
        setSendError("The reply cannot be empty.");
      } else {
        setSendError("Could not send this reply. Please try again.");
      }
    } finally {
      setSending(false);
    }
  }

  function handleDraftKeyDown(event: KeyboardEvent<HTMLDivElement>): void {
    if (event.key === "Enter" && !event.shiftKey) {
      event.preventDefault();
      void handleSend();
    }
  }

  const backLink = (
    <Button component={Link} to="/agent-desk/live-chat" startIcon={<ArrowBackIcon />} size="small" sx={{ mb: 2 }}>
      Back to live chat
    </Button>
  );

  if (loading) {
    return (
      <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, py: 4 }}>
        <CircularProgress size={22} />
        <Typography color="text.secondary">Loading…</Typography>
      </Box>
    );
  }

  if (notFound) {
    return (
      <Box sx={{ maxWidth: 900 }}>
        <Alert severity="warning" sx={{ mb: 2 }}>
          Conversation not found.
        </Alert>
        {backLink}
      </Box>
    );
  }

  if (error !== null && session === null) {
    return (
      <Box sx={{ maxWidth: 900 }}>
        <Alert severity="error">{error}</Alert>
        {backLink}
      </Box>
    );
  }

  if (session === null) {
    return null;
  }

  const isClosed = session.status === "Closed";

  return (
    <Box sx={{ maxWidth: 900 }}>
      {backLink}

      <Box sx={{ display: "flex", flexWrap: "wrap", justifyContent: "space-between", alignItems: "flex-start", gap: 2, mb: 2.5 }}>
        <Box>
          <Typography variant="h4" component="h1">
            {session.customerName}
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.25 }}>
            {session.subject}
          </Typography>
        </Box>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, flexWrap: "wrap" }}>
          <Chip label={session.status} color={statusChipColor(session.status)} sx={{ fontWeight: 600 }} />
          <Button component={Link} to={`/tickets/${session.ticketId}`} variant="outlined">
            View ticket
          </Button>
        </Box>
      </Box>

      {error !== null && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      <Paper variant="outlined" sx={{ display: "flex", flexDirection: "column", height: 560 }}>
        <Box sx={{ flex: 1, overflowY: "auto", p: 2, display: "flex", flexDirection: "column", gap: 1.5 }}>
          {session.messages.map((message) => {
            const isAgent = message.sender === "Agent";
            return (
              <Box key={message.id} sx={{ display: "flex", justifyContent: isAgent ? "flex-end" : "flex-start", gap: 1 }}>
                {!isAgent && (
                  <Avatar sx={{ width: 28, height: 28, fontSize: 13 }}>
                    {session.customerName.charAt(0).toUpperCase()}
                  </Avatar>
                )}
                <Box
                  sx={{
                    maxWidth: "75%",
                    px: 1.5,
                    py: 1,
                    borderRadius: 2,
                    bgcolor: isAgent ? "primary.main" : "action.hover",
                    color: isAgent ? "primary.contrastText" : "text.primary",
                  }}
                >
                  {isAgent && (
                    <Typography variant="caption" sx={{ display: "block", fontWeight: 600, mb: 0.25 }}>
                      {message.senderName ?? "Agent"}
                    </Typography>
                  )}
                  <Typography variant="body2" sx={{ whiteSpace: "pre-wrap", overflowWrap: "break-word" }}>
                    {message.body}
                  </Typography>
                </Box>
              </Box>
            );
          })}
          <div ref={messagesEndRef} />
        </Box>

        {canSend && (
          <Box sx={{ borderTop: "1px solid", borderColor: "divider", p: 2 }}>
            {sendError !== null && (
              <Alert severity="error" sx={{ mb: 1.5 }}>
                {sendError}
              </Alert>
            )}
            <Box sx={{ display: "flex", justifyContent: "flex-end", mb: 1 }}>
              <QuickReplyPicker onInsert={(body) => setDraft((current) => (current.length > 0 ? `${current}\n${body}` : body))} />
            </Box>
            <Box sx={{ display: "flex", gap: 1, alignItems: "flex-end" }}>
              <TextField
                value={draft}
                onChange={(event) => setDraft(event.target.value)}
                onKeyDown={handleDraftKeyDown}
                placeholder={isClosed ? "This conversation is closed." : "Type a reply…"}
                fullWidth
                multiline
                maxRows={4}
                size="small"
                disabled={isClosed}
              />
              <IconButton
                color="primary"
                onClick={() => void handleSend()}
                disabled={sending || draft.trim().length === 0 || isClosed}
                aria-label="Send"
              >
                <SendIcon />
              </IconButton>
            </Box>
          </Box>
        )}
      </Paper>
    </Box>
  );
}
