import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { MemoryRouter, Route, Routes, useLocation } from "react-router-dom";
import { beforeEach, describe, expect, it, vi } from "vitest";
import * as quickRepliesApi from "../quickRepliesApi";
import { QuickReplyFormPage } from "../QuickReplyFormPage";
import type { QuickReply } from "../types";

const EXISTING: QuickReply = {
  id: "qr-1",
  title: "Greeting",
  body: "Hello, thanks for reaching out!",
  isActive: true,
  createdAt: "2026-08-01T00:00:00Z",
  updatedAt: "2026-08-01T00:00:00Z",
};

function LandingProbe() {
  const location = useLocation();
  return <div data-testid="landed">landed on {location.pathname}</div>;
}

function renderCreate() {
  return render(
    <MemoryRouter initialEntries={["/quick-replies/new"]}>
      <Routes>
        <Route path="/quick-replies/new" element={<QuickReplyFormPage />} />
        <Route path="/quick-replies" element={<LandingProbe />} />
      </Routes>
    </MemoryRouter>,
  );
}

function renderEdit(id: string) {
  return render(
    <MemoryRouter initialEntries={[`/quick-replies/${id}/edit`]}>
      <Routes>
        <Route path="/quick-replies/:id/edit" element={<QuickReplyFormPage />} />
        <Route path="/quick-replies" element={<LandingProbe />} />
      </Routes>
    </MemoryRouter>,
  );
}

describe("QuickReplyFormPage — create mode", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("shows validation errors when title and body are blank", async () => {
    renderCreate();

    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    expect(await screen.findByText("Title is required.")).toBeInTheDocument();
    expect(screen.getByText("Body is required.")).toBeInTheDocument();
  });

  it("submits the trimmed title and body and navigates back to the list", async () => {
    const createQuickReply = vi.spyOn(quickRepliesApi, "createQuickReply").mockResolvedValue(EXISTING);

    renderCreate();
    await userEvent.type(screen.getByLabelText("Title"), "  Greeting  ");
    await userEvent.type(screen.getByLabelText("Body"), "  Hello there  ");
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    await vi.waitFor(() =>
      expect(createQuickReply).toHaveBeenCalledWith({ title: "Greeting", body: "Hello there" }),
    );
    expect(await screen.findByTestId("landed")).toHaveTextContent("landed on /quick-replies");
  });

  it("shows a duplicate-title error on a 409 response", async () => {
    vi.spyOn(quickRepliesApi, "createQuickReply").mockRejectedValue({
      isAxiosError: true,
      response: { status: 409, data: { error: "duplicate_quick_reply_title" } },
    });

    renderCreate();
    await userEvent.type(screen.getByLabelText("Title"), "Greeting");
    await userEvent.type(screen.getByLabelText("Body"), "Hello");
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    expect(await screen.findByText("A quick reply with this title already exists.")).toBeInTheDocument();
  });

  it("does not show the Active switch in create mode", () => {
    renderCreate();

    expect(screen.queryByLabelText("Active")).not.toBeInTheDocument();
  });
});

describe("QuickReplyFormPage — edit mode", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("prefills title, body, and active state from the existing quick reply", async () => {
    vi.spyOn(quickRepliesApi, "getQuickReply").mockResolvedValue(EXISTING);

    renderEdit("qr-1");

    expect(await screen.findByDisplayValue("Greeting")).toBeInTheDocument();
    expect(screen.getByDisplayValue("Hello, thanks for reaching out!")).toBeInTheDocument();
    expect(screen.getByLabelText("Active")).toBeChecked();
  });

  it("submits the update including isActive and navigates back to the list", async () => {
    vi.spyOn(quickRepliesApi, "getQuickReply").mockResolvedValue(EXISTING);
    const updateQuickReply = vi.spyOn(quickRepliesApi, "updateQuickReply").mockResolvedValue({ ...EXISTING, isActive: false });

    renderEdit("qr-1");
    await screen.findByDisplayValue("Greeting");
    await userEvent.click(screen.getByLabelText("Active"));
    await userEvent.click(screen.getByRole("button", { name: "Save" }));

    await vi.waitFor(() =>
      expect(updateQuickReply).toHaveBeenCalledWith("qr-1", {
        title: "Greeting",
        body: "Hello, thanks for reaching out!",
        isActive: false,
      }),
    );
    expect(await screen.findByTestId("landed")).toHaveTextContent("landed on /quick-replies");
  });
});
