/** Mirrors the backend DTOs in `Api/AgentDesk/Tasks/AgentTaskDtos.cs` (camelCase — System.Text.Json's default; `state` serializes as its enum name via `[JsonConverter(typeof(JsonStringEnumConverter))]`). */

export type AgentTaskState = "Pending" | "Upcoming" | "Overdue" | "Completed";

export interface AgentTask {
  id: string;
  title: string;
  description: string | null;
  reminderAt: string | null;
  completedAt: string | null;
  state: AgentTaskState;
  /** Null = a general task with no ticket context. Set automatically when created from a ticket's detail page, or optionally chosen when created from the Tasks & Reminders page. */
  ticketId: string | null;
  ticketSubject: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface CreateAgentTaskPayload {
  title: string;
  description: string | null;
  reminderAt: string | null;
  ticketId?: string | null;
}

export type UpdateAgentTaskPayload = CreateAgentTaskPayload;
