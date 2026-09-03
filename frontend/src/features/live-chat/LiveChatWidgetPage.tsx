import SendIcon from "@mui/icons-material/Send";
import { Alert, Avatar, Box, Button, IconButton, Paper, TextField, Typography } from "@mui/material";
import axios from "axios";
import { useEffect, useRef, useState } from "react";
import type { FormEvent, KeyboardEvent } from "react";
import * as publicLiveChatApi from "./publicLiveChatApi";
import type { LiveChatMessage, LiveChatStatusValue, StartLiveChatSessionPayload } from "./types";

const GENERIC_FAILURE = "Something went wrong. Please try again.";
const RATE_LIMITED = "Too many requests — please wait a minute and try again.";
const POLL_INTERVAL_MS = 4000;

// A refresh mid-conversation should not orphan the customer from their own chat — the session
// survives a reload the same way an inbox tab would, scoped to this browser tab only.
const STORAGE_KEY = "livechat.session";

interface StoredSession {
  sessionId: string;
  sessionToken: string;
}

function loadStoredSession(): StoredSession | null {
  try {
    const raw = window.sessionStorage.getItem(STORAGE_KEY);
    if (raw === null) return null;
    const parsed: unknown = JSON.parse(raw);
    if (
      typeof parsed === "object" &&
      parsed !== null &&
      typeof (parsed as StoredSession).sessionId === "string" &&
      typeof (parsed as StoredSession).sessionToken === "string"
    ) {
      return parsed as StoredSession;
    }
    return null;
  } catch {
    return null;
  }
}

function storeSession(session: StoredSession | null): void {
  try {
    if (session === null) {
      window.sessionStorage.removeItem(STORAGE_KEY);
    } else {
      window.sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session));
    }
  } catch {
    // Best-effort only — a private-browsing tab with storage disabled just loses persistence across reload.
  }
}

/**
 * Story 21: the public, unauthenticated live chat widget — `POST /api/public/live-chat/sessions`
 * starts a conversation, then this page polls `GET .../messages` and posts replies via
 * `POST .../messages`. Rendered standalone with no `AppLayout`, same pattern as `SupportRequestPage`
 * (Story 19) and `LoginPage`. Unlike Email/WhatsApp/SMS there is no external provider to simulate —
 * this widget *is* the real, live customer-facing entry point, not a dev/test stand-in.
 */
