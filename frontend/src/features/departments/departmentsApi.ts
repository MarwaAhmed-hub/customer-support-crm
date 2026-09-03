import { http } from "../../lib/http";
import type { CreateDepartmentPayload, Department, UpdateDepartmentPayload } from "./types";

export interface ListDepartmentsParams {
  includeInactive?: boolean;
}

export async function listDepartments(params: ListDepartmentsParams = {}): Promise<Department[]> {
  const response = await http.get<Department[]>("/departments", { params });
  return response.data;
}

export async function getDepartment(id: string): Promise<Department> {
  const response = await http.get<Department>(`/departments/${id}`);
  return response.data;
}

export async function createDepartment(payload: CreateDepartmentPayload): Promise<Department> {
  const response = await http.post<Department>("/departments", payload);
  return response.data;
}

export async function updateDepartment(id: string, payload: UpdateDepartmentPayload): Promise<Department> {
  const response = await http.put<Department>(`/departments/${id}`, payload);
  return response.data;
}
