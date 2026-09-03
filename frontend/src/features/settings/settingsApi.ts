import { http } from "../../lib/http";
import type { SystemSettings, UpdateSystemSettingsRequest, UploadLogoResponse } from "./types";

export async function getSystemSettings(): Promise<SystemSettings> {
  const response = await http.get<SystemSettings>("/system-settings");
  return response.data;
}

export async function updateSystemSettings(payload: UpdateSystemSettingsRequest): Promise<SystemSettings> {
  const response = await http.put<SystemSettings>("/system-settings", payload);
  return response.data;
}

/**
 * Uploads a logo image file and returns the URL it now lives at. This alone does not persist
 * anything — the caller still has to put the returned `logoUrl` into the form and Save, exactly
 * like pasting an externally-hosted URL would.
 */
export async function uploadLogo(file: File): Promise<UploadLogoResponse> {
  const formData = new FormData();
  formData.append("file", file);
  // No explicit Content-Type here — axios sets `multipart/form-data` with the correct boundary
  // itself when the body is a FormData instance.
  const response = await http.post<UploadLogoResponse>("/system-settings/logo", formData);
  return response.data;
}
