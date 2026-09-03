import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import * as tasksApi from "../tasksApi";
import { TasksListPage } from "../TasksListPage";
import type { AgentTask } from "../types";

function task(overrides: Partial<AgentTask> = {}): AgentTask {
  return {
    id: "task-1",
    title: "Follow up with Jane",
    description: null,
    reminderAt: null,
    completedAt: null,
    state: "Pending",
    ticketId: null,
    ticketSubject: null,
    createdAt: "2026-08-31T00:00:00Z",
    updatedAt: "2026-08-31T00:00:00Z",
    ...overrides,
  };
}

function renderPage() {
  return render(
    <MemoryRouter>
      <TasksListPage />
    </MemoryRouter>,
  );
}

describe("TasksListPage", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("renders the empty state when there are no tasks", async () => {
    vi.spyOn(tasksApi, "listTasks").mockResolvedValue([]);

    renderPage();

    expect(await screen.findByText("You have no tasks yet.")).toBeInTheDocument();
  });

  it("shows overdue and upcoming reminders in the callout", async () => {
    vi.spyOn(tasksApi, "listTasks").mockResolvedValue([
      task({ id: "t-overdue", title: "Overdue task", state: "Overdue", reminderAt: "2026-08-01T00:00:00Z" }),
      task({ id: "t-upcoming", title: "Upcoming task", state: "Upcoming", reminderAt: "2026-09-01T00:00:00Z" }),
    ]);

    renderPage();

    expect(await screen.findByText("Reminders")).toBeInTheDocument();
    // Both titles also appear in the main table below (the default "All" filter still shows
    // Overdue/Upcoming tasks — only Completed is hidden by default), so at least one match is enough.
    expect(screen.getAllByText("Overdue task").length).toBeGreaterThan(0);
    expect(screen.getAllByText("Upcoming task").length).toBeGreaterThan(0);
  });

  it("hides the reminders callout when there are no overdue or upcoming tasks", async () => {
    vi.spyOn(tasksApi, "listTasks").mockResolvedValue([task({ state: "Pending" })]);

    renderPage();

    await screen.findByText("Follow up with Jane");
    expect(screen.queryByText("Reminders")).not.toBeInTheDocument();
  });

  it("hides completed tasks under the default 'All' filter", async () => {
    vi.spyOn(tasksApi, "listTasks").mockResolvedValue([
      task({ id: "t-pending", title: "Pending task", state: "Pending" }),
      task({ id: "t-done", title: "Done task", state: "Completed", completedAt: "2026-08-30T00:00:00Z" }),
    ]);

    renderPage();

    expect(await screen.findByText("Pending task")).toBeInTheDocument();
    expect(screen.queryByText("Done task")).not.toBeInTheDocument();
  });

  it("shows completed tasks when the Completed filter chip is selected", async () => {
    vi.spyOn(tasksApi, "listTasks").mockResolvedValue([
      task({ id: "t-pending", title: "Pending task", state: "Pending" }),
      task({ id: "t-done", title: "Done task", state: "Completed", completedAt: "2026-08-30T00:00:00Z" }),
    ]);

    renderPage();
    await screen.findByText("Pending task");

    await userEvent.click(screen.getByText("Completed"));

    expect(await screen.findByText("Done task")).toBeInTheDocument();
    expect(screen.queryByText("Pending task")).not.toBeInTheDocument();
  });

  it("filters to only the selected state when a specific chip is clicked", async () => {
    vi.spyOn(tasksApi, "listTasks").mockResolvedValue([
      task({ id: "t-pending", title: "Pending task", state: "Pending" }),
      task({ id: "t-overdue", title: "Overdue task", state: "Overdue", reminderAt: "2026-08-01T00:00:00Z" }),
    ]);

    renderPage();
    await screen.findByText("Pending task");

    await userEvent.click(screen.getByRole("button", { name: "Overdue" }));

    expect(screen.queryByText("Pending task")).not.toBeInTheDocument();
  });

  it("links a ticket-linked task's Ticket cell to the ticket, and shows 'General' for unlinked tasks", async () => {
    vi.spyOn(tasksApi, "listTasks").mockResolvedValue([
      task({ id: "t-linked", title: "Linked task", ticketId: "ticket-1", ticketSubject: "Cannot log in" }),
      task({ id: "t-general", title: "General task", ticketId: null, ticketSubject: null }),
    ]);

    renderPage();
    await screen.findByText("Linked task");

    expect(screen.getByRole("link", { name: "Cannot log in" })).toHaveAttribute("href", "/tickets/ticket-1");
    expect(screen.getByText("General")).toBeInTheDocument();
  });
});
