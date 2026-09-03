import AddIcon from "@mui/icons-material/Add";
import {
  Alert,
  Box,
  Button,
  Card,
  CardContent,
  Chip,
  CircularProgress,
  Paper,
  Table,
  TableBody,
  TableCell,
  TableContainer,
  TableHead,
  TableRow,
  Typography,
} from "@mui/material";
import { useEffect, useMemo, useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { stateColor, stateLabel } from "./taskDisplay";
import * as tasksApi from "./tasksApi";
import type { AgentTask, AgentTaskState } from "./types";

type FilterChip = "all" | AgentTaskState;

const FILTER_CHIPS: { value: FilterChip; label: string }[] = [
  { value: "all", label: "All" },
  { value: "Pending", label: "Pending" },
  { value: "Upcoming", label: "Upcoming" },
  { value: "Overdue", label: "Overdue" },
  { value: "Completed", label: "Completed" },
];

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

export function TasksListPage() {
  const navigate = useNavigate();

  const [tasks, setTasks] = useState<AgentTask[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [reloadToken, setReloadToken] = useState(0);

  // "all" hides Completed by default, per the story's product rule — a separate "Completed" chip is
  // the explicit way to see them. Fetched once (includeCompleted: true) and filtered client-side by
  // chip, since a single agent's personal task list is expected to be small.
  const [activeFilter, setActiveFilter] = useState<FilterChip>("all");

  const [actioningId, setActioningId] = useState<string | null>(null);

  useEffect(() => {
    let cancelled = false;
    setLoading(true);
    setError(null);

    tasksApi
      .listTasks({ includeCompleted: true })
      .then((result) => {
        if (!cancelled) setTasks(result);
      })
      .catch(() => {
        if (!cancelled) setError("Could not load your tasks. Please try again.");
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, [reloadToken]);

  const overdue = useMemo(
    () => tasks.filter((task) => task.state === "Overdue").sort((a, b) => (a.reminderAt ?? "").localeCompare(b.reminderAt ?? "")),
    [tasks],
  );
  const upcoming = useMemo(
    () => tasks.filter((task) => task.state === "Upcoming").sort((a, b) => (a.reminderAt ?? "").localeCompare(b.reminderAt ?? "")),
    [tasks],
  );

  const visibleTasks = useMemo(
    () => (activeFilter === "all" ? tasks.filter((task) => task.state !== "Completed") : tasks.filter((task) => task.state === activeFilter)),
    [tasks, activeFilter],
  );

  async function handleComplete(task: AgentTask): Promise<void> {
    if (actioningId !== null) return;

    setActioningId(task.id);
    setError(null);

    try {
      const updated = task.state === "Completed" ? await tasksApi.reopenTask(task.id) : await tasksApi.completeTask(task.id);
      setTasks((current) => current.map((t) => (t.id === task.id ? updated : t)));
    } catch {
      setError("Could not update this task. Please try again.");
    } finally {
      setActioningId(null);
    }
  }

  async function handleDelete(task: AgentTask): Promise<void> {
    if (actioningId !== null) return;
    if (!window.confirm(`Delete "${task.title}"? This cannot be undone.`)) return;

    setActioningId(task.id);
    setError(null);

    try {
      await tasksApi.deleteTask(task.id);
      setTasks((current) => current.filter((t) => t.id !== task.id));
    } catch {
      setError("Could not delete this task. Please try again.");
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
          <Typography variant="h4">Tasks & Reminders</Typography>
          <Typography variant="body2" color="text.secondary">
            Your personal to-do list.
          </Typography>
        </Box>
        <Button component={Link} to="/agent-desk/tasks/new" variant="contained" startIcon={<AddIcon />}>
          New task
        </Button>
      </Box>

      {error !== null && (
        <Alert
          severity="error"
          sx={{ mb: 2 }}
          action={
            <Button color="inherit" size="small" onClick={() => setReloadToken((current) => current + 1)}>
              Retry
            </Button>
          }
        >
          {error}
        </Alert>
      )}

      {loading ? (
        <Box sx={{ display: "flex", alignItems: "center", gap: 1.5, py: 4 }}>
          <CircularProgress size={22} />
          <Typography color="text.secondary">Loading…</Typography>
        </Box>
      ) : (
        <>
          {(overdue.length > 0 || upcoming.length > 0) && (
            <Card variant="outlined" sx={{ mb: 3 }}>
              <CardContent>
                <Typography variant="overline" color="text.secondary" sx={{ letterSpacing: 0.4, fontWeight: 700 }}>
                  Reminders
                </Typography>
                <Box sx={{ display: "flex", flexDirection: "column", gap: 1, mt: 1.5 }}>
                  {overdue.map((task) => (
                    <Box key={task.id} sx={{ display: "flex", alignItems: "center", gap: 1.5 }}>
                      <Chip label="Overdue" size="small" color="error" />
                      <Typography variant="body2" sx={{ flexGrow: 1 }}>
                        {task.title}
                      </Typography>
                      <Typography variant="caption" color="text.secondary">
                        {task.reminderAt !== null && formatReminder(task.reminderAt)}
                      </Typography>
                    </Box>
                  ))}
                  {upcoming.map((task) => (
                    <Box key={task.id} sx={{ display: "flex", alignItems: "center", gap: 1.5 }}>
                      <Chip label="Upcoming" size="small" color="warning" />
                      <Typography variant="body2" sx={{ flexGrow: 1 }}>
                        {task.title}
                      </Typography>
                      <Typography variant="caption" color="text.secondary">
                        {task.reminderAt !== null && formatReminder(task.reminderAt)}
                      </Typography>
                    </Box>
                  ))}
                </Box>
              </CardContent>
            </Card>
          )}

          <Box sx={{ display: "flex", gap: 1, mb: 2, flexWrap: "wrap" }}>
            {FILTER_CHIPS.map((chip) => (
              <Chip
                key={chip.value}
                label={chip.label}
                onClick={() => setActiveFilter(chip.value)}
                color={activeFilter === chip.value ? "primary" : "default"}
                variant={activeFilter === chip.value ? "filled" : "outlined"}
              />
            ))}
          </Box>

          {visibleTasks.length === 0 ? (
            <Paper variant="outlined" sx={{ p: 4, textAlign: "center" }}>
              <Typography color="text.secondary" sx={{ mb: 2 }}>
                {tasks.length === 0 ? "You have no tasks yet." : "No tasks match this filter."}
              </Typography>
              {tasks.length === 0 && (
                <Button component={Link} to="/agent-desk/tasks/new" variant="outlined" startIcon={<AddIcon />}>
                  New task
                </Button>
              )}
            </Paper>
          ) : (
            <TableContainer component={Paper} variant="outlined">
              <Table>
                <TableHead>
                  <TableRow>
                    <TableCell>Title</TableCell>
                    <TableCell>Ticket</TableCell>
                    <TableCell>Reminder</TableCell>
                    <TableCell>State</TableCell>
                    <TableCell align="right">Actions</TableCell>
                  </TableRow>
                </TableHead>
                <TableBody>
                  {visibleTasks.map((task) => (
                    <TableRow key={task.id} hover>
                      <TableCell sx={{ fontWeight: 500 }}>{task.title}</TableCell>
                      <TableCell>
                        {task.ticketId !== null ? (
                          <Link to={`/tickets/${task.ticketId}`} onClick={(event) => event.stopPropagation()}>
                            {task.ticketSubject ?? "View ticket"}
                          </Link>
                        ) : (
                          <Typography component="span" variant="body2" color="text.secondary">
                            General
                          </Typography>
                        )}
                      </TableCell>
                      <TableCell>{task.reminderAt !== null ? formatReminder(task.reminderAt) : "—"}</TableCell>
                      <TableCell>
                        <Chip label={stateLabel(task.state)} size="small" color={stateColor(task.state)} />
                      </TableCell>
                      <TableCell align="right">
                        <Box sx={{ display: "flex", gap: 0.5, justifyContent: "flex-end" }}>
                          <Button size="small" onClick={() => navigate(`/agent-desk/tasks/${task.id}/edit`)}>
                            Edit
                          </Button>
                          <Button size="small" disabled={actioningId === task.id} onClick={() => void handleComplete(task)}>
                            {task.state === "Completed" ? "Reopen" : "Complete"}
                          </Button>
                          <Button
                            size="small"
                            color="error"
                            disabled={actioningId === task.id}
                            onClick={() => void handleDelete(task)}
                          >
                            Delete
                          </Button>
                        </Box>
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            </TableContainer>
          )}
        </>
      )}
    </Box>
  );
}
