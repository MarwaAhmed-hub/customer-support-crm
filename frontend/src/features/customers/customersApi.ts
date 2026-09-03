import { http } from "../../lib/http";
import type { Customer, CreateCustomerPayload, UpdateCustomerPayload } from "./types";

export interface ListCustomersParams {
  search?: string;
}

export async function listCustomers(params: ListCustomersParams = {}): Promise<Customer[]> {
  const response = await http.get<Customer[]>("/customers", { params });
  return response.data;
}

export async function getCustomer(id: string): Promise<Customer> {
  const response = await http.get<Customer>(`/customers/${id}`);
  return response.data;
}

export async function createCustomer(payload: CreateCustomerPayload): Promise<Customer> {
  const response = await http.post<Customer>("/customers", payload);
  return response.data;
}

export async function updateCustomer(id: string, payload: UpdateCustomerPayload): Promise<Customer> {
  const response = await http.put<Customer>(`/customers/${id}`, payload);
  return response.data;
}

export async function deleteCustomer(id: string): Promise<void> {
  await http.delete(`/customers/${id}`);
}
