export type SimulatorChannel = "email" | "webform" | "whatsapp" | "sms" | "livechat";

export interface ChannelDefinition {
  value: SimulatorChannel;
  label: string;
  /** False for a channel with no backend implementation yet (Live Chat: Story 21) — the tab renders a placeholder instead of a form. */
  implemented: boolean;
  /** Shown in the placeholder for a not-yet-implemented channel. */
  ownerNote?: string;
}

export const SIMULATOR_CHANNELS: ChannelDefinition[] = [
  { value: "email", label: "Email", implemented: true },
  { value: "webform", label: "Web Form", implemented: true },
  { value: "whatsapp", label: "WhatsApp", implemented: true },
  { value: "sms", label: "SMS", implemented: true },
  { value: "livechat", label: "Live Chat", implemented: false, ownerNote: "Story 21 — not implemented yet." },
];

/** What every channel's simulated submission reports back, once it produces a ticket. */
export interface SimulationOutcome {
  ticketId: string;
  customerId: string;
  alreadyProcessed?: boolean;
}
