import { http } from "../../lib/http";
import type {
  CreateRolePayload,
  PermissionCategory,
  Role,
  UpdateRolePayload,
  UserRoleSummary,
} from "./types";

export async function listRoles(): Promise<Role[]> {
  const response = await http.get<Role[]>("/roles");
  return response.data;
}

export async function getRole(id: string): Promise<Role> {
  const response = await http.get<Role>(`/roles/${id}`);
  return response.data;
}

export async function createRole(payload: CreateRolePayload): Promise<Role> {
  const response = await http.post<Role>("/roles", payload);
  return response.data;
}

export async function updateRole(id: string, payload: UpdateRolePayload): Promise<Role> {
  const response = await http.put<Role>(`/roles/${id}`, payload);
  return response.data;
}

export async function replaceRolePermissions(id: string, permissions: string[]): Promise<Role> {
  const response = await http.put<Role>(`/roles/${id}/permissions`, { permissions });
  return response.data;
}

export async function listPermissions(): Promise<PermissionCategory[]> {
  const response = await http.get<PermissionCategory[]>("/permissions");
  return response.data;
}

/**
 * The catalogue subset this role is eligible to hold — the Eligible Permissions Matrix row for
 * Manager/Agent/Customer, or the full catalogue for Administrator/custom roles. `RolePermissionsPage`
 * renders this instead of `listPermissions()`'s raw full catalogue, so e.g. the Customer role never
 * even shows a `users.*`/`roles.*` checkbox to hide — the backend never sends them.
 */
export async function listEligiblePermissions(roleId: string): Promise<PermissionCategory[]> {
  const response = await http.get<PermissionCategory[]>(`/roles/${roleId}/eligible-permissions`);
  return response.data;
}

export async function assignRoleToUser(userId: string, roleId: string): Promise<UserRoleSummary[]> {
  const response = await http.post<UserRoleSummary[]>(`/users/${userId}/roles`, { roleId });
  return response.data;
}

export async function removeRoleFromUser(userId: string, roleId: string): Promise<UserRoleSummary[]> {
  const response = await http.delete<UserRoleSummary[]>(`/users/${userId}/roles/${roleId}`);
  return response.data;
}

export async function getUserRoles(userId: string): Promise<UserRoleSummary[]> {
  const response = await http.get<UserRoleSummary[]>(`/users/${userId}/roles`);
  return response.data;
}
