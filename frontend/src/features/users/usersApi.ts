import { http } from "../../lib/http";
import type {
  CreateUserPayload,
  PagedResult,
  UpdateUserPayload,
  UserDetail,
  UserListItem,
} from "./types";

export interface ListUsersParams {
  page?: number;
  pageSize?: number;
  search?: string;
  departmentId?: string;
  branchId?: string;
}

export async function listUsers(params: ListUsersParams = {}): Promise<PagedResult<UserListItem>> {
  const response = await http.get<PagedResult<UserListItem>>("/users", { params });
  return response.data;
}

export async function getUser(id: string): Promise<UserDetail> {
  const response = await http.get<UserDetail>(`/users/${id}`);
  return response.data;
}

export async function createUser(payload: CreateUserPayload): Promise<UserDetail> {
  const response = await http.post<UserDetail>("/users", payload);
  return response.data;
}

export async function updateUser(id: string, payload: UpdateUserPayload): Promise<UserDetail> {
  const response = await http.put<UserDetail>(`/users/${id}`, payload);
  return response.data;
}

export async function setUserActive(id: string, isActive: boolean): Promise<UserDetail> {
  const action = isActive ? "activate" : "deactivate";
  const response = await http.post<UserDetail>(`/users/${id}/${action}`);
  return response.data;
}
