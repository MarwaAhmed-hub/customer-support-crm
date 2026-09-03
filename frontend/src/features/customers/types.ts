/** Mirrors the backend DTOs in `Api/Customers/CustomerDtos.cs` (camelCase — System.Text.Json's default). */

export interface Customer {
  id: string;
  firstName: string;
  lastName: string;
  companyName: string | null;
  email: string | null;
  phone: string | null;
  createdAt: string;
  updatedAt: string;
}

export interface CreateCustomerPayload {
  firstName: string;
  lastName: string;
  companyName: string | null;
  email: string | null;
  phone: string | null;
}

export interface UpdateCustomerPayload {
  firstName: string;
  lastName: string;
  companyName: string | null;
  email: string | null;
  phone: string | null;
}
