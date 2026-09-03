/** Mirrors the backend DTOs in `Api/Users/UserDtos.cs` (camelCase — System.Text.Json's default). */

/** `departmentId`/`branchId` and their denormalised names (Story 04) are additive on top of Story 03's shape. */
export interface UserListItem {
  id: string;
  email: string;
  displayName: string;
  isActive: boolean;
  departmentId: string | null;
  departmentName: string | null;
  branchId: string | null;
  branchName: string | null;
}

export interface UserRoleSummary {
  id: string;
  name: string;
}

/** `roles` (Story 03) is additive on top of Story 02's shape. */
export interface UserDetail extends UserListItem {
  createdAt: string;
  roles: UserRoleSummary[];
}

export interface CreateUserPayload {
  email: string;
  displayName: string;
  password: string;
  departmentId: string | null;
  branchId: string | null;
}

export interface UpdateUserPayload {
  email: string;
  displayName: string;
  departmentId: string | null;
  branchId: string | null;
}

export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  total: number;
}
