import { http } from "../../../lib/http";
import type { PagedResult, TicketListItem } from "../../tickets/tickets/types";

const DASHBOARD_PAGE_SIZE = 100;

/**
 * `GET /api/tickets/mine` — the backend derives the caller from the JWT rather than accepting a
 * caller-supplied assignee, so this never needs (and cannot be made) to pass another agent's id.
 * No pagination UI: an individual agent's assigned-ticket count is expected to be small, so a single
 * generously-sized page keeps this a plain read instead of adding pager state the story doesn't ask for.
 */
export async function fetchMyAssignedTickets(): Promise<PagedResult<TicketListItem>> {
  const response = await http.get<PagedResult<TicketListItem>>("/tickets/mine", {
    params: { pageSize: DASHBOARD_PAGE_SIZE },
  });
  return response.data;
}
