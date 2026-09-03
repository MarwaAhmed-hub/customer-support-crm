/** Mirrors the backend DTOs in `Api/Sla/SlaPolicyDtos.cs` (camelCase — System.Text.Json's default). */

export interface SlaPolicy {
  id: string;
  priorityId: string | null;
  /** Null for the default policy (`priorityId` is also null there). */
  priorityName: string | null;
  name: string;
  firstResponseMinutes: number;
  resolutionMinutes: number;
  isActive: boolean;
}

export interface UpdateSlaPolicyPayload {
  firstResponseMinutes: number;
  resolutionMinutes: number;
  isActive: boolean;
}
