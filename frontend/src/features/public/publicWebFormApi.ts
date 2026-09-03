import { http } from "../../lib/http";

export interface WebFormSubmissionPayload {
  name: string;
  email: string;
  subject: string;
  description: string;
  phone?: string;
  /** Honeypot — always empty for a real visitor. See `SupportRequestPage`'s hidden field. */
  website?: string;
}

export interface WebFormSubmissionResponse {
  ticketId: string;
  customerId: string;
}

/**
 * Story 19: the anonymous, public entry point — `POST /api/public/web-forms/tickets`. Uses the same
 * shared `http` instance as every authenticated call; the endpoint itself is `[AllowAnonymous]` and
 * ignores any bearer token a staff member happens to be carrying if they load this page while signed
 * in, so there is no need for a separate unauthenticated client.
 */
export async function submitWebFormTicket(payload: WebFormSubmissionPayload): Promise<WebFormSubmissionResponse> {
  const response = await http.post<WebFormSubmissionResponse>("/public/web-forms/tickets", payload);
  return response.data;
}
