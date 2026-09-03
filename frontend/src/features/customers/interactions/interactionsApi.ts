import { http } from "../../../lib/http";
import type { CustomerInteractionListResponse } from "./types";

export interface ListCustomerInteractionsParams {
  page?: number;
  pageSize?: number;
  /** Narrows the customer's history down to interactions tied to one ticket. */
  ticketId?: string;
}

export async function listCustomerInteractions(
  customerId: string,
  params: ListCustomerInteractionsParams = {},
): Promise<CustomerInteractionListResponse> {
  const response = await http.get<CustomerInteractionListResponse>(`/customers/${customerId}/interactions`, {
    params: { page: params.page ?? 1, pageSize: params.pageSize ?? 25, ticketId: params.ticketId },
  });
  return response.data;
}
