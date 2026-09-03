import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AxiosError, AxiosHeaders } from "axios";
import type { AxiosResponse, InternalAxiosRequestConfig } from "axios";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import * as branchesApi from "../../branches/branchesApi";
import type { Branch } from "../../branches/types";
import * as departmentsApi from "../../departments/departmentsApi";
import type { Department } from "../../departments/types";
import * as usersApi from "../usersApi";
import { UserFormPage } from "../UserFormPage";
import type { UserDetail } from "../types";

const EXISTING: UserDetail = {
  id: "u-1",
  email: "existing@local.test",
  displayName: "Existing User",
  isActive: true,
  createdAt: "2026-01-01T00:00:00Z",
  roles: [],
  departmentId: null,
  departmentName: null,
  branchId: null,
  branchName: null,
};

const SUPPORT_DEPARTMENT: Department = {
  id: "dept-1",
  name: "Support",
  code: null,
  isActive: true,
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: "2026-01-01T00:00:00Z",
};

const CAIRO_BRANCH: Branch = {
  id: "branch-1",
  name: "Cairo",
  code: null,
  isActive: true,
  createdAt: "2026-01-01T00:00:00Z",
  updatedAt: "2026-01-01T00:00:00Z",
};

function axiosErrorWith(status: number): AxiosError {
  const config = { url: "/users", headers: new AxiosHeaders() } as InternalAxiosRequestConfig;
  const error = new AxiosError("failed", String(status), config);
  error.response = { status, statusText: "", data: {}, headers: {}, config } as AxiosResponse;
  return error;
}

function LandingProbe() {
  const location = useLocation();
  return <div data-testid="landed">landed on {location.pathname}</div>;
}

function renderCreate() {
  return render(
    <MemoryRouter initialEntries={["/users/new"]}>
      <Routes>
        <Route path="/users/new" element={<UserFormPage />} />
        <Route path="/users/:id" element={<LandingProbe />} />
      </Routes>
    </MemoryRouter>,
  );
}

function renderEdit(id: string) {
  return render(
    <MemoryRouter initialEntries={[`/users/${id}/edit`]}>
      <Routes>
        <Route path="/users/:id/edit" element={<UserFormPage />} />
        <Route path="/users/:id" element={<LandingProbe />} />
      </Routes>
    </MemoryRouter>,
  );
}

async function fillCreateForm(overrides: { email?: string; displayName?: string; password?: string } = {}) {
  const email = overrides.email ?? "new@local.test";
  const displayName = overrides.displayName ?? "New User";
  const password = overrides.password ?? "TempPass!23";

  if (email.length > 0) await userEvent.type(screen.getByLabelText("Email"), email);
  if (displayName.length > 0) await userEvent.type(screen.getByLabelText("Name"), displayName);
  if (password.length > 0) await userEvent.type(screen.getByLabelText("Temporary password"), password);

  await userEvent.click(screen.getByRole("button", { name: "Save" }));
}

