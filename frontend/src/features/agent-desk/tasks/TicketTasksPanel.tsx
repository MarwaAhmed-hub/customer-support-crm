import AddIcon from "@mui/icons-material/Add";
import { Alert, Box, Button, Chip, CircularProgress, Paper, Stack, Typography } from "@mui/material";
import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { useAuth } from "../../auth/useAuth";
import { stateColor, stateLabel } from "./taskDisplay";
import * as tasksApi from "./tasksApi";
import type { AgentTask } from "./types";

function formatReminder(isoString: string): string {
  return new Date(isoString).toLocaleString("en-US", {
    month: "short",
    day: "numeric",
    year: "numeric",
    hour: "numeric",
    minute: "2-digit",
    hour12: true,
  });
}

/**
 * The other half of the Tasks & Reminders ticket-link (see the story's correction): shows the
 * *current viewer's own* tasks linked to this ticket, with a shortcut to add another one already
 * pre-linked. Deliberately does not show other agents' tasks on the same ticket — the personal-task
 * owner-scoping from Story 16 is a privacy boundary, not something a ticket context relaxes.
 * Mounted from `TicketDetailPage`, gated there by `agenttasks.read`.
 */
export function TicketTasksPanel({ ticketId }: { ticketId: string }) {
  const { hasPermission } = useAuth();
  const canCreate = hasPermission("agenttasks.create");

  const [tasks, setTasks] = useState<AgentTask[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    tasksApi
      .listTasks({ ticketId, includeCompleted: true })
      .then((result) => {
        if (!cancelled) setTasks(result);
      })
      .catch(() => {
        if (!cancelled) setError("Could not load tasks for this ticket. Please try again.");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [ticketId]);

  return (
    <Box>
      <Box sx={{ display: "flex", justifyContent: "space-between", alignItems: "center", mb: 1.5 }}>
        <Typography variant="overline" color="text.secondary" sx={{ letterSpacing: 0.4, fontWeight: 700 }}>
          Tasks
        </Typography>
        {canCreate && (
          <Button
            component={Link}
            to={`/agent-desk/tasks/new?ticketId=${ticketId}`}
            size="small"
            startIcon={<AddIcon />}
          >
            Add task
          </Button>
        )}
      </Box>

      {error !== null && (
        <Alert severity="error" sx={{ mb: 2 }}>
          {error}
        </Alert>
      )}

      {loading ? (
        <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, py: 2 }}>
          <CircularProgress size={20} />
          <Typography variant="body2" color="text.secondary">
            Loading…
          </Typography>
        </Box>
      ) : error !== null ? null : tasks.length === 0 ? (
        <Typography variant="body2" color="text.secondary">
          No tasks linked to this ticket yet.
        </Typography>
      ) : (
        <Stack spacing={1}>
          {tasks.map((task) => (
            <Paper key={task.id} variant="outlined" sx={{ p: 1.5, display: "flex", alignItems: "center", gap: 1.5 }}>
              <Chip label={stateLabel(task.state)} size="small" color={stateColor(task.state)} />
              <Typography variant="body2" sx={{ flexGrow: 1 }}>
                {task.title}
              </Typography>
              {task.reminderAt !== null && (
                <Typography variant="caption" color="text.secondary">
                  {formatReminder(task.reminderAt)}
                </Typography>
              )}
              <Button component={Link} to={`/agent-desk/tasks/${task.id}/edit`} size="small">
                Edit
              </Button>
            </Paper>
          ))}
        </Stack>
      )}
    </Box>
  );
}
