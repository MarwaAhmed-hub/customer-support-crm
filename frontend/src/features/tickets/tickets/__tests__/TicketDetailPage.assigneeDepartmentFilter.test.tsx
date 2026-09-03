import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AuthContext } from "../../../auth/AuthContext";
import type { AuthState } from "../../../auth/AuthContext";
import * as usersApi from "../../../users/usersApi";
import type { PagedResult, UserListItem } from "../../../users/types";
import * as collaborationApi from "../collaboration/collaborationApi";
import * as ticketHistoryApi from "../history/ticketHistoryApi";
import { TicketDetailPage } from "../TicketDetailPage";
import * as ticketsApi from "../ticketsApi";
import type { TicketDetail } from "../types";

const BASE_TICKET: TicketDetail = {
  id: "ticket-1",
  customerId: "customer-1",
  customerName: "Jane Doe",
  subject: "Cannot log in",
  categoryId: "cat-1",
  categoryName: "Complaints",
  priorityId: "pri-1",
  priorityName: "High",
  status: "open",
  createdByUserId: "creator-1",
  createdByUserName: "Local Admin",
  assignedUserId: null,
  assignedUserName: null,
  isEscalated: false,
  createdAt: "2026-08-01T00:00:00Z",
  updatedAt: "2026-08-01T00:00:00Z",
  description: "Customer cannot access their account.",
  escalatedAt: null,
  escalatedByUserId: null,
  escalatedByUserName: null,
  escalationReason: null,
  sourceChannel: null,
  sla: null,
  categoryDepartmentId: null,
};

function user(id: string, displayName: string, departmentId: string | null): UserListItem {
  return { id, email: `${id}@local.test`, displayName, isActive: true, departmentId, departmentName: null, branchId: null, branchName: null };
}

function paged(items: UserListItem[]): PagedResult<UserListItem> {
  return { items, page: 1, pageSize: 100, total: items.length };
}

function stubAuth(permissions: string[]): AuthState {
  return {
    status: "authenticated",
    isAdmin: false,
    permissions,
    hasPermission: (code) => permissions.includes(code),
    hasAnyPermission: (codes) => codes.some((code) => permissions.includes(code)),
    user: { id: "current", email: "current@local.test", displayName: "Current User" },
    login: async () => undefined,
    logout: () => undefined,
  };
}

function renderPage() {
  return render(
    <AuthContext value={stubAuth(["tickets.assign"])}>
      <MemoryRouter initialEntries={["/tickets/ticket-1"]}>
        <Routes>
          <Route path="/tickets/:id" element={<TicketDetailPage />} />
        </Routes>
      </MemoryRouter>
    </AuthContext>,
  );
}

describe("TicketDetailPage — assignee picker scoped to the category's department", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
    vi.spyOn(ticketHistoryApi, "getTicketHistory").mockResolvedValue([]);
    vi.spyOn(collaborationApi, "listCollaborationComments").mockResolvedValue([]);
  });

  it("requests only users in the category's department when it has one", async () => {
    vi.spyOn(ticketsApi, "getTicket").mockResolvedValue({ ...BASE_TICKET, categoryDepartmentId: "dept-complaints" });
    const listUsers = vi
      .spyOn(usersApi, "listUsers")
      .mockResolvedValue(paged([user("agent-1", "Complaints Agent", "dept-complaints")]));

    renderPage();

    await screen.findByText("Assignment");
    await userEvent.click(screen.getByLabelText("Assignee"));
    expect(await screen.findByRole("option", { name: "Complaints Agent" })).toBeInTheDocument();
    expect(listUsers).toHaveBeenCalledWith({ pageSize: 100, departmentId: "dept-complaints" });
  });

  it("falls back to every active user when the category has no department", async () => {
    vi.spyOn(ticketsApi, "getTicket").mockResolvedValue({ ...BASE_TICKET, categoryDepartmentId: null });
    const listUsers = vi
      .spyOn(usersApi, "listUsers")
      .mockResolvedValue(paged([user("agent-1", "Any Agent", null)]));

    renderPage();

    await screen.findByText("Assignment");
    await userEvent.click(screen.getByLabelText("Assignee"));
    expect(await screen.findByRole("option", { name: "Any Agent" })).toBeInTheDocument();
    expect(listUsers).toHaveBeenCalledWith({ pageSize: 100 });
  });

  it("does NOT fall back to other departments' users when the category's department has none active — the picker stays empty", async () => {
    vi.spyOn(ticketsApi, "getTicket").mockResolvedValue({ ...BASE_TICKET, categoryDepartmentId: "dept-billing" });
    const listUsers = vi.spyOn(usersApi, "listUsers").mockResolvedValue(paged([]));

    renderPage();

    await screen.findByText("Assignment");
    await userEvent.click(screen.getByLabelText("Assignee"));
    expect(await screen.findByRole("option", { name: "— Unassigned —" })).toBeInTheDocument();
    expect(screen.queryByRole("option", { name: /Agent/ })).not.toBeInTheDocument();
    expect(listUsers).toHaveBeenCalledWith({ pageSize: 100, departmentId: "dept-billing" });
    expect(listUsers).not.toHaveBeenCalledWith({ pageSize: 100 });
  });

  it("shows a dedicated error when the backend rejects an assign attempt across departments", async () => {
    vi.spyOn(ticketsApi, "getTicket").mockResolvedValue({ ...BASE_TICKET, categoryDepartmentId: "dept-complaints" });
    vi.spyOn(usersApi, "listUsers").mockResolvedValue(paged([user("agent-1", "Complaints Agent", "dept-complaints")]));
    vi.spyOn(ticketsApi, "updateTicketAssignment").mockRejectedValue({
      isAxiosError: true,
      response: { status: 400, data: { error: "assigned_user_outside_department" } },
    });

    renderPage();

    await screen.findByText("Assignment");
    await userEvent.click(screen.getByLabelText("Assignee"));
    await userEvent.click(await screen.findByRole("option", { name: "Complaints Agent" }));
    await userEvent.click(screen.getByRole("button", { name: "Save assignment" }));

    expect(await screen.findByText("This user isn't in the department this ticket's category requires. Please pick another.")).toBeInTheDocument();
  });
});
