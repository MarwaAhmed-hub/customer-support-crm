import { http } from "../../../lib/http";
import type { CustomerAttachment } from "./types";

export async function listAttachments(customerId: string): Promise<CustomerAttachment[]> {
  const response = await http.get<CustomerAttachment[]>(`/customers/${customerId}/attachments`);
  return response.data;
}

export async function uploadAttachment(customerId: string, file: File): Promise<CustomerAttachment> {
  const formData = new FormData();
  formData.append("file", file);
  // No explicit Content-Type here — axios sets `multipart/form-data` with the correct boundary
  // itself when the body is a FormData instance (same pattern as settingsApi.uploadLogo).
  const response = await http.post<CustomerAttachment>(`/customers/${customerId}/attachments`, formData);
  return response.data;
}

/**
 * The download endpoint is permission-gated, so a plain `<a href>` to it wouldn't carry the bearer
 * token — a raw browser navigation never goes through the `http` client's request interceptor.
 * Fetching the bytes as a Blob through `http` (token included) and triggering a synthetic click on an
 * object URL is what actually saves the file, matching `settingsApi`'s branding-logo preview pattern.
 */
export async function downloadAttachment(customerId: string, attachmentId: string, fileName: string): Promise<void> {
  const response = await http.get<Blob>(`/customers/${customerId}/attachments/${attachmentId}/download`, {
    responseType: "blob",
  });

  const blobUrl = URL.createObjectURL(response.data);
  const link = document.createElement("a");
  link.href = blobUrl;
  link.download = fileName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  URL.revokeObjectURL(blobUrl);
}

export async function deleteAttachment(customerId: string, attachmentId: string): Promise<void> {
  await http.delete(`/customers/${customerId}/attachments/${attachmentId}`);
}
