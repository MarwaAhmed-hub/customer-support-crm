import ArrowBackIcon from "@mui/icons-material/ArrowBack";
import { Alert, Box, Button, Card, CardContent, Chip, CircularProgress, MenuItem, TextField, Typography } from "@mui/material";
import axios from "axios";
import { useEffect, useState } from "react";
import type { ReactNode } from "react";
import { Link, useLocation, useNavigate, useParams } from "react-router-dom";
import { TicketTasksPanel } from "../../agent-desk/tasks/TicketTasksPanel";
import { useAuth } from "../../auth/useAuth";
import { CustomerInteractionHistory } from "../../customers/interactions/CustomerInteractionHistory";
import { QuickReplyPicker } from "../../quick-replies/QuickReplyPicker";
import * as usersApi from "../../users/usersApi";
import type { UserListItem } from "../../users/types";
import { TicketCollaborationPanel } from "./collaboration/TicketCollaborationPanel";
import { TicketHistoryPanel } from "./history/TicketHistoryPanel";
import * as ticketsApi from "./ticketsApi";
import type { TicketDetail, TicketStatus } from "./types";
import { TICKET_STATUS_TRANSITIONS } from "./types";
import { escalationLabel, priorityChipColor, slaStatusChipColor, slaStatusLabel, statusChipColor, statusLabel } from "./ticketDisplay";

const STATUS_ERROR_MESSAGES: Record<string, string> = {
  invalid_status: "That is not a recognised status.",
  invalid_status_transition: "This ticket cannot move to that status from its current one.",
};

const ESCALATE_ERROR_MESSAGES: Record<string, string> = {
  invalid_reason: "A reason is required to escalate this ticket.",
  already_escalated: "This ticket is already escalated.",
};

const DEESCALATE_ERROR_MESSAGES: Record<string, string> = {
  not_escalated: "This ticket is not currently escalated.",
};

/** Story 19/20: both reply endpoints share this — keyed by the `error` code either one can return. */
const REPLY_ERROR_MESSAGES: Record<string, string> = {
  not_email_channel: "This ticket did not come in by email, so a reply can't be sent this way.",
  customer_has_no_email: "This customer has no email address on file.",
  not_sendable_channel: "This ticket's channel doesn't support replying this way.",
  no_recipient: "There is no phone number to reply to for this ticket.",
  invalid_body: "The reply cannot be empty.",
  email_send_failed: "The email could not be sent. Please try again.",
  provider_failed: "The message could not be sent. Please try again.",
};

function extractErrorCode(caught: unknown): string | undefined {
  if (axios.isAxiosError(caught) && caught.response?.status === 400) {
    const code: unknown = caught.response.data?.error;
    return typeof code === "string" ? code : undefined;
  }
  return undefined;
}

/** Story 19/20: both reply endpoints also use 502 (send failure), unlike every other ticket action's 400-only error contract. */
function extractReplyErrorCode(caught: unknown): string | undefined {
  if (axios.isAxiosError(caught) && (caught.response?.status === 400 || caught.response?.status === 502)) {
    const code: unknown = caught.response.data?.error;
    return typeof code === "string" ? code : undefined;
  }
  return undefined;
}

const UNASSIGNED_OPTION = "";

/** Label above value, value visually stronger than the label — used throughout the information grid below. */
function Field({ label, value }: { label: string; value: ReactNode }) {
  return (
    <Box sx={{ minWidth: 0 }}>
      <Typography
        variant="caption"
        color="text.secondary"
        sx={{ display: "block", mb: 0.5, textTransform: "uppercase", letterSpacing: 0.4, fontSize: "0.7rem", fontWeight: 600 }}
      >
        {label}
      </Typography>
      <Box sx={{ typography: "body2", fontWeight: 600, wordBreak: "break-word" }}>{value}</Box>
    </Box>
  );
}

