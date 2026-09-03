import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AuthContext } from "../../features/auth/AuthContext";
import type { AuthState } from "../../features/auth/AuthContext";
import * as notificationsApi from "../../features/notifications/notificationsApi";
import type { Notification, NotificationListResponse } from "../../features/notifications/types";
import { BrandingContext } from "../../features/settings/BrandingContext";
import type { BrandingState } from "../../features/settings/BrandingContext";
import { AppLayout } from "../AppLayout";

const BRANDING_STUB: BrandingState = {
  branding: {
    applicationName: "Customer Support CRM",
    brandDisplayName: "Customer Support CRM",
    logoUrl: null,
    primaryColor: "#1976D2",
    secondaryColor: "#9C27B0",
  },
  refresh: () => undefined,
};

function notification(overrides: Partial<Notification> = {}): Notification {
  return {
    id: "notif-1",
    eventType: "TicketAssigned",
    slaType: null,
    ticketId: "ticket-1",
    subject: "Ticket assigned to you",
    body: 'You\'ve been assigned ticket "Cannot log in".',
    createdAtUtc: "2026-08-01T00:00:00Z",
    readAtUtc: null,
    ...overrides,
  };
}

function page(items: Notification[], total?: number): NotificationListResponse {
  return { items, total: total ?? items.length, page: 1, pageSize: 20 };
}

function stubAuth(permissions: string[]): AuthState {
  return {
    status: "authenticated",
    isAdmin: false,
    permissions,
    hasPermission: (code) => permissions.includes(code),
    hasAnyPermission: (codes) => codes.some((code) => permissions.includes(code)),
    user: { id: "u-1", email: "person@local.test", displayName: "A Person" },
    login: async () => undefined,
    logout: () => undefined,
  };
}

function renderLayout(permissions: string[]) {
  return render(
    <AuthContext value={stubAuth(permissions)}>
      <BrandingContext value={BRANDING_STUB}>
        <MemoryRouter initialEntries={["/"]}>
          <Routes>
            <Route path="/" element={<AppLayout>page content</AppLayout>} />
            <Route path="/tickets/:id" element={<div data-testid="ticket-page">ticket page</div>} />
          </Routes>
        </MemoryRouter>
      </BrandingContext>
    </AuthContext>,
  );
}

describe("AppLayout — notification bell", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("hides the bell entirely for a caller without notifications.view.own", () => {
    renderLayout([]);

    expect(screen.queryByLabelText("Notifications")).not.toBeInTheDocument();
  });

  it("shows the unread count as a badge for a caller with the permission", async () => {
    vi.spyOn(notificationsApi, "listMyNotifications").mockResolvedValue(page([notification(), notification({ id: "notif-2" })], 2));

    renderLayout(["notifications.view.own"]);

    expect(await screen.findByText("2")).toBeInTheDocument();
  });

  it("opens a dropdown with recent notifications when the bell is clicked", async () => {
    vi.spyOn(notificationsApi, "listMyNotifications").mockResolvedValue(page([notification()]));

    renderLayout(["notifications.view.own"]);
    await userEvent.click(await screen.findByLabelText("Notifications"));

    expect(await screen.findByText("Ticket assigned to you")).toBeInTheDocument();
    expect(screen.getByRole("menuitem", { name: /View all notifications/ })).toBeInTheDocument();
  });

  it("marks an unread notification read and navigates to its ticket when clicked", async () => {
    vi.spyOn(notificationsApi, "listMyNotifications").mockResolvedValue(page([notification()]));
    const markRead = vi.spyOn(notificationsApi, "markNotificationRead").mockResolvedValue(undefined);

    renderLayout(["notifications.view.own"]);
    await userEvent.click(await screen.findByLabelText("Notifications"));
    await userEvent.click(await screen.findByText("Ticket assigned to you"));

    expect(markRead).toHaveBeenCalledWith("notif-1");
    expect(await screen.findByTestId("ticket-page")).toBeInTheDocument();
  });
});
