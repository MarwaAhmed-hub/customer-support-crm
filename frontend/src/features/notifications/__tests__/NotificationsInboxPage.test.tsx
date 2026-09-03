import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { NotificationsInboxPage } from "../NotificationsInboxPage";
import * as notificationsApi from "../notificationsApi";
import type { Notification, NotificationListResponse } from "../types";

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

function renderPage() {
  return render(
    <MemoryRouter>
      <NotificationsInboxPage />
    </MemoryRouter>,
  );
}

describe("NotificationsInboxPage", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("renders the list returned by the API", async () => {
    vi.spyOn(notificationsApi, "listMyNotifications").mockResolvedValue(page([notification()]));

    renderPage();

    expect(await screen.findByText("Ticket assigned to you")).toBeInTheDocument();
    expect(screen.getByText("Assigned")).toBeInTheDocument();
  });

  it("shows the empty state when there are no notifications", async () => {
    vi.spyOn(notificationsApi, "listMyNotifications").mockResolvedValue(page([]));

    renderPage();

    expect(await screen.findByText("No notifications yet.")).toBeInTheDocument();
  });

  it("calls markRead and updates the row when 'Mark read' is clicked", async () => {
    vi.spyOn(notificationsApi, "listMyNotifications").mockResolvedValue(page([notification()]));
    const markRead = vi.spyOn(notificationsApi, "markNotificationRead").mockResolvedValue(undefined);

    renderPage();
    await screen.findByText("Ticket assigned to you");
    await userEvent.click(screen.getByRole("button", { name: "Mark read" }));

    expect(markRead).toHaveBeenCalledWith("notif-1");
    expect(await screen.findByRole("button", { name: "Mark all as read" })).toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Mark read" })).not.toBeInTheDocument();
  });

  it("calls markAllRead when 'Mark all as read' is clicked", async () => {
    vi.spyOn(notificationsApi, "listMyNotifications").mockResolvedValue(page([notification()]));
    const markAllRead = vi.spyOn(notificationsApi, "markAllNotificationsRead").mockResolvedValue(undefined);

    renderPage();
    await screen.findByText("Ticket assigned to you");
    await userEvent.click(screen.getByRole("button", { name: "Mark all as read" }));

    expect(markAllRead).toHaveBeenCalled();
  });

  it("re-fetches with unreadOnly: true when the toggle is switched on", async () => {
    const listMyNotifications = vi.spyOn(notificationsApi, "listMyNotifications").mockResolvedValue(page([notification()]));

    renderPage();
    await screen.findByText("Ticket assigned to you");
    listMyNotifications.mockClear();
    listMyNotifications.mockResolvedValue(page([]));

    await userEvent.click(screen.getByRole("switch", { name: "Unread only" }));

    await screen.findByText("No unread notifications.");
    expect(listMyNotifications).toHaveBeenCalledWith(expect.objectContaining({ unreadOnly: true }));
  });
});
