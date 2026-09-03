import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import * as ticketsApi from "../../../tickets/tickets/ticketsApi";
import type { TicketDetail, TicketListItem } from "../../../tickets/tickets/types";
import * as tasksApi from "../tasksApi";
import { TaskFormPage } from "../TaskFormPage";
import type { AgentTask } from "../types";

const EXISTING: AgentTask = {
  id: "task-1",
  title: "Existing task",
  description: "Existing description",
  reminderAt: "2026-09-01T10:30:00.000Z",
  completedAt: null,
  state: "Upcoming",
  ticketId: null,
  ticketSubject: null,
  createdAt: "2026-08-01T00:00:00Z",
  updatedAt: "2026-08-01T00:00:00Z",
};

const TICKET: TicketListItem = {
  id: "ticket-1",
  customerId: "customer-1",
  customerName: "Jane Doe",
  subject: "Cannot log in",
  categoryId: "cat-1",
  categoryName: "Account / Access",
  priorityId: "pri-1",
  priorityName: "High",
  status: "open",
  createdByUserId: "creator-1",
  createdByUserName: "Local Admin",
  assignedUserId: null,
  assignedUserName: null,
  isEscalated: false,
  createdAt: "2026-08-01T00:00:00Z",
  updatedAt: "2026-08-01T00:00:00Z",
  sourceChannel: null,
  sla: null,
};

function LandingProbe() {
  const location = useLocation();
  return <div data-testid="landed">landed on {location.pathname}</div>;
}

function renderCreate() {
  return render(
    <MemoryRouter initialEntries={["/agent-desk/tasks/new"]}>
      <Routes>
        <Route path="/agent-desk/tasks/new" element={<TaskFormPage />} />
        <Route path="/agent-desk/tasks" element={<LandingProbe />} />
      </Routes>
    </MemoryRouter>,
  );
}

function renderCreateFromTicket(ticketId: string) {
  return render(
    <MemoryRouter initialEntries={[`/agent-desk/tasks/new?ticketId=${ticketId}`]}>
      <Routes>
        <Route path="/agent-desk/tasks/new" element={<TaskFormPage />} />
        <Route path="/tickets/:id" element={<LandingProbe />} />
      </Routes>
    </MemoryRouter>,
  );
}

function renderEdit(id: string) {
  return render(
    <MemoryRouter initialEntries={[`/agent-desk/tasks/${id}/edit`]}>
      <Routes>
        <Route path="/agent-desk/tasks/:id/edit" element={<TaskFormPage />} />
        <Route path="/agent-desk/tasks" element={<LandingProbe />} />
      </Routes>
    </MemoryRouter>,
  );
}

describe("TaskFormPage — create mode", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    // Every test in this file exercises the title/description/reminder flow, not the ticket picker
    // specifically — default its source to empty so those tests don't need to know about it.
    vi.spyOn(ticketsApi, "listTickets").mockResolvedValue({ items: [], page: 1, pageSize: 100, total: 0 });
  });

  it("shows a validation error when the title is blank", async () => {
    renderCreate();

    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    expect(await screen.findByText("Title is required.")).toBeInTheDocument();
  });

  it("converts the local reminder value to a UTC ISO string on submit", async () => {
    const createTask = vi.spyOn(tasksApi, "createTask").mockResolvedValue(EXISTING);

    renderCreate();
    await userEvent.type(screen.getByLabelText("Title"), "New task");
    const reminderInput = screen.getByLabelText("Reminder");
    await userEvent.type(reminderInput, "2026-09-01T10:30");
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    const expectedIso = new Date("2026-09-01T10:30").toISOString();
    await vi.waitFor(() =>
      expect(createTask).toHaveBeenCalledWith({
        title: "New task",
        description: null,
        reminderAt: expectedIso,
        ticketId: null,
      }),
    );
    expect(await screen.findByTestId("landed")).toHaveTextContent("landed on /agent-desk/tasks");
  });

  it("submits a null reminder when none is set", async () => {
    const createTask = vi.spyOn(tasksApi, "createTask").mockResolvedValue(EXISTING);

    renderCreate();
    await userEvent.type(screen.getByLabelText("Title"), "No reminder task");
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    await vi.waitFor(() =>
      expect(createTask).toHaveBeenCalledWith({
        title: "No reminder task",
        description: null,
        reminderAt: null,
        ticketId: null,
      }),
    );
  });

  it("shows an editable 'Related ticket' picker and includes the selection in the payload", async () => {
    vi.spyOn(ticketsApi, "listTickets").mockResolvedValue({ items: [TICKET], page: 1, pageSize: 100, total: 1 });
    const createTask = vi.spyOn(tasksApi, "createTask").mockResolvedValue(EXISTING);

    renderCreate();
    await userEvent.type(screen.getByLabelText("Title"), "Follow up");
    await userEvent.click(screen.getByLabelText("Related ticket (optional)"));
    await userEvent.click(await screen.findByRole("option", { name: "Cannot log in" }));
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    await vi.waitFor(() =>
      expect(createTask).toHaveBeenCalledWith(
        expect.objectContaining({ title: "Follow up", ticketId: "ticket-1" }),
      ),
    );
  });

  it("defaults the ticket picker to '— None —' and submits ticketId: null when nothing is chosen", async () => {
    vi.spyOn(ticketsApi, "listTickets").mockResolvedValue({ items: [TICKET], page: 1, pageSize: 100, total: 1 });

    renderCreate();

    expect(screen.getByText("— None —")).toBeInTheDocument();
  });
});