export function TicketDetailPage() {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const location = useLocation();
  const { hasPermission } = useAuth();

  // Story 23: a one-time notice handed off via navigation state by TicketFormPage when its save just
  // triggered automatic assignment (AssignedUserId null -> an agent). Read once on mount, then cleared
  // from history state so it doesn't reappear on a back-navigation or refresh.
  const [autoAssignNotice, setAutoAssignNotice] = useState<string | null>(() => {
    const state = location.state as { autoAssignNotice?: string } | null;
    return state?.autoAssignNotice ?? null;
  });

  useEffect(() => {
    if (autoAssignNotice === null) return;
    navigate(location.pathname, { replace: true, state: null });
    // Only ever needs to run once, right after mount — re-running on navigate/location changes would
    // immediately clear a notice a fresh navigation just set.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const canAssign = hasPermission("tickets.assign");
  const canChangeStatus = hasPermission("tickets.update");
  // Escalation workflow correction: requesting an escalation (Agent + Manager) and resolving one
  // (Manager only) are gated by two separate permissions — see Permissions.Tickets.Escalate /
  // EscalationManage on the backend.
  const canRequestEscalation = hasPermission("tickets.escalate");
  const canManageEscalation = hasPermission("tickets.escalation.manage");
  const canSeeEscalationSection = canRequestEscalation || canManageEscalation;
  const canUseQuickReplies = hasPermission("quickreplies.view");
  const canSendEmailReply = hasPermission("tickets.email.reply");
  const canSendChannelReply = hasPermission("tickets.channel.reply");

  const [ticket, setTicket] = useState<TicketDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [notFound, setNotFound] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const [eligibleUsers, setEligibleUsers] = useState<UserListItem[]>([]);
  const [selectedAssigneeId, setSelectedAssigneeId] = useState(UNASSIGNED_OPTION);
  const [assigning, setAssigning] = useState(false);
  const [assignError, setAssignError] = useState<string | null>(null);

  const [selectedStatus, setSelectedStatus] = useState("");
  const [changingStatus, setChangingStatus] = useState(false);
  const [statusError, setStatusError] = useState<string | null>(null);

  const [escalateReason, setEscalateReason] = useState("");
  const [escalating, setEscalating] = useState(false);
  const [escalateError, setEscalateError] = useState<string | null>(null);
  const [deEscalating, setDeEscalating] = useState(false);
  const [deEscalateError, setDeEscalateError] = useState<string | null>(null);

  // Story 17: a local-only scratch area for composing a reply. For a manually/internally created
  // ticket (sourceChannel === null) this stays exactly what it always was — nothing is ever sent.
  // Story 19 adds a real Send action, but only for an email-sourced ticket.
  const [replyDraft, setReplyDraft] = useState("");
  const [sendingReply, setSendingReply] = useState(false);
  const [sendReplyError, setSendReplyError] = useState<string | null>(null);
  const [replySent, setReplySent] = useState(false);

  // Only fetched for callers who can actually assign — no point loading the user catalogue for a
  // viewer who cannot act on it. A large single page rather than a live search-as-you-type endpoint,
  // matching the department/branch pickers on UserFormPage. Waits for the ticket to load, since the
  // filter below depends on its category's department. Strictly scoped to active users in that
  // department when it has one — no cross-department fallback, even if that leaves the picker empty,
  // matching the backend's own enforcement in TicketsService.UpdateAssignmentAsync (an assign attempt
  // outside the department is rejected there regardless of what this list shows). A category with no
  // department at all imposes no such restriction — every active user is eligible.
  useEffect(() => {
    if (!canAssign || ticket === null) return;

    let cancelled = false;
    const departmentId = ticket.categoryDepartmentId;

    usersApi
      .listUsers(departmentId === null ? { pageSize: 100 } : { pageSize: 100, departmentId })
      .then((result) => {
        if (!cancelled) setEligibleUsers(result.items.filter((user) => user.isActive));
      })
      .catch(() => {
        // Non-fatal: the picker simply has nothing to offer.
      });

    return () => {
      cancelled = true;
    };
    // Depends on primitives, not the `ticket` object itself, so this doesn't re-fetch every time
    // the ticket is re-set after an assign/status/escalate action — only when a different ticket
    // (or a differently-departmented category, which never happens for the same ticket today) loads.
  }, [canAssign, ticket?.id, ticket?.categoryDepartmentId]);

  useEffect(() => {
    if (id === undefined) return;

    let cancelled = false;
    setLoading(true);
    setNotFound(false);
    setError(null);

    ticketsApi
      .getTicket(id)
      .then((current) => {
        if (cancelled) return;
        setTicket(current);
        setSelectedAssigneeId(current.assignedUserId ?? UNASSIGNED_OPTION);
        setSelectedStatus("");
        setEscalateReason("");
        setReplyDraft("");
        setSendReplyError(null);
        setReplySent(false);
      })
      .catch((caught: unknown) => {
        if (cancelled) return;
        if (axios.isAxiosError(caught) && caught.response?.status === 404) {
          setNotFound(true);
        } else {
          setError("Could not load this ticket. Please try again.");
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [id]);

  async function handleAssign(): Promise<void> {
    if (ticket === null || assigning) return;

    setAssigning(true);
    setAssignError(null);

    try {
      const updated = await ticketsApi.updateTicketAssignment(
        ticket.id,
        selectedAssigneeId === UNASSIGNED_OPTION ? null : selectedAssigneeId,
      );
      setTicket(updated);
    } catch (caught: unknown) {
      const code = extractErrorCode(caught);
      if (code === "assigned_user_outside_department") {
        setAssignError("This user isn't in the department this ticket's category requires. Please pick another.");
      } else if (code === "invalid_assigned_user") {
        setAssignError("This user is no longer eligible for assignment. Please pick another.");
      } else {
        setAssignError("Could not update the assignment. Please try again.");
      }
    } finally {
      setAssigning(false);
    }
  }

  async function handleChangeStatus(): Promise<void> {
    if (ticket === null || changingStatus || selectedStatus.length === 0) return;

    setChangingStatus(true);
    setStatusError(null);

    try {
      const updated = await ticketsApi.updateTicketStatus(ticket.id, selectedStatus);
      setTicket(updated);
      setSelectedStatus("");
    } catch (caught: unknown) {
      const code = extractErrorCode(caught);
      setStatusError((code !== undefined && STATUS_ERROR_MESSAGES[code]) || "Could not update the status. Please try again.");
    } finally {
      setChangingStatus(false);
    }
  }

  async function handleEscalate(): Promise<void> {
    if (ticket === null || escalating || escalateReason.trim().length === 0) return;

    setEscalating(true);
    setEscalateError(null);

    try {
      const updated = await ticketsApi.escalateTicket(ticket.id, escalateReason.trim());
      setTicket(updated);
      setEscalateReason("");
    } catch (caught: unknown) {
      const code = extractErrorCode(caught);
      setEscalateError((code !== undefined && ESCALATE_ERROR_MESSAGES[code]) || "Could not escalate this ticket. Please try again.");
    } finally {
      setEscalating(false);
    }
  }

  async function handleDeEscalate(): Promise<void> {
    if (ticket === null || deEscalating) return;

    setDeEscalating(true);
    setDeEscalateError(null);

    try {
      const updated = await ticketsApi.deEscalateTicket(ticket.id);
      setTicket(updated);
    } catch (caught: unknown) {
      const code = extractErrorCode(caught);
      setDeEscalateError((code !== undefined && DEESCALATE_ERROR_MESSAGES[code]) || "Could not de-escalate this ticket. Please try again.");
    } finally {
      setDeEscalating(false);
    }
  }

  /** Story 19 (Email) / Story 20 (WhatsApp/Sms) — which sendable channel this ticket is on, gated by the caller's permission for that specific channel's reply endpoint. Null for a manual/WebForm ticket, or a sendable-channel ticket the caller lacks permission to reply on. */
  function sendableChannel(current: TicketDetail): "email" | "channel" | null {
    if (current.sourceChannel === "Email") return canSendEmailReply ? "email" : null;
    if (current.sourceChannel === "WhatsApp" || current.sourceChannel === "Sms") return canSendChannelReply ? "channel" : null;
    return null;
  }

  async function handleSendReply(): Promise<void> {
    if (ticket === null || sendingReply || replyDraft.trim().length === 0) return;
    const channel = sendableChannel(ticket);
    if (channel === null) return;

    setSendingReply(true);
    setSendReplyError(null);
    setReplySent(false);

    try {
      const updated = channel === "email"
        ? await ticketsApi.sendEmailReply(ticket.id, replyDraft.trim())
        : await ticketsApi.sendChannelReply(ticket.id, replyDraft.trim());
      setTicket(updated);
      setReplyDraft("");
      setReplySent(true);
    } catch (caught: unknown) {
      const code = extractReplyErrorCode(caught);
      setSendReplyError((code !== undefined && REPLY_ERROR_MESSAGES[code]) || "Could not send this reply. Please try again.");
    } finally {
      setSendingReply(false);
    }
  }

  const backLink = (
    <Button component={Link} to="/tickets" startIcon={<ArrowBackIcon />} size="small" sx={{ mb: 2 }}>
      Back to tickets
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
          Ticket not found.
        </Alert>
        {backLink}
      </Box>
    );
  }

  if (error !== null && ticket === null) {
    return (
      <Box sx={{ maxWidth: 900 }}>
        <Alert severity="error">{error}</Alert>
      </Box>
    );
  }

  if (ticket === null) {
    return null;
  }

  const assigneeUnchanged = selectedAssigneeId === (ticket.assignedUserId ?? UNASSIGNED_OPTION);
  const activeSendableChannel = sendableChannel(ticket);

  return (
    <Box sx={{ maxWidth: 900 }}>
      {backLink}

      {/* Page header: title/id on the left, status + primary action on the right — matches the
          title+action-button header pattern used by CustomersListPage/TicketCategoriesListPage. */}
      <Box
        sx={{
          display: "flex",
          flexWrap: "wrap",
          justifyContent: "space-between",
          alignItems: "flex-start",
          gap: 2,
          mb: 2.5,
        }}
      >
        <Box sx={{ minWidth: 0 }}>
          <Typography variant="h4" component="h1" sx={{ wordBreak: "break-word" }}>
            {ticket.subject}
          </Typography>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.25 }}>
            Ticket #{ticket.id.slice(0, 8)}
          </Typography>
        </Box>
        <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, flexWrap: "wrap" }}>
          <Chip label={statusLabel(ticket.status)} color={statusChipColor(ticket.status)} sx={{ fontWeight: 600 }} />
          {ticket.isEscalated && <Chip label="Escalated" color="error" sx={{ fontWeight: 600 }} />}
          {hasPermission("tickets.update") && (
            <Button variant="outlined" onClick={() => navigate(`/tickets/${ticket.id}/edit`)}>
              Edit
            </Button>
          )}
        </Box>
      </Box>

      {error !== null && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      {autoAssignNotice !== null && (
        <Alert severity="success" sx={{ mb: 2 }} onClose={() => setAutoAssignNotice(null)}>
          {autoAssignNotice}
        </Alert>
      )}

      <Card variant="outlined" sx={{ mb: 3 }}>
        <CardContent>
          <Typography variant="overline" color="text.secondary" sx={{ letterSpacing: 0.4, fontWeight: 700 }}>
            Ticket information
          </Typography>

          <Box
            sx={{
              display: "grid",
              gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)", md: "repeat(3, 1fr)" },
              columnGap: 3,
              rowGap: 2.5,
              mt: 2,
            }}
          >
            <Field
              label="Customer"
              value={<Link to={`/customers/${ticket.customerId}`}>{ticket.customerName}</Link>}
            />
            <Field label="Assignee" value={ticket.assignedUserName ?? "Unassigned"} />
            <Field label="Category" value={ticket.categoryName} />
            <Field
              label="Priority"
              value={
                <Chip
                  label={ticket.priorityName}
                  size="small"
                  color={priorityChipColor(ticket.priorityName)}
                  sx={{ textTransform: "capitalize", fontWeight: 600 }}
                />
              }
            />
            <Field label="Created by" value={ticket.createdByUserName ?? "—"} />
            <Field label="Created" value={new Date(ticket.createdAt).toLocaleString()} />
            <Field label="Updated" value={new Date(ticket.updatedAt).toLocaleString()} />
          </Box>
        </CardContent>
      </Card>

      {/* Story 22: SLA is read-only here — no configuration UI, no new actions. Absent entirely when
          the ticket predates the migration or its policy was missing at creation (ticket.sla === null). */}
      {ticket.sla !== null && (
        <Card variant="outlined" sx={{ mb: 3 }}>
          <CardContent>
            <Typography variant="overline" color="text.secondary" sx={{ letterSpacing: 0.4, fontWeight: 700 }}>
              SLA
            </Typography>
            <Box
              sx={{
                display: "grid",
                gridTemplateColumns: { xs: "1fr", sm: "repeat(2, 1fr)" },
                columnGap: 3,
                rowGap: 2.5,
                mt: 2,
              }}
            >
              <Box sx={{ minWidth: 0 }}>
                <Typography
                  variant="caption"
                  color="text.secondary"
                  sx={{ display: "block", mb: 0.75, textTransform: "uppercase", letterSpacing: 0.4, fontSize: "0.7rem", fontWeight: 600 }}
                >
                  First Response
                </Typography>
                <Chip
                  label={slaStatusLabel(ticket.sla.firstResponseStatus)}
                  size="small"
                  color={slaStatusChipColor(ticket.sla.firstResponseStatus)}
                  sx={{ fontWeight: 600, mb: 0.75 }}
                />
                <Typography variant="body2" color="text.secondary">
                  Due {new Date(ticket.sla.firstResponseDueAt).toLocaleString()}
                </Typography>
                {ticket.sla.firstResponseAt !== null && (
                  <Typography variant="body2" color="text.secondary">
                    Responded {new Date(ticket.sla.firstResponseAt).toLocaleString()}
                  </Typography>
                )}
              </Box>
              <Box sx={{ minWidth: 0 }}>
                <Typography
                  variant="caption"
                  color="text.secondary"
                  sx={{ display: "block", mb: 0.75, textTransform: "uppercase", letterSpacing: 0.4, fontSize: "0.7rem", fontWeight: 600 }}
                >
                  Resolution
                </Typography>
                <Chip
                  label={slaStatusLabel(ticket.sla.resolutionStatus)}
                  size="small"
                  color={slaStatusChipColor(ticket.sla.resolutionStatus)}
                  sx={{ fontWeight: 600, mb: 0.75 }}
                />
                <Typography variant="body2" color="text.secondary">
                  Due {new Date(ticket.sla.resolutionDueAt).toLocaleString()}
                </Typography>
                {ticket.sla.resolvedAt !== null && (
                  <Typography variant="body2" color="text.secondary">
                    Resolved {new Date(ticket.sla.resolvedAt).toLocaleString()}
                  </Typography>
                )}
              </Box>
            </Box>
          </CardContent>
        </Card>
      )}

      <Card variant="outlined" sx={{ mb: 3 }}>
        <CardContent>
          <Typography variant="overline" color="text.secondary" sx={{ letterSpacing: 0.4, fontWeight: 700 }}>
            Description
          </Typography>
          <Typography sx={{ whiteSpace: "pre-wrap", overflowWrap: "break-word", mt: 1.5, lineHeight: 1.6 }}>
            {ticket.description}
          </Typography>
        </CardContent>
      </Card>

      <Card variant="outlined" sx={{ mb: 3 }}>
        <CardContent>
          <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", flexWrap: "wrap", gap: 1.5 }}>
            <Typography variant="overline" color="text.secondary" sx={{ letterSpacing: 0.4, fontWeight: 700 }}>
              Reply draft
            </Typography>
            {canUseQuickReplies && (
              <QuickReplyPicker
                onInsert={(body) => setReplyDraft((current) => (current.length > 0 ? `${current}\n${body}` : body))}
              />
            )}
          </Box>
          <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5, mb: 1.5 }}>
            {activeSendableChannel !== null
              ? `This ticket came in via ${ticket.sourceChannel} — pressing Send sends your reply to the customer.`
              : "A scratch area for composing your reply. Nothing here is sent."}
          </Typography>

          {sendReplyError !== null && (
            <Alert severity="error" sx={{ mb: 1.5 }}>
              {sendReplyError}
            </Alert>
          )}
          {replySent && (
            <Alert severity="success" sx={{ mb: 1.5 }}>
              Reply sent.
            </Alert>
          )}

          <TextField
            value={replyDraft}
            onChange={(event) => setReplyDraft(event.target.value)}
            placeholder="Type a reply, or insert a quick reply above…"
            fullWidth
            multiline
            minRows={4}
          />

          {activeSendableChannel !== null && (
            <Box sx={{ display: "flex", justifyContent: "flex-end", mt: 1.5 }}>
              <Button
                variant="contained"
                disabled={sendingReply || replyDraft.trim().length === 0}
                onClick={() => void handleSendReply()}
              >
                {sendingReply ? "Sending…" : "Send"}
              </Button>
            </Box>
          )}
        </CardContent>
      </Card>

      {canAssign && (
        <Card variant="outlined">
          <CardContent>
            <Typography variant="overline" color="text.secondary" sx={{ letterSpacing: 0.4, fontWeight: 700 }}>
              Assignment
            </Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5, mb: 2 }}>
              Choose who is responsible for working this ticket.
            </Typography>

            {assignError !== null && (
              <Alert severity="error" sx={{ mb: 2 }}>
                {assignError}
              </Alert>
            )}

            <Box sx={{ display: "flex", flexDirection: { xs: "column", sm: "row" }, gap: 2, alignItems: { sm: "flex-end" } }}>
              <TextField
                select
                label="Assignee"
                size="small"
                value={selectedAssigneeId}
                onChange={(event) => setSelectedAssigneeId(event.target.value)}
                sx={{ minWidth: 260, flex: { sm: "0 1 320px" } }}
                slotProps={{ select: { displayEmpty: true }, inputLabel: { shrink: true } }}
              >
                <MenuItem value={UNASSIGNED_OPTION}>— Unassigned —</MenuItem>
                {eligibleUsers.map((user) => (
                  <MenuItem key={user.id} value={user.id}>
                    {user.displayName}
                  </MenuItem>
                ))}
              </TextField>
              <Button variant="contained" disabled={assigning || assigneeUnchanged} onClick={() => void handleAssign()}>
                {assigning ? "Saving…" : "Save assignment"}
              </Button>
            </Box>
          </CardContent>
        </Card>
      )}

      {canChangeStatus && (
        <Card variant="outlined" sx={{ mt: 3 }}>
          <CardContent>
            <Typography variant="overline" color="text.secondary" sx={{ letterSpacing: 0.4, fontWeight: 700 }}>
              Status
            </Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5, mb: 2 }}>
              Current status: <strong>{statusLabel(ticket.status)}</strong>
            </Typography>

            {statusError !== null && (
              <Alert severity="error" sx={{ mb: 2 }}>
                {statusError}
              </Alert>
            )}

            {(() => {
              const allowedNext = TICKET_STATUS_TRANSITIONS[ticket.status as TicketStatus] ?? [];
              if (allowedNext.length === 0) {
                return (
                  <Typography variant="body2" color="text.secondary">
                    No further status transitions are available.
                  </Typography>
                );
              }
              return (
                <Box sx={{ display: "flex", flexDirection: { xs: "column", sm: "row" }, gap: 2, alignItems: { sm: "flex-end" } }}>
                  <TextField
                    select
                    label="Change status to"
                    size="small"
                    value={selectedStatus}
                    onChange={(event) => setSelectedStatus(event.target.value)}
                    sx={{ minWidth: 220, flex: { sm: "0 1 280px" } }}
                    slotProps={{ select: { displayEmpty: true }, inputLabel: { shrink: true } }}
                  >
                    <MenuItem value="" disabled>
                      — select a status —
                    </MenuItem>
                    {allowedNext.map((next) => (
                      <MenuItem key={next} value={next}>
                        {statusLabel(next)}
                      </MenuItem>
                    ))}
                  </TextField>
                  <Button
                    variant="contained"
                    disabled={changingStatus || selectedStatus.length === 0}
                    onClick={() => void handleChangeStatus()}
                  >
                    {changingStatus ? "Saving…" : "Save status"}
                  </Button>
                </Box>
              );
            })()}
          </CardContent>
        </Card>
      )}

      {canSeeEscalationSection && (
        <Card variant="outlined" sx={{ mt: 3 }}>
          <CardContent>
            <Typography variant="overline" color="text.secondary" sx={{ letterSpacing: 0.4, fontWeight: 700 }}>
              Escalation
            </Typography>
            <Typography variant="body2" color="text.secondary" sx={{ mt: 0.5, mb: 2 }}>
              Current state: <strong>{escalationLabel(ticket)}</strong>
              {ticket.isEscalated && ticket.escalationReason !== null && <> — {ticket.escalationReason}</>}
            </Typography>

            {ticket.isEscalated ? (
              canManageEscalation ? (
                <>
                  {deEscalateError !== null && (
                    <Alert severity="error" sx={{ mb: 2 }}>
                      {deEscalateError}
                    </Alert>
                  )}
                  <Button variant="outlined" color="error" disabled={deEscalating} onClick={() => void handleDeEscalate()}>
                    {deEscalating ? "Saving…" : "De-escalate"}
                  </Button>
                </>
              ) : (
                // Requested by an Agent, resolved by a Manager: an Agent (or anyone without
                // tickets.escalation.manage) sees the escalated state but has no action here.
                <Typography variant="body2" color="text.secondary">
                  Awaiting review by a manager.
                </Typography>
              )
            ) : canRequestEscalation ? (
              <>
                {escalateError !== null && (
                  <Alert severity="error" sx={{ mb: 2 }}>
                    {escalateError}
                  </Alert>
                )}
                <Box sx={{ display: "flex", flexDirection: { xs: "column", sm: "row" }, gap: 2, alignItems: { sm: "flex-end" } }}>
                  <TextField
                    label="Reason"
                    size="small"
                    value={escalateReason}
                    onChange={(event) => setEscalateReason(event.target.value)}
                    fullWidth
                    sx={{ flex: { sm: "0 1 400px" } }}
                  />
                  <Button
                    variant="contained"
                    color="error"
                    disabled={escalating || escalateReason.trim().length === 0}
                    onClick={() => void handleEscalate()}
                  >
                    {escalating ? "Saving…" : "Escalate"}
                  </Button>
                </Box>
              </>
            ) : null}
          </CardContent>
        </Card>
      )}

      {hasPermission("agenttasks.read") && (
        <Card variant="outlined" sx={{ mt: 3 }}>
          <CardContent>
            <TicketTasksPanel ticketId={ticket.id} />
          </CardContent>
        </Card>
      )}

      {hasPermission("tickets.collaboration.view") && (
        <Card variant="outlined" sx={{ mt: 3 }}>
          <CardContent>
            <TicketCollaborationPanel ticketId={ticket.id} />
          </CardContent>
        </Card>
      )}

      <Card variant="outlined" sx={{ mt: 3 }}>
        <CardContent>
          <TicketHistoryPanel ticketId={ticket.id} />
        </CardContent>
      </Card>

      {hasPermission("customers.interactions.read") && (
        <Card variant="outlined" sx={{ mt: 3 }}>
          <CardContent>
            <CustomerInteractionHistory customerId={ticket.customerId} ticketId={ticket.id} />
          </CardContent>
        </Card>
      )}
    </Box>
  );
}
