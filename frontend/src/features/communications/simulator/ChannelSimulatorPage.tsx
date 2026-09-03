import { Alert, Box, Button, Paper, Tab, Tabs, TextField, Typography } from "@mui/material";
import axios from "axios";
import { useState } from "react";
import type { FormEvent, ReactNode } from "react";
import { Link } from "react-router-dom";
import * as publicWebFormApi from "../../public/publicWebFormApi";
import * as channelInboundApi from "./channelInboundApi";
import * as emailIngestionApi from "./emailIngestionApi";
import { SIMULATOR_CHANNELS } from "./types";
import type { SimulationOutcome, SimulatorChannel } from "./types";

const GENERIC_FAILURE = "Something went wrong. Please try again.";

function newRandomId(prefix: string): string {
  return `${prefix}-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
}

/** Shown after a channel's simulated submission succeeds — the same for every implemented channel. */
function ResultPanel({ result, onDismiss }: { result: SimulationOutcome; onDismiss: () => void }) {
  return (
    <Alert severity="success" onClose={onDismiss} sx={{ mt: 2 }}>
      <Typography variant="body2">
        {result.alreadyProcessed === true ? "Already processed — returned the existing ticket." : "Created."}{" "}
        <Link to={`/tickets/${result.ticketId}`}>View ticket</Link> ·{" "}
        <Link to={`/customers/${result.customerId}`}>View customer</Link>
      </Typography>
    </Alert>
  );
}

function EmailChannelForm() {
  const [from, setFrom] = useState("");
  const [to, setTo] = useState("");
  const [subject, setSubject] = useState("");
  const [bodyText, setBodyText] = useState("");
  const [externalMessageId, setExternalMessageId] = useState(() => newRandomId("sim-email"));
  const [inReplyToMessageId, setInReplyToMessageId] = useState("");

  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<SimulationOutcome | null>(null);

  const canSubmit = from.trim().length > 0 && subject.trim().length > 0 && bodyText.trim().length > 0 && externalMessageId.trim().length > 0;

  async function handleSubmit(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    if (submitting || !canSubmit) return;

    setSubmitting(true);
    setError(null);
    setResult(null);

    const payload: emailIngestionApi.IncomingEmailPayload = {
      from: from.trim(),
      subject: subject.trim(),
      bodyText: bodyText.trim(),
      externalMessageId: externalMessageId.trim(),
    };
    if (to.trim().length > 0) payload.to = to.trim();
    if (inReplyToMessageId.trim().length > 0) payload.inReplyToMessageId = inReplyToMessageId.trim();

    try {
      const outcome = await emailIngestionApi.ingestEmail(payload);
      setResult(outcome);
      // A fresh id for the next submission — resubmitting the *same* id on purpose (by editing it
      // back) is exactly how a tester exercises the idempotency path.
      setExternalMessageId(newRandomId("sim-email"));
    } catch (caught: unknown) {
      if (axios.isAxiosError(caught) && caught.response?.status === 400) {
        const code: unknown = caught.response.data?.error;
        setError(typeof code === "string" ? `Rejected: ${code}` : GENERIC_FAILURE);
      } else {
        setError(GENERIC_FAILURE);
      }
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Box component="form" onSubmit={handleSubmit} noValidate>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        Calls <code>POST /api/public/email/ingest</code> — the same find-or-create-customer /
        find-or-link-ticket / write-one-inbound-interaction flow a real inbound email would trigger.
        Resubmitting with the same Message-ID returns the existing ticket instead of duplicating it;
        setting "In-Reply-To" to an earlier message's id threads this into that message's ticket.
      </Typography>

      <TextField label="From" type="email" value={from} onChange={(e) => setFrom(e.target.value)} fullWidth margin="normal" />
      <TextField label="To (optional)" value={to} onChange={(e) => setTo(e.target.value)} fullWidth margin="normal" />
      <TextField label="Subject" value={subject} onChange={(e) => setSubject(e.target.value)} fullWidth margin="normal" />
      <TextField label="Body" value={bodyText} onChange={(e) => setBodyText(e.target.value)} fullWidth multiline minRows={3} margin="normal" />
      <TextField
        label="Message-ID"
        value={externalMessageId}
        onChange={(e) => setExternalMessageId(e.target.value)}
        helperText="A fresh one is pre-filled each time; edit it back to an earlier value to test idempotency."
        fullWidth
        margin="normal"
      />
      <TextField
        label="In-Reply-To (optional)"
        value={inReplyToMessageId}
        onChange={(e) => setInReplyToMessageId(e.target.value)}
        helperText="An earlier message's own Message-ID — links this to that message's ticket instead of opening a new one."
        fullWidth
        margin="normal"
      />

      {error !== null && (
        <Alert severity="error" sx={{ mt: 2 }}>
          {error}
        </Alert>
      )}
      {result !== null && <ResultPanel result={result} onDismiss={() => setResult(null)} />}

      <Button type="submit" variant="contained" disabled={submitting || !canSubmit} sx={{ mt: 2 }}>
        {submitting ? "Sending…" : "Simulate inbound email"}
      </Button>
    </Box>
  );
}

function WebFormChannelForm() {
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [subject, setSubject] = useState("");
  const [description, setDescription] = useState("");
  const [phone, setPhone] = useState("");

  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<SimulationOutcome | null>(null);

  const canSubmit = name.trim().length > 0 && email.trim().length > 0 && subject.trim().length > 0 && description.trim().length > 0;

  async function handleSubmit(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    if (submitting || !canSubmit) return;

    setSubmitting(true);
    setError(null);
    setResult(null);

    const payload: publicWebFormApi.WebFormSubmissionPayload = {
      name: name.trim(),
      email: email.trim(),
      subject: subject.trim(),
      description: description.trim(),
    };
    if (phone.trim().length > 0) payload.phone = phone.trim();

    try {
      const outcome = await publicWebFormApi.submitWebFormTicket(payload);
      setResult(outcome);
    } catch (caught: unknown) {
      if (axios.isAxiosError(caught) && caught.response?.status === 429) {
        setError("Rate-limited (5 submissions/minute/IP) — same limit a real anonymous caller hits. Wait a minute and try again.");
      } else if (axios.isAxiosError(caught) && caught.response?.status === 400) {
        const code: unknown = caught.response.data?.error;
        setError(typeof code === "string" ? `Rejected: ${code}` : GENERIC_FAILURE);
      } else {
        setError(GENERIC_FAILURE);
      }
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Box component="form" onSubmit={handleSubmit} noValidate>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        Calls the exact same anonymous <code>POST /api/public/web-forms/tickets</code> the public{" "}
        <Link to="/support">/support</Link> page uses — including its rate limit (5/minute/IP) and
        honeypot. This form has no honeypot field of its own to trip, since a deliberate tester filling
        this in by hand is not the audience the honeypot defends against.
      </Typography>

      <TextField label="Name" value={name} onChange={(e) => setName(e.target.value)} fullWidth margin="normal" />
      <TextField label="Email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} fullWidth margin="normal" />
      <TextField label="Phone (optional)" value={phone} onChange={(e) => setPhone(e.target.value)} fullWidth margin="normal" />
      <TextField label="Subject" value={subject} onChange={(e) => setSubject(e.target.value)} fullWidth margin="normal" />
      <TextField label="Description" value={description} onChange={(e) => setDescription(e.target.value)} fullWidth multiline minRows={3} margin="normal" />

      {error !== null && (
        <Alert severity="error" sx={{ mt: 2 }}>
          {error}
        </Alert>
      )}
      {result !== null && <ResultPanel result={result} onDismiss={() => setResult(null)} />}

      <Button type="submit" variant="contained" disabled={submitting || !canSubmit} sx={{ mt: 2 }}>
        {submitting ? "Sending…" : "Simulate web form submission"}
      </Button>
    </Box>
  );
}

function PhoneChannelForm({ channel, label, endpointPath }: {
  channel: "whatsapp" | "sms";
  label: string;
  endpointPath: string;
}) {
  // Dereferenced from the namespace object inside handleSubmit below (not captured as a prop here) —
  // CHANNEL_CONTENT is built once at module load, so a function reference bound into props at that
  // point would be captured before a test's vi.spyOn(channelInboundApi, ...) ever replaces it.
  const ingest = channel === "whatsapp" ? channelInboundApi.ingestWhatsApp : channelInboundApi.ingestSms;
  const [fromPhoneNumber, setFromPhoneNumber] = useState("");
  const [toPhoneNumber, setToPhoneNumber] = useState("");
  const [body, setBody] = useState("");
  const [externalMessageId, setExternalMessageId] = useState(() => newRandomId(`sim-${channel}`));
  const [externalConversationId, setExternalConversationId] = useState("");

  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<SimulationOutcome | null>(null);

  const canSubmit = fromPhoneNumber.trim().length > 0 && body.trim().length > 0 && externalMessageId.trim().length > 0;

  async function handleSubmit(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    if (submitting || !canSubmit) return;

    setSubmitting(true);
    setError(null);
    setResult(null);

    const payload: channelInboundApi.InboundMessagePayload = {
      fromPhoneNumber: fromPhoneNumber.trim(),
      body: body.trim(),
      externalMessageId: externalMessageId.trim(),
    };
    if (toPhoneNumber.trim().length > 0) payload.toPhoneNumber = toPhoneNumber.trim();
    if (externalConversationId.trim().length > 0) payload.externalConversationId = externalConversationId.trim();

    try {
      const outcome = await ingest(payload);
      setResult({ ticketId: outcome.ticketId, customerId: outcome.customerId, alreadyProcessed: outcome.deduplicated });
      // A fresh id for the next submission — resubmitting the *same* id on purpose (by editing it
      // back) is exactly how a tester exercises the idempotency path.
      setExternalMessageId(newRandomId(`sim-${channel}`));
    } catch (caught: unknown) {
      if (axios.isAxiosError(caught) && caught.response?.status === 400) {
        const code: unknown = caught.response.data?.error;
        setError(typeof code === "string" ? `Rejected: ${code}` : GENERIC_FAILURE);
      } else {
        setError(GENERIC_FAILURE);
      }
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <Box component="form" onSubmit={handleSubmit} noValidate>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
        Calls <code>POST {endpointPath}</code> — the same find-or-create-customer /
        find-or-create-ticket / write-one-inbound-interaction flow a real inbound {label} message
        would trigger. The same phone number always reuses the same customer. A message with a
        Conversation ID matching a still-open ticket for that same customer and channel is added to
        that ticket instead of opening a new one; with no Conversation ID (or one that matches nothing
        open), a new ticket opens every time. Resubmitting the same Message-ID is a separate case — a
        provider retry, not a second message — and always returns the existing ticket without writing
        anything new.
      </Typography>

      <TextField label="From (phone number)" value={fromPhoneNumber} onChange={(e) => setFromPhoneNumber(e.target.value)} fullWidth margin="normal" />
      <TextField label="To (optional)" value={toPhoneNumber} onChange={(e) => setToPhoneNumber(e.target.value)} fullWidth margin="normal" />
      <TextField label="Body" value={body} onChange={(e) => setBody(e.target.value)} fullWidth multiline minRows={3} margin="normal" />
      <TextField
        label="Message-ID"
        value={externalMessageId}
        onChange={(e) => setExternalMessageId(e.target.value)}
        helperText="A fresh one is pre-filled each time; edit it back to an earlier value to test idempotency."
        fullWidth
        margin="normal"
      />
      <TextField
        label="Conversation ID (optional)"
        value={externalConversationId}
        onChange={(e) => setExternalConversationId(e.target.value)}
        helperText="A stable provider thread id. Reuse the same value on a second submission (same phone number, still-open ticket) to route it into that same ticket instead of opening a new one."
        fullWidth
        margin="normal"
      />

      {error !== null && (
        <Alert severity="error" sx={{ mt: 2 }}>
          {error}
        </Alert>
      )}
      {result !== null && <ResultPanel result={result} onDismiss={() => setResult(null)} />}

      <Button type="submit" variant="contained" disabled={submitting || !canSubmit} sx={{ mt: 2 }}>
        {submitting ? "Sending…" : `Simulate inbound ${label}`}
      </Button>
    </Box>
  );
}

function LiveChatChannelNote() {
  return (
    <Alert severity="info">
      Live Chat (Story 21) has no "ingest" to simulate — unlike Email/WhatsApp/SMS there is no
      external provider standing in the way, so the widget itself is already the real, live
      customer-facing entry point. Open <Link to="/live-chat">/live-chat</Link> in another tab to
      start a conversation, then answer it from the <Link to="/agent-desk/live-chat">agent inbox</Link>.
    </Alert>
  );
}

const CHANNEL_CONTENT: Record<SimulatorChannel, ReactNode> = {
  email: <EmailChannelForm />,
  webform: <WebFormChannelForm />,
  whatsapp: <PhoneChannelForm channel="whatsapp" label="WhatsApp" endpointPath="/api/public/channels/whatsapp/inbound" />,
  sms: <PhoneChannelForm channel="sms" label="SMS" endpointPath="/api/public/channels/sms/inbound" />,
  livechat: <LiveChatChannelNote />,
};

/**
 * Dev/test-only tool: pick a channel, fill in what that channel would carry, and submit — this calls
 * the exact same backend services a real inbound message would (`POST /api/public/email/ingest`,
 * `POST /api/public/web-forms/tickets`, `POST /api/public/channels/{whatsapp|sms}/inbound`), never a
 * parallel simulation path. It owns no business logic of its own and changes nothing about which Story
 * owns what; Live Chat (Story 21) has no ingest to simulate here since its widget already is the real,
 * live entry point — its tab just links out to it instead of rendering a form.
 *
 * Anonymous, same as every endpoint above (correction — see `Program.cs`'s rate limiter comment):
 * every one of these represents a customer submitting something, never a staff member, so neither this
 * page nor the endpoints it calls require a login.
 */
export function ChannelSimulatorPage() {
  const [channel, setChannel] = useState<SimulatorChannel>("email");

  return (
    <Box sx={{ maxWidth: 720 }}>
      <Typography variant="h4" sx={{ mb: 1 }}>
        Channel Simulator
      </Typography>
      <Typography variant="body2" color="text.secondary" sx={{ mb: 3 }}>
        Dev/test tool — simulates an inbound message on a channel without a real mailbox/WhatsApp/SMS
        provider. No login needed, same as every real customer-facing entry point. Every submission
        below is a real write through the same code a production message would go through; nothing here
        is mocked.
      </Typography>

      <Paper variant="outlined">
        <Tabs value={channel} onChange={(_, value: SimulatorChannel) => setChannel(value)} sx={{ borderBottom: 1, borderColor: "divider" }}>
          {SIMULATOR_CHANNELS.map((c) => (
            <Tab key={c.value} value={c.value} label={c.label} />
          ))}
        </Tabs>
        <Box sx={{ p: 3 }}>{CHANNEL_CONTENT[channel]}</Box>
      </Paper>
    </Box>
  );
}
