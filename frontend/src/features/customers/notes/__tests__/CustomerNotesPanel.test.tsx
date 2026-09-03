import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AuthContext } from "../../../auth/AuthContext";
import type { AuthState } from "../../../auth/AuthContext";
import * as notesApi from "../notesApi";
import { CustomerNotesPanel } from "../CustomerNotesPanel";
import type { CustomerNote } from "../types";

function note(overrides: Partial<CustomerNote> = {}): CustomerNote {
  return {
    id: "note-1",
    customerId: "cust-1",
    body: "Called back, left a message.",
    createdByUserId: "user-1",
    createdByDisplayName: "Alex Agent",
    createdAt: "2026-08-01T10:00:00Z",
    updatedAt: null,
    ...overrides,
  };
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

const ALL_PERMISSIONS = ["customers.notes.read", "customers.notes.create", "customers.notes.update", "customers.notes.delete"];

function renderPanel(permissions: string[] = ALL_PERMISSIONS) {
  return render(
    <AuthContext value={stubAuth(permissions)}>
      <CustomerNotesPanel customerId="cust-1" />
    </AuthContext>,
  );
}

describe("CustomerNotesPanel", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("shows a loading state before the fetch resolves", () => {
    vi.spyOn(notesApi, "listNotes").mockReturnValue(new Promise(() => {}));

    renderPanel();

    expect(screen.getByText("Loading…")).toBeInTheDocument();
  });

  it("shows an empty state when there are no notes", async () => {
    vi.spyOn(notesApi, "listNotes").mockResolvedValue([]);

    renderPanel();

    expect(await screen.findByText("No notes yet.")).toBeInTheDocument();
  });

  it("renders the fetched notes", async () => {
    vi.spyOn(notesApi, "listNotes").mockResolvedValue([note()]);

    renderPanel();

    expect(await screen.findByText("Called back, left a message.")).toBeInTheDocument();
    expect(screen.getByText(/Alex Agent/)).toBeInTheDocument();
  });

  it("adds a note and prepends it to the list", async () => {
    vi.spyOn(notesApi, "listNotes").mockResolvedValue([]);
    const createNote = vi.spyOn(notesApi, "createNote").mockResolvedValue(note({ id: "note-2", body: "New note" }));

    renderPanel();
    await screen.findByText("No notes yet.");

    await userEvent.type(screen.getByPlaceholderText("Add a note…"), "New note");
    await userEvent.click(screen.getByRole("button", { name: "Add" }));

    expect(createNote).toHaveBeenCalledWith("cust-1", "New note");
    expect(await screen.findByText("New note")).toBeInTheDocument();
  });

  it("deletes a note after confirmation", async () => {
    vi.spyOn(notesApi, "listNotes").mockResolvedValue([note()]);
    const deleteNote = vi.spyOn(notesApi, "deleteNote").mockResolvedValue(undefined);
    vi.spyOn(window, "confirm").mockReturnValue(true);

    renderPanel();
    await screen.findByText("Called back, left a message.");

    await userEvent.click(screen.getByRole("button", { name: "Delete" }));

    expect(deleteNote).toHaveBeenCalledWith("cust-1", "note-1");
    expect(screen.queryByText("Called back, left a message.")).not.toBeInTheDocument();
  });

  it("hides the add form and edit/delete actions when the caller lacks the corresponding permissions", async () => {
    vi.spyOn(notesApi, "listNotes").mockResolvedValue([note()]);

    renderPanel(["customers.notes.read"]);
    await screen.findByText("Called back, left a message.");

    expect(screen.queryByPlaceholderText("Add a note…")).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Edit" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Delete" })).not.toBeInTheDocument();
  });
});
