/** Mirrors the backend DTOs in `Api/Departments/DepartmentDtos.cs` (camelCase — System.Text.Json's default). */

export interface Department {
  id: string;
  name: string;
  code: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface CreateDepartmentPayload {
  name: string;
  code: string | null;
}

export interface UpdateDepartmentPayload {
  name: string;
  code: string | null;
  isActive: boolean;
}
