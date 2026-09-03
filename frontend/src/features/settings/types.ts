/** Mirrors the backend DTOs in `Api/SystemSettings/SystemSettingsDtos.cs` (camelCase — System.Text.Json's default). */

export interface SystemSettings {
  applicationName: string;
  supportEmail: string;
  defaultTimezone: string;
  defaultCulture: string;
  brandDisplayName: string;
  logoUrl: string | null;
  primaryColor: string;
  secondaryColor: string;
  updatedAtUtc: string;
}

export interface UpdateSystemSettingsRequest {
  applicationName: string;
  supportEmail: string;
  defaultTimezone: string;
  defaultCulture: string;
  brandDisplayName: string;
  logoUrl: string | null;
  primaryColor: string;
  secondaryColor: string;
}

export interface UploadLogoResponse {
  logoUrl: string;
}
