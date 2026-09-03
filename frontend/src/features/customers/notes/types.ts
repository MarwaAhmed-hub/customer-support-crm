/** Mirrors the backend DTOs in `Api/Customers/Notes/CustomerNoteDtos.cs` (camelCase — System.Text.Json's default). */

export interface CustomerNote {
  id: string;
  customerId: string;
  body: string;
  createdByUserId: string | null;
  createdByDisplayName: string | null;
  createdAt: string;
  updatedAt: string | null;
}
