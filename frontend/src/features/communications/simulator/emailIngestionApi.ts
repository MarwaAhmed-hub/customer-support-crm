import { http } from "../../../lib/http";

export interface IncomingEmailPayload {
  from: string;
  to?: string;
  subject: string;
  bodyText: string;
  externalMessageId: string;
  inReplyToMessageId?: string;
}

export interface EmailIngestionResult {
  ticketId: string;
  customerId: string;
  alreadyProcessed: boolean;
}

/**
 * Story 19's manual/dev replay endpoint (`POST /api/public/email/ingest`) — see backend/README.md's
 * "Email channel" section. There is no real mailbox behind this; it exists specifically so the
 * ingest → find-or-create-customer → find-or-link-ticket → interaction flow can be exercised without
 * one. Anonymous on the backend (correction — this represents a customer's message arriving, not a
 * staff action), same as the public Web Form and Live Chat widget.
 */
export async function ingestEmail(payload: IncomingEmailPayload): Promise<EmailIngestionResult> {
  const response = await http.post<EmailIngestionResult>("/public/email/ingest", payload);
  return response.data;
}
