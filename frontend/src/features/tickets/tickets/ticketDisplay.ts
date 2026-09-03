import type { ChipProps } from "@mui/material";
import type { SlaStatus, TicketDetail, TicketSla, TicketStatus } from "./types";

const STATUS_LABELS: Record<TicketStatus, string> = {
  open: "Open",
  in_progress: "In Progress",
  pending: "Pending",
  resolved: "Resolved",
  closed: "Closed",
};

/** Human label for a status code — falls back to the raw code for anything outside the known lifecycle. */
export function statusLabel(status: string): string {
  return STATUS_LABELS[status as TicketStatus] ?? status;
}

/** Story 13: "Escalated" vs "Normal" — purely cosmetic, driven by `TicketDetail.isEscalated`. */
export function escalationLabel(ticket: Pick<TicketDetail, "isEscalated">): string {
  return ticket.isEscalated ? "Escalated" : "Normal";
}

/** Story 19/20: short label for a ticket's `sourceChannel` badge on the list page. Only called when `sourceChannel` is non-null. */
export function channelLabel(sourceChannel: string): string {
  switch (sourceChannel) {
    case "Sms":
      return "SMS";
    case "WhatsApp":
      return "WhatsApp";
    case "Email":
      return "Email";
    case "WebForm":
      return "Web";
    case "LiveChat":
      return "Live Chat";
    default:
      return sourceChannel;
  }
}

/**
 * Frontend-only display heuristics — purely cosmetic, no backend field backs these colors.
 * Unknown values fall back to "default" so a future status/priority never breaks, it just renders
 * as a plain grey chip until this map is extended.
 */
export function statusChipColor(status: string): ChipProps["color"] {
  switch (status.toLowerCase()) {
    case "open":
    case "in_progress":
      return "info";
    case "resolved":
    case "closed":
      return "success";
    case "pending":
      return "warning";
    default:
      return "default";
  }
}

export function priorityChipColor(priorityName: string): ChipProps["color"] {
  switch (priorityName.toLowerCase()) {
    case "urgent":
      return "error";
    case "high":
      return "warning";
    case "medium":
      return "info";
    case "low":
      return "success";
    default:
      return "default";
  }
}

const SLA_STATUS_LABELS: Record<SlaStatus, string> = {
  running: "Running",
  met: "Met",
  breached: "Breached",
};

/** Story 22: human label for an SLA clock's status ("running" | "met" | "breached"). */
export function slaStatusLabel(status: SlaStatus): string {
  return SLA_STATUS_LABELS[status] ?? status;
}

/** Story 22: green = Met, red = Breached, neutral = Running — per the story's color rule. */
export function slaStatusChipColor(status: SlaStatus): ChipProps["color"] {
  switch (status) {
    case "met":
      return "success";
    case "breached":
      return "error";
    default:
      return "default";
  }
}

/** Story 22: the worse of First Response and Resolution, for the list page's single compact column — Breached > Running > Met. */
export function worstSlaStatus(sla: TicketSla): SlaStatus {
  const statuses = [sla.firstResponseStatus, sla.resolutionStatus];
  if (statuses.includes("breached")) return "breached";
  if (statuses.includes("running")) return "running";
  return "met";
}