describe("TaskFormPage — created from a ticket (?ticketId=)", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("shows a read-only linked-ticket line instead of a picker, and never calls listTickets", async () => {
    const listTickets = vi.spyOn(ticketsApi, "listTickets");
    vi.spyOn(ticketsApi, "getTicket").mockResolvedValue({ ...TICKET, description: "Customer cannot access their account." } as TicketDetail);

    renderCreateFromTicket("ticket-1");

    expect(await screen.findByText("Cannot log in")).toBeInTheDocument();
    expect(screen.queryByLabelText("Related ticket (optional)")).not.toBeInTheDocument();
    expect(listTickets).not.toHaveBeenCalled();
  });

  it("automatically links the created task to the ticket without the agent selecting it", async () => {
    vi.spyOn(ticketsApi, "getTicket").mockResolvedValue({ ...TICKET, description: "d" } as TicketDetail);
    const createTask = vi.spyOn(tasksApi, "createTask").mockResolvedValue({ ...EXISTING, ticketId: "ticket-1", ticketSubject: "Cannot log in" });

    renderCreateFromTicket("ticket-1");
    await userEvent.type(await screen.findByLabelText("Title"), "Call customer back");
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    await vi.waitFor(() =>
      expect(createTask).toHaveBeenCalledWith(
        expect.objectContaining({ title: "Call customer back", ticketId: "ticket-1" }),
      ),
    );
    expect(await screen.findByTestId("landed")).toHaveTextContent("landed on /tickets/ticket-1");
  });
});

describe("TaskFormPage — edit mode", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    // Every test in this file exercises the title/description/reminder flow, not the ticket picker
    // specifically — default its source to empty so those tests don't need to know about it.
    vi.spyOn(ticketsApi, "listTickets").mockResolvedValue({ items: [], page: 1, pageSize: 100, total: 0 });
  });

  it("prefills the title, description, and reminder from the existing task", async () => {
    vi.spyOn(tasksApi, "getTask").mockResolvedValue(EXISTING);

    renderEdit("task-1");

    expect(await screen.findByDisplayValue("Existing task")).toBeInTheDocument();
    expect(screen.getByDisplayValue("Existing description")).toBeInTheDocument();
  });

  it("submits the update and navigates back to the list", async () => {
    vi.spyOn(tasksApi, "getTask").mockResolvedValue(EXISTING);
    const updateTask = vi.spyOn(tasksApi, "updateTask").mockResolvedValue({ ...EXISTING, title: "Renamed" });

    renderEdit("task-1");
    const titleInput = await screen.findByDisplayValue("Existing task");
    await userEvent.clear(titleInput);
    await userEvent.type(titleInput, "Renamed");
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    await vi.waitFor(() => expect(updateTask).toHaveBeenCalledWith("task-1", expect.objectContaining({ title: "Renamed" })));
    expect(await screen.findByTestId("landed")).toHaveTextContent("landed on /agent-desk/tasks");
  });

  it("shows an editable ticket picker (not the locked read-only line) pre-filled with the task's linked ticket", async () => {
    vi.spyOn(ticketsApi, "listTickets").mockResolvedValue({ items: [TICKET], page: 1, pageSize: 100, total: 1 });
    vi.spyOn(tasksApi, "getTask").mockResolvedValue({ ...EXISTING, ticketId: "ticket-1", ticketSubject: "Cannot log in" });

    renderEdit("task-1");

    expect(await screen.findByLabelText("Related ticket (optional)")).toBeInTheDocument();
    expect(screen.getByText("Cannot log in")).toBeInTheDocument();
  });

  it("can unlink a task from its ticket by selecting '— None —'", async () => {
    vi.spyOn(ticketsApi, "listTickets").mockResolvedValue({ items: [TICKET], page: 1, pageSize: 100, total: 1 });
    vi.spyOn(tasksApi, "getTask").mockResolvedValue({ ...EXISTING, ticketId: "ticket-1", ticketSubject: "Cannot log in" });
    const updateTask = vi.spyOn(tasksApi, "updateTask").mockResolvedValue(EXISTING);

    renderEdit("task-1");
    await userEvent.click(await screen.findByLabelText("Related ticket (optional)"));
    await userEvent.click(await screen.findByRole("option", { name: "— None —" }));
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    await vi.waitFor(() => expect(updateTask).toHaveBeenCalledWith("task-1", expect.objectContaining({ ticketId: null })));
  });
});
