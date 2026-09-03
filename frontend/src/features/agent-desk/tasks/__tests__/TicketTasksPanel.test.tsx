import { render, screen } from "@testing-library/react";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AuthContext } from "../../../auth/AuthContext";
import type { AuthState } from "../../../auth/AuthContext";
import * as tasksApi from "../tasksApi";
import { TicketTasksPanel } from "../TicketTasksPanel";
import type { AgentTask } from "../types";

function task(overrides: Partial<AgentTask> = {}): AgentTask {
  return {
    id: "task-1",
    title: "Call customer back",
    description: null,
    reminderAt: null,
    completedAt: null,
    state: "Pending",
    ticketId: "ticket-1",
    ticketSubject: "Cannot log in",
    createdAt: "2026-08-31T00:00:00Z",
    updatedAt: "2026-08-31T00:00:00Z",
    ...overrides,
  };
}

function stubAuth(permissions: string[]): AuthState {
  return {
    status: "authenticated",
    isAdmin: false,
    permissions,
    hasPermission: (code) => permissions.includes(code),
    hasAnyPermission: (codes) => codes.some((code) => permissions.includes(code)),
    user: { id: "current", email: "current@local.test", displayName: "Current User" },
    login: async () => undefined,
    logout: () => undefined,
  };
}

function renderPanel(permissions: string[]) {
  return render(
    <AuthContext value={stubAuth(permissions)}>
      <MemoryRouter>
        <TicketTasksPanel ticketId="ticket-1" />
      </MemoryRouter>
    </AuthContext>,
  );
}

describe("TicketTasksPanel", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("lists the caller's own tasks linked to this ticket", async () => {
    const listTasks = vi.spyOn(tasksApi, "listTasks").mockResolvedValue([task()]);

    renderPanel(["agenttasks.read"]);

    expect(await screen.findByText("Call customer back")).toBeInTheDocument();
    expect(listTasks).toHaveBeenCalledWith({ ticketId: "ticket-1", includeCompleted: true });
  });

  it("shows the empty state when no tasks are linked", async () => {
    vi.spyOn(tasksApi, "listTasks").mockResolvedValue([]);

    renderPanel(["agenttasks.read"]);

    expect(await screen.findByText("No tasks linked to this ticket yet.")).toBeInTheDocument();
  });

  it("shows the Add task link when the caller has agenttasks.create", async () => {
    vi.spyOn(tasksApi, "listTasks").mockResolvedValue([]);

    renderPanel(["agenttasks.read", "agenttasks.create"]);
    await screen.findByText("No tasks linked to this ticket yet.");

    expect(screen.getByRole("link", { name: /add task/i })).toHaveAttribute("href", "/agent-desk/tasks/new?ticketId=ticket-1");
  });

  it("hides the Add task link when agenttasks.create is missing", async () => {
    vi.spyOn(tasksApi, "listTasks").mockResolvedValue([]);

    renderPanel(["agenttasks.read"]);
    await screen.findByText("No tasks linked to this ticket yet.");

    expect(screen.queryByRole("link", { name: /add task/i })).not.toBeInTheDocument();
  });
});