export function LiveChatWidgetPage() {
  const [session, setSession] = useState<StoredSession | null>(() => loadStoredSession());
  const [status, setStatus] = useState<LiveChatStatusValue | null>(null);
  const [messages, setMessages] = useState<LiveChatMessage[]>([]);

  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [phone, setPhone] = useState("");
  const [firstMessage, setFirstMessage] = useState("");
  const [starting, setStarting] = useState(false);
  const [startError, setStartError] = useState<string | null>(null);

  const [draft, setDraft] = useState("");
  const [sending, setSending] = useState(false);
  const [sendError, setSendError] = useState<string | null>(null);
  const [pollError, setPollError] = useState<string | null>(null);

  const messagesEndRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    if (session === null) return;

    let cancelled = false;

    function poll(): void {
      if (session === null) return;
      publicLiveChatApi
        .getSession(session.sessionId, session.sessionToken)
        .then((current) => {
          if (cancelled) return;
          setStatus(current.status);
          setMessages(current.messages);
          setPollError(null);
        })
        .catch((caught: unknown) => {
          if (cancelled) return;
          if (axios.isAxiosError(caught) && caught.response?.status === 403) {
            // The stored token no longer matches (e.g. cleared/reset server-side) — drop it so the
            // visitor gets a fresh start screen instead of a silently-stuck widget.
            storeSession(null);
            setSession(null);
          } else {
            setPollError("Could not refresh this conversation.");
          }
        });
    }

    poll();
    const timer = window.setInterval(poll, POLL_INTERVAL_MS);
    return () => {
      cancelled = true;
      window.clearInterval(timer);
    };
  }, [session]);

  useEffect(() => {
    // jsdom (unit tests) has no scrollIntoView implementation at all — guard rather than polyfill.
    messagesEndRef.current?.scrollIntoView?.({ behavior: "smooth" });
  }, [messages]);

  async function handleStart(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    if (starting || firstMessage.trim().length === 0) return;

    setStarting(true);
    setStartError(null);

    const payload: StartLiveChatSessionPayload = { message: firstMessage.trim() };
    const trimmedName = name.trim();
    const trimmedEmail = email.trim();
    const trimmedPhone = phone.trim();
    if (trimmedName.length > 0) payload.name = trimmedName;
    if (trimmedEmail.length > 0) payload.email = trimmedEmail;
    if (trimmedPhone.length > 0) payload.phone = trimmedPhone;

    try {
      const result = await publicLiveChatApi.startSession(payload);
      const started: StoredSession = { sessionId: result.sessionId, sessionToken: result.sessionToken };
      storeSession(started);
      setSession(started);
      setStatus(result.status);
    } catch (caught: unknown) {
      if (axios.isAxiosError(caught) && caught.response?.status === 429) {
        setStartError(RATE_LIMITED);
      } else {
        setStartError(GENERIC_FAILURE);
      }
    } finally {
      setStarting(false);
    }
  }

  async function handleSend(): Promise<void> {
    if (session === null || sending || draft.trim().length === 0) return;

    setSending(true);
    setSendError(null);

    try {
      const message = await publicLiveChatApi.sendMessage(session.sessionId, session.sessionToken, draft.trim());
      setMessages((current) => [...current, message]);
      setDraft("");
    } catch (caught: unknown) {
      if (axios.isAxiosError(caught) && caught.response?.status === 409) {
        setSendError("This conversation has been closed.");
      } else {
        setSendError(GENERIC_FAILURE);
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

  // A closed conversation has nowhere else to go — without this, a returning visitor whose earlier
  // chat was closed by an agent would be stuck re-reading that same transcript forever, with no way
  // back to the start form (the stored session always takes priority over it).
  function handleStartNew(): void {
    storeSession(null);
    setSession(null);
    setStatus(null);
    setMessages([]);
    setFirstMessage("");
    setDraft("");
  }

  if (session === null) {
    return (
      <Box component="main" sx={{ display: "flex", alignItems: "center", justifyContent: "center", minHeight: "100vh", p: 2, bgcolor: "background.default" }}>
        <Paper component="form" onSubmit={handleStart} noValidate elevation={0} sx={{ width: "100%", maxWidth: 480, p: 4, border: "1px solid", borderColor: "divider" }}>
          <Typography variant="h5" sx={{ mb: 0.5 }}>
            Chat with us
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
            Send a message and an agent will join the conversation shortly.
          </Typography>

          {startError !== null && (
            <Alert severity="error" sx={{ mb: 2 }}>
              {startError}
            </Alert>
          )}

          <TextField label="Your name (optional)" value={name} onChange={(event) => setName(event.target.value)} fullWidth margin="normal" />
          <TextField label="Email (optional)" type="email" autoComplete="email" value={email} onChange={(event) => setEmail(event.target.value)} fullWidth margin="normal" />
          <TextField label="Phone (optional)" value={phone} onChange={(event) => setPhone(event.target.value)} fullWidth margin="normal" />
          <TextField
            label="How can we help?"
            value={firstMessage}
            onChange={(event) => setFirstMessage(event.target.value)}
            fullWidth
            multiline
            minRows={3}
            margin="normal"
          />

          <Button type="submit" variant="contained" fullWidth disabled={starting || firstMessage.trim().length === 0} sx={{ mt: 2, py: 1.1 }}>
            {starting ? "Starting…" : "Start chat"}
          </Button>
        </Paper>
      </Box>
    );
  }

  return (
    <Box component="main" sx={{ display: "flex", alignItems: "center", justifyContent: "center", minHeight: "100vh", p: 2, bgcolor: "background.default" }}>
      <Paper elevation={0} sx={{ width: "100%", maxWidth: 480, height: "min(640px, 90vh)", display: "flex", flexDirection: "column", border: "1px solid", borderColor: "divider" }}>
        <Box sx={{ px: 3, py: 2, borderBottom: "1px solid", borderColor: "divider", display: "flex", justifyContent: "space-between", alignItems: "center" }}>
          <Typography variant="h6">Live Chat</Typography>
          <Typography variant="caption" color="text.secondary">
            {status === "Closed" ? "Closed" : status === "Active" ? "Agent connected" : "Waiting for an agent…"}
          </Typography>
        </Box>

        {pollError !== null && (
          <Alert severity="warning" sx={{ mx: 2, mt: 1.5 }}>
            {pollError}
          </Alert>
        )}

        <Box sx={{ flex: 1, overflowY: "auto", p: 2, display: "flex", flexDirection: "column", gap: 1.5 }}>
          {messages.map((message) => {
            const isCustomer = message.sender === "Customer";
            return (
              <Box key={message.id} sx={{ display: "flex", justifyContent: isCustomer ? "flex-end" : "flex-start", gap: 1 }}>
                {!isCustomer && (
                  <Avatar sx={{ width: 28, height: 28, fontSize: 13, bgcolor: "primary.main" }}>
                    {(message.senderName ?? "A").charAt(0).toUpperCase()}
                  </Avatar>
                )}
                <Box
                  sx={{
                    maxWidth: "75%",
                    px: 1.5,
                    py: 1,
                    borderRadius: 2,
                    bgcolor: isCustomer ? "primary.main" : "action.hover",
                    color: isCustomer ? "primary.contrastText" : "text.primary",
                  }}
                >
                  {!isCustomer && (
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

        {sendError !== null && (
          <Alert severity="error" sx={{ mx: 2, mb: 1 }}>
            {sendError}
          </Alert>
        )}

        {status === "Closed" ? (
          <Box sx={{ p: 2, borderTop: "1px solid", borderColor: "divider", textAlign: "center" }}>
            <Typography variant="body2" color="text.secondary" sx={{ mb: 1.5 }}>
              This conversation has been closed.
            </Typography>
            <Button variant="contained" onClick={handleStartNew}>
              Start a new conversation
            </Button>
          </Box>
        ) : (
          <Box sx={{ p: 2, borderTop: "1px solid", borderColor: "divider", display: "flex", gap: 1, alignItems: "flex-end" }}>
            <TextField
              value={draft}
              onChange={(event) => setDraft(event.target.value)}
              onKeyDown={handleDraftKeyDown}
              placeholder="Type a message…"
              fullWidth
              multiline
              maxRows={4}
              size="small"
            />
            <IconButton color="primary" onClick={() => void handleSend()} disabled={sending || draft.trim().length === 0} aria-label="Send">
              <SendIcon />
            </IconButton>
          </Box>
        )}
      </Paper>
    </Box>
  );
}
