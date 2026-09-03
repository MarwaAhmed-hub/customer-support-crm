import { http } from "../../lib/http";
import type { SlaPolicy, UpdateSlaPolicyPayload } from "./types";

/** Story 22: minimal admin surface over the SLA policies `ISlaService` applies at ticket creation. */
export async function listSlaPolicies(): Promise<SlaPolicy[]> {
  const response = await http.get<SlaPolicy[]>("/sla/policies");
  return response.data;
}

export async function updateSlaPolicy(id: string, payload: UpdateSlaPolicyPayload): Promise<SlaPolicy> {
  const response = await http.put<SlaPolicy>(`/sla/policies/${id}`, payload);
  return response.data;
}
