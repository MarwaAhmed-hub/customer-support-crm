/** Mirrors the backend DTO in `Api/Customers/Attachments/CustomerAttachmentDtos.cs` (camelCase — System.Text.Json's default). */

export interface CustomerAttachment {
  id: string;
  customerId: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  uploadedByUserId: string | null;
  uploadedByDisplayName: string | null;
  uploadedAt: string;
  downloadUrl: string;
}