describe("UserFormPage — create mode", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    // Every test in this file exercises the email/name/password flow, not the department/branch
    // pickers specifically — default the picker sources to empty so those tests don't need to know
    // about them. Tests that DO care override these with their own vi.spyOn call below.
    vi.spyOn(departmentsApi, "listDepartments").mockResolvedValue([]);
    vi.spyOn(branchesApi, "listBranches").mockResolvedValue([]);
  });

  it("shows validation errors for missing required fields", async () => {
    renderCreate();

    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    expect(await screen.findByText("Email is required.")).toBeInTheDocument();
    expect(screen.getByText("Name is required.")).toBeInTheDocument();
    expect(screen.getByText("Password is required.")).toBeInTheDocument();
  });

  it("shows a validation error for a malformed email", async () => {
    renderCreate();

    await fillCreateForm({ email: "not-an-email" });

    expect(await screen.findByText("Enter a valid email address.")).toBeInTheDocument();
  });

  it("shows a validation error for a short password", async () => {
    renderCreate();

    await fillCreateForm({ password: "short" });

    expect(await screen.findByText("Password must be at least 8 characters.")).toBeInTheDocument();
  });

  it("submits and navigates to the new user's detail page", async () => {
    const createUser = vi.spyOn(usersApi, "createUser").mockResolvedValue({
      id: "new-id",
      email: "new@local.test",
      displayName: "New User",
      isActive: true,
      createdAt: "2026-01-01T00:00:00Z",
      roles: [],
      departmentId: null,
      departmentName: null,
      branchId: null,
      branchName: null,
    });

    renderCreate();
    await fillCreateForm();

    expect(createUser).toHaveBeenCalledWith({
      email: "new@local.test",
      displayName: "New User",
      password: "TempPass!23",
      departmentId: null,
      branchId: null,
    });
    expect(await screen.findByTestId("landed")).toHaveTextContent("landed on /users/new-id");
  });

  it("populates the department and branch dropdowns and defaults to '— none —'", async () => {
    vi.spyOn(departmentsApi, "listDepartments").mockResolvedValue([SUPPORT_DEPARTMENT]);
    vi.spyOn(branchesApi, "listBranches").mockResolvedValue([CAIRO_BRANCH]);

    renderCreate();

    expect(await screen.findAllByText("— none —")).toHaveLength(2);

    await userEvent.click(screen.getByLabelText("Department"));
    expect(await screen.findByRole("option", { name: "Support" })).toBeInTheDocument();
    await userEvent.click(screen.getByRole("option", { name: "Support" }));

    await userEvent.click(screen.getByLabelText("Branch"));
    expect(await screen.findByRole("option", { name: "Cairo" })).toBeInTheDocument();
  });

  it("includes the selected department and branch in the submit payload", async () => {
    vi.spyOn(departmentsApi, "listDepartments").mockResolvedValue([SUPPORT_DEPARTMENT]);
    vi.spyOn(branchesApi, "listBranches").mockResolvedValue([CAIRO_BRANCH]);
    const createUser = vi.spyOn(usersApi, "createUser").mockResolvedValue({
      id: "new-id",
      email: "new@local.test",
      displayName: "New User",
      isActive: true,
      createdAt: "2026-01-01T00:00:00Z",
      roles: [],
      departmentId: "dept-1",
      departmentName: "Support",
      branchId: "branch-1",
      branchName: "Cairo",
    });

    renderCreate();
    await userEvent.click(screen.getByLabelText("Department"));
    await userEvent.click(await screen.findByRole("option", { name: "Support" }));
    await userEvent.click(screen.getByLabelText("Branch"));
    await userEvent.click(await screen.findByRole("option", { name: "Cairo" }));
    await fillCreateForm();

    expect(createUser).toHaveBeenCalledWith({
      email: "new@local.test",
      displayName: "New User",
      password: "TempPass!23",
      departmentId: "dept-1",
      branchId: "branch-1",
    });
  });

  it("surfaces a 409 as a field-level error on Email", async () => {
    vi.spyOn(usersApi, "createUser").mockRejectedValue(axiosErrorWith(409));

    renderCreate();
    await fillCreateForm();

    expect(await screen.findByText("This email is already in use.")).toBeInTheDocument();
  });

  it("surfaces a non-409 server failure as a form-level error", async () => {
    vi.spyOn(usersApi, "createUser").mockRejectedValue(axiosErrorWith(500));

    renderCreate();
    await fillCreateForm();

    expect(await screen.findByRole("alert")).toHaveTextContent("Something went wrong. Please try again.");
  });
});

describe("UserFormPage — edit mode", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    // Every test in this file exercises the email/name/password flow, not the department/branch
    // pickers specifically — default the picker sources to empty so those tests don't need to know
    // about them. Tests that DO care override these with their own vi.spyOn call below.
    vi.spyOn(departmentsApi, "listDepartments").mockResolvedValue([]);
    vi.spyOn(branchesApi, "listBranches").mockResolvedValue([]);
  });

  it("prefills the form from the existing user and has no password field", async () => {
    vi.spyOn(usersApi, "getUser").mockResolvedValue(EXISTING);

    renderEdit("u-1");

    expect(await screen.findByDisplayValue("existing@local.test")).toBeInTheDocument();
    expect(screen.getByDisplayValue("Existing User")).toBeInTheDocument();
    expect(screen.queryByLabelText("Temporary password")).not.toBeInTheDocument();
  });

  it("submits the update and navigates to the detail page", async () => {
    vi.spyOn(usersApi, "getUser").mockResolvedValue(EXISTING);
    const updateUser = vi.spyOn(usersApi, "updateUser").mockResolvedValue({ ...EXISTING, displayName: "Renamed" });

    renderEdit("u-1");
    const nameInput = await screen.findByDisplayValue("Existing User");
    await userEvent.clear(nameInput);
    await userEvent.type(nameInput, "Renamed");

    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    await waitFor(() =>
      expect(updateUser).toHaveBeenCalledWith("u-1", {
        email: "existing@local.test",
        displayName: "Renamed",
        departmentId: null,
        branchId: null,
      }),
    );
    expect(await screen.findByTestId("landed")).toHaveTextContent("landed on /users/u-1");
  });

  it("prefills the department and branch pickers, keeping an assignment even if it is no longer active", async () => {
    vi.spyOn(usersApi, "getUser").mockResolvedValue({
      ...EXISTING,
      departmentId: "dept-retired",
      departmentName: "Retired Dept",
      branchId: "branch-1",
      branchName: "Cairo",
    });
    vi.spyOn(departmentsApi, "listDepartments").mockResolvedValue([]); // "Retired Dept" is inactive, so absent from the active-only list
    vi.spyOn(branchesApi, "listBranches").mockResolvedValue([CAIRO_BRANCH]);

    renderEdit("u-1");

    expect(await screen.findByText("Retired Dept (inactive)")).toBeInTheDocument();
    expect(screen.getByText("Cairo")).toBeInTheDocument();
  });
});
