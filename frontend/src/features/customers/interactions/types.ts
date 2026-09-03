/** Mirrors the backend DTOs in `Api/Customers/Interactions/CustomerInteractionDtos.cs` (camelCase — System.Text.Json's default). */

export interface CustomerInteraction {
  id: string;
  customerId: string;
  occurredAt: string; // ISO, UTC
  interactionType: string;
  summary: string | null;
  details: string | null;
  userId: string | null;
  userDisplayName: string | null;
}

export interface CustomerInteractionListResponse {
  items: CustomerInteraction[];
  total: number;
  page: number;
  pageSize: number;
}
