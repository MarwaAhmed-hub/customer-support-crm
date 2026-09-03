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
  Typography,
} from "@mui/material";
import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import * as notificationsApi from "./notificationsApi";
import type { Notification, NotificationEventType } from "./types";

const PAGE_SIZE = 20;

function eventTypeLabel(eventType: NotificationEventType): string {
  switch (eventType) {
    case "TicketAssigned":
      return "Assigned";
    case "SlaWarning":
      return "SLA Warning";
    case "SlaBreached":
      return "SLA Breached";
  }
}

function eventTypeChipColor(eventType: NotificationEventType): "info" | "warning" | "error" {
  switch (eventType) {
    case "TicketAssigned":
      return "info";
    case "SlaWarning":
      return "warning";
    case "SlaBreached":
      return "error";
  }
}

function formatDate(isoString: string): string {
  const date = new Date(isoString.endsWith("Z") ? isoString : `${isoString}Z`);
  return date.toLocaleDateString("en-US", {
    month: "short",
    day: "numeric",
    year: "numeric",
    hour: "numeric",
    minute: "2-digit",
    hour12: true,
  });
}

export function NotificationsInboxPage() {
  const [notifications, setNotifications] = useState<Notification[]>([]);
  const [unreadOnly, setUnreadOnly] = useState(false);
  const [page, setPage] = useState(1);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [markingAllRead, setMarkingAllRead] = useState(false);
  const [reloadToken, setReloadToken] = useState(0);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    notificationsApi
      .listMyNotifications({ unreadOnly, page, pageSize: PAGE_SIZE })
      .then((result) => {
        if (cancelled) return;
        setNotifications(result.items);
        setTotal(result.total);
      })
      .catch(() => {
        if (!cancelled) setError("Could not load notifications. Please try again.");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [unreadOnly, page, reloadToken]);

  async function handleMarkRead(id: string): Promise<void> {
    try {
      await notificationsApi.markNotificationRead(id);
      setNotifications((current) =>
        current.map((n) => (n.id === id ? { ...n, readAtUtc: new Date().toISOString() } : n)),
      );
    } catch {
      // Non-fatal: the row just stays showing as unread; the user can retry the click.
    }
  }

  async function handleMarkAllRead(): Promise<void> {
    setMarkingAllRead(true);
    try {
      await notificationsApi.markAllNotificationsRead();
      setReloadToken((current) => current + 1);
    } catch {
      setError("Could not mark all notifications as read. Please try again.");
    } finally {
      setMarkingAllRead(false);
    }
  }

  const totalPages = Math.max(1, Math.ceil(total / PAGE_SIZE));

  return (
    <Box sx={{ maxWidth: 1000 }}>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1, flexWrap: "wrap", gap: 2 }}>
        <Box>
          <Typography variant="h4">Notifications</Typography>
          <Typography variant="body2" color="text.secondary">
            Ticket assignments and SLA warnings/breaches routed to you.
          </Typography>
        </Box>
        <Button variant="outlined" disabled={markingAllRead} onClick={() => void handleMarkAllRead()}>
          {markingAllRead ? "Marking…" : "Mark all as read"}
        </Button>
      </Box>

      <FormControlLabel
        sx={{ mb: 2, mt: 1 }}
        control={
          <Switch
            checked={unreadOnly}
            onChange={(event) => {
              setUnreadOnly(event.target.checked);
              setPage(1);
            }}
          />
        }
        label="Unread only"
      />

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
      ) : notifications.length === 0 ? (
        <Paper variant="outlined" sx={{ p: 4, textAlign: "center" }}>
          <Typography color="text.secondary">
            {unreadOnly ? "No unread notifications." : "No notifications yet."}
          </Typography>
        </Paper>
      ) : (
        <TableContainer component={Paper} variant="outlined">
          <Table size="small">
            <TableHead>
              <TableRow>
                <TableCell>Type</TableCell>
                <TableCell>Message</TableCell>
                <TableCell>Received</TableCell>
                <TableCell align="right">Actions</TableCell>
              </TableRow>
            </TableHead>
            <TableBody>
              {notifications.map((notification) => {
                const isUnread = notification.readAtUtc === null;
                return (
                  <TableRow key={notification.id} sx={isUnread ? { backgroundColor: "action.hover" } : undefined}>
                    <TableCell>
                      <Chip
                        label={eventTypeLabel(notification.eventType)}
                        size="small"
                        color={eventTypeChipColor(notification.eventType)}
                        variant={isUnread ? "filled" : "outlined"}
                      />
                    </TableCell>
                    <TableCell>
                      <Typography variant="body2" sx={{ fontWeight: isUnread ? 600 : 400 }}>
                        {notification.subject}
                      </Typography>
                      <Typography variant="body2" color="text.secondary">
                        {notification.body}
                      </Typography>
                      <Link to={`/tickets/${notification.ticketId}`} style={{ fontSize: "0.8125rem" }}>
                        View ticket
                      </Link>
                    </TableCell>
                    <TableCell sx={{ whiteSpace: "nowrap" }}>{formatDate(notification.createdAtUtc)}</TableCell>
                    <TableCell align="right">
                      {isUnread && (
                        <Button size="small" onClick={() => void handleMarkRead(notification.id)}>
                          Mark read
                        </Button>
                      )}
                    </TableCell>
                  </TableRow>
                );
              })}
            </TableBody>
          </Table>
        </TableContainer>
      )}

      {!loading && notifications.length > 0 && (
        <Box sx={{ display: "flex", alignItems: "center", justifyContent: "center", gap: 2, mt: 3 }}>
          <Button variant="outlined" size="small" disabled={page <= 1} onClick={() => setPage((current) => Math.max(1, current - 1))}>
            Previous
          </Button>
          <Typography variant="body2" color="text.secondary">
            Page {page} of {totalPages}
          </Typography>
          <Button variant="outlined" size="small" disabled={page >= totalPages} onClick={() => setPage((current) => current + 1)}>
            Next
          </Button>
        </Box>
      )}
    </Box>
  );
}
