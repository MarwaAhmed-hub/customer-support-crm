/** Mirrors the backend DTOs in `Api/Branches/BranchDtos.cs` (camelCase — System.Text.Json's default). */

export interface Branch {
  id: string;
  name: string;
  code: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreateBranchPayload {
  name: string;
  code: string | null;
}

export interface UpdateBranchPayload {
  name: string;
  code: string | null;
  isActive: boolean;
}
