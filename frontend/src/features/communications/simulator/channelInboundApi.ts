import { http } from "../../../lib/http";

export interface InboundMessagePayload {
  fromPhoneNumber: string;
  toPhoneNumber?: string;
  body: string;
  externalMessageId: string;
  externalConversationId?: string;
}

export interface InboundMessageResult {
  ticketId: string;
  customerId: string;
  deduplicated: boolean;
}

/**
 * Story 20's manual/dev replay endpoints (`POST /api/public/channels/{whatsapp|sms}/inbound`) — see
 * backend/README.md-equivalent remarks on `InboundMessageService`. No real WhatsApp/SMS provider is
 * wired up; this exercises the same ingest → find-or-create-customer → find-or-link-ticket → interaction
 * flow a real inbound message would. Anonymous on the backend (correction — this represents a
 * customer's message arriving, not a staff action), same as the public Web Form and Live Chat widget.
 */
export async function ingestWhatsApp(payload: InboundMessagePayload): Promise<InboundMessageResult> {
  const response = await http.post<InboundMessageResult>("/public/channels/whatsapp/inbound", payload);
  return response.data;
}

export async function ingestSms(payload: InboundMessagePayload): Promise<InboundMessageResult> {
  const response = await http.post<InboundMessageResult>("/public/channels/sms/inbound", payload);
  return response.data;
}
