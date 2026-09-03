import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { AxiosError, AxiosHeaders } from "axios";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { AuthContext } from "../../../auth/AuthContext";
import type { AuthState } from "../../../auth/AuthContext";
import * as attachmentsApi from "../attachmentsApi";
import { CustomerAttachmentsPanel } from "../CustomerAttachmentsPanel";
import type { CustomerAttachment } from "../types";

function attachment(overrides: Partial<CustomerAttachment> = {}): CustomerAttachment {
  return {
    id: "att-1",
    customerId: "cust-1",
    fileName: "contract.pdf",
    contentType: "application/pdf",
    sizeBytes: 2048,
    uploadedByUserId: "user-1",
    uploadedByDisplayName: "Alex Agent",
    uploadedAt: "2026-08-01T10:00:00Z",
    downloadUrl: "/api/customers/cust-1/attachments/att-1/download",
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

const ALL_PERMISSIONS = ["customers.attachments.read", "customers.attachments.create", "customers.attachments.delete"];

function renderPanel(permissions: string[] = ALL_PERMISSIONS) {
  return render(
    <AuthContext value={stubAuth(permissions)}>
      <CustomerAttachmentsPanel customerId="cust-1" />
    </AuthContext>,
  );
}

function badRequest(errorCode: string): AxiosError {
  const error = new AxiosError("Bad Request", "400");
  error.response = {
    status: 400,
    statusText: "Bad Request",
    data: { error: errorCode },
    headers: {},
    config: { headers: new AxiosHeaders() },
  };
  return error;
}

describe("CustomerAttachmentsPanel", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("shows a loading state before the fetch resolves", () => {
    vi.spyOn(attachmentsApi, "listAttachments").mockReturnValue(new Promise(() => {}));

    renderPanel();

    expect(screen.getByText("Loading…")).toBeInTheDocument();
  });

  it("shows an empty state when there are no attachments", async () => {
    vi.spyOn(attachmentsApi, "listAttachments").mockResolvedValue([]);

    renderPanel();

    expect(await screen.findByText("No attachments yet.")).toBeInTheDocument();
  });

  it("renders the fetched attachments", async () => {
    vi.spyOn(attachmentsApi, "listAttachments").mockResolvedValue([attachment()]);

    renderPanel();

    expect(await screen.findByText("contract.pdf")).toBeInTheDocument();
    expect(screen.getByText("2.0 KB")).toBeInTheDocument();
    expect(screen.getByText("Alex Agent")).toBeInTheDocument();
  });

  it("uploads a chosen file", async () => {
    vi.spyOn(attachmentsApi, "listAttachments").mockResolvedValue([]);
    const uploadAttachment = vi.spyOn(attachmentsApi, "uploadAttachment").mockResolvedValue(attachment());

    renderPanel();
    await screen.findByText("No attachments yet.");

    const file = new File(["%PDF-1.4"], "contract.pdf", { type: "application/pdf" });
    const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement;
    await userEvent.upload(fileInput, file);

    expect(uploadAttachment).toHaveBeenCalledWith("cust-1", file);
    expect(await screen.findByText("contract.pdf")).toBeInTheDocument();
  });

  it("shows a server validation error when the upload is rejected", async () => {
    vi.spyOn(attachmentsApi, "listAttachments").mockResolvedValue([]);
    vi.spyOn(attachmentsApi, "uploadAttachment").mockRejectedValue(badRequest("attachment.invalid_type"));

    renderPanel();
    await screen.findByText("No attachments yet.");

    const file = new File(["MZ"], "virus.exe", { type: "application/octet-stream" });
    const fileInput = document.querySelector('input[type="file"]') as HTMLInputElement;
    await userEvent.upload(fileInput, file, { applyAccept: false });

    expect(await screen.findByText("This file type is not supported.")).toBeInTheDocument();
  });

  it("downloads an attachment", async () => {
    vi.spyOn(attachmentsApi, "listAttachments").mockResolvedValue([attachment()]);
    const downloadAttachment = vi.spyOn(attachmentsApi, "downloadAttachment").mockResolvedValue(undefined);

    renderPanel();
    await screen.findByText("contract.pdf");

    await userEvent.click(screen.getByRole("button", { name: "Download" }));

    expect(downloadAttachment).toHaveBeenCalledWith("cust-1", "att-1", "contract.pdf");
  });

  it("deletes an attachment after confirmation", async () => {
    vi.spyOn(attachmentsApi, "listAttachments").mockResolvedValue([attachment()]);
    const deleteAttachment = vi.spyOn(attachmentsApi, "deleteAttachment").mockResolvedValue(undefined);
    vi.spyOn(window, "confirm").mockReturnValue(true);

    renderPanel();
    await screen.findByText("contract.pdf");

    await userEvent.click(screen.getByRole("button", { name: "Delete" }));

    expect(deleteAttachment).toHaveBeenCalledWith("cust-1", "att-1");
    expect(screen.queryByText("contract.pdf")).not.toBeInTheDocument();
  });

  it("hides the Upload button and Delete action when the caller lacks the corresponding permissions", async () => {
    vi.spyOn(attachmentsApi, "listAttachments").mockResolvedValue([attachment()]);

    renderPanel(["customers.attachments.read"]);
    await screen.findByText("contract.pdf");

    expect(screen.queryByRole("button", { name: "Upload" })).not.toBeInTheDocument();
    expect(screen.queryByRole("button", { name: "Delete" })).not.toBeInTheDocument();
  });
});
