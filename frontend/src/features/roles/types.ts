/** Mirrors the backend DTOs in `Api/Roles/RoleDtos.cs` and `Api/Roles/PermissionsController.cs` (camelCase — System.Text.Json's default). */

// GET/POST/DELETE .../users/{id}/roles all return this shape (Api/Users/UserRolesService.cs's
// UserRoleDto) — reusing the users feature's type instead of redeclaring it here.
export type { UserRoleSummary } from "../users/types";

export interface Role {
  id: string;
  name: string;
  description: string | null;
  isSystem: boolean;
  permissions: string[];
}

export interface PermissionDefinition {
  code: string;
  category: string;
  displayName: string;
  description: string | null;
}

export interface PermissionCategory {
  category: string;
  permissions: PermissionDefinition[];
}

export interface CreateRolePayload {
  name: string;
  description: string | null;
}

export interface UpdateRolePayload {
  name: string;
  description: string | null;
}
