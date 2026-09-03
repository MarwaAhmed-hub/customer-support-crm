import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it, vi } from "vitest";
import * as quickRepliesApi from "../quickRepliesApi";
import { QuickReplyPicker } from "../QuickReplyPicker";
import type { QuickReply } from "../types";

function quickReply(overrides: Partial<QuickReply> = {}): QuickReply {
  return {
    id: "qr-1",
    title: "Greeting",
    body: "Hello, thanks for reaching out!",
    isActive: true,
    createdAt: "2026-08-31T00:00:00Z",
    updatedAt: "2026-08-31T00:00:00Z",
    ...overrides,
  };
}

describe("QuickReplyPicker", () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it("does not fetch quick replies until the picker is opened", () => {
    const listQuickReplies = vi.spyOn(quickRepliesApi, "listQuickReplies");

    render(<QuickReplyPicker onInsert={() => undefined} />);

    expect(listQuickReplies).not.toHaveBeenCalled();
  });

  it("lists active quick replies once opened", async () => {
    vi.spyOn(quickRepliesApi, "listQuickReplies").mockResolvedValue([quickReply()]);

    render(<QuickReplyPicker onInsert={() => undefined} />);
    await userEvent.click(screen.getByRole("button", { name: "Quick reply" }));

    expect(await screen.findByText("Greeting")).toBeInTheDocument();
  });

  it("calls onInsert with the selected quick reply's body and closes the popover", async () => {
    vi.spyOn(quickRepliesApi, "listQuickReplies").mockResolvedValue([quickReply()]);
    const onInsert = vi.fn();

    render(<QuickReplyPicker onInsert={onInsert} />);
    await userEvent.click(screen.getByRole("button", { name: "Quick reply" }));
    await userEvent.click(await screen.findByText("Greeting"));

    expect(onInsert).toHaveBeenCalledWith("Hello, thanks for reaching out!");
    await vi.waitFor(() => expect(screen.queryByText("Greeting")).not.toBeInTheDocument());
  });

  it("shows an empty state when no quick replies match", async () => {
    vi.spyOn(quickRepliesApi, "listQuickReplies").mockResolvedValue([]);

    render(<QuickReplyPicker onInsert={() => undefined} />);
    await userEvent.click(screen.getByRole("button", { name: "Quick reply" }));

    expect(await screen.findByText("No quick replies found.")).toBeInTheDocument();
  });
});
