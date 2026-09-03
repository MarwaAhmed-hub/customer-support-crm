import { http } from "../../lib/http";
import type { Branch, CreateBranchPayload, UpdateBranchPayload } from "./types";

export interface ListBranchesParams {
  includeInactive?: boolean;
}

export async function listBranches(params: ListBranchesParams = {}): Promise<Branch[]> {
  const response = await http.get<Branch[]>("/branches", { params });
  return response.data;
}

export async function getBranch(id: string): Promise<Branch> {
  const response = await http.get<Branch>(`/branches/${id}`);
  return response.data;
}

export async function createBranch(payload: CreateBranchPayload): Promise<Branch> {
  const response = await http.post<Branch>("/branches", payload);
  return response.data;
}

export async function updateBranch(id: string, payload: UpdateBranchPayload): Promise<Branch> {
  const response = await http.put<Branch>(`/branches/${id}`, payload);
  return response.data;
}
