import type { ChipProps } from "@mui/material";
import type { AgentTaskState } from "./types";

const STATE_LABELS: Record<AgentTaskState, string> = {
  Pending: "Pending",
  Upcoming: "Upcoming",
  Overdue: "Overdue",
  Completed: "Completed",
};

export function stateLabel(state: AgentTaskState): string {
  return STATE_LABELS[state];
}

/** Overdue = red, Upcoming = amber, Completed = green, Pending = neutral — shared by the list badges and the reminders callout. */
export function stateColor(state: AgentTaskState): ChipProps["color"] {
  switch (state) {
    case "Overdue":
      return "error";
    case "Upcoming":
      return "warning";
    case "Completed":
      return "success";
    case "Pending":
    default:
      return "default";
  }
}
