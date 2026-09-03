import { http } from "../../../lib/http";
import type { AgentTask, AgentTaskState, CreateAgentTaskPayload, UpdateAgentTaskPayload } from "./types";

export interface ListTasksParams {
  /** `false` excludes Completed tasks; omitted/`true` includes everything. */
  includeCompleted?: boolean;
  state?: AgentTaskState;
  /** Scopes to tasks linked to this ticket — still only the caller's own tasks (owner-scoping is never relaxed). Powers the ticket detail page's Tasks section. */
  ticketId?: string;
}

export async function listTasks(params: ListTasksParams = {}): Promise<AgentTask[]> {
  const response = await http.get<AgentTask[]>("/agent-desk/tasks", { params });
  return response.data;
}

export async function getTask(id: string): Promise<AgentTask> {
  const response = await http.get<AgentTask>(`/agent-desk/tasks/${id}`);
  return response.data;
}

export async function createTask(payload: CreateAgentTaskPayload): Promise<AgentTask> {
  const response = await http.post<AgentTask>("/agent-desk/tasks", payload);
  return response.data;
}

export async function updateTask(id: string, payload: UpdateAgentTaskPayload): Promise<AgentTask> {
  const response = await http.put<AgentTask>(`/agent-desk/tasks/${id}`, payload);
  return response.data;
}

export async function completeTask(id: string): Promise<AgentTask> {
  const response = await http.post<AgentTask>(`/agent-desk/tasks/${id}/complete`);
  return response.data;
}

export async function reopenTask(id: string): Promise<AgentTask> {
  const response = await http.post<AgentTask>(`/agent-desk/tasks/${id}/reopen`);
  return response.data;
}

export async function deleteTask(id: string): Promise<void> {
  await http.delete(`/agent-desk/tasks/${id}`);
}
