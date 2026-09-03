# Story intake

## Feature

* **Feature name (display):** SLA & Automation
* **Feature slug (folder under `plans/`):** `sla-and-automation`

## Tracker (metadata only)

* **Tracker type:** `none`
* **Work item id:** ``
* **Work item type:** `Story`
* **Status:** ``
* **Assignee:** ``
* **Labels:** `sla, automation, notifications`

---

## Title

```text
Alerts & Notifications
```

---

## Description

```text
Implement alerts and notifications related to SLA and ticket automation.

The system should notify the appropriate users when important SLA and automation events occur.

Notifications are triggered by the business events produced by the SLA and escalation flows. This story is responsible for notification delivery and recipient handling; it does not calculate SLA deadlines or decide escalation rules.

The main notification scenarios are:

1. Ticket Assignment
- When a ticket is automatically assigned to an Agent through Story 23, the assigned Agent should receive a notification.
- The notification should identify the ticket and provide enough information for the Agent to understand that a new ticket has been assigned.
- Assignment notification must not be sent if no Agent was selected.

2. SLA Warning
- When an SLA reaches its warning threshold through Story 24, the appropriate internal user should receive a notification.
- For an assigned ticket, the warning notification can be sent to the assigned Agent.
- For an unassigned ticket, the warning notification should be directed to the Administrator responsible for the Unassigned Tickets Queue.
- First Response and Resolution warnings are separate events.

3. SLA Breach / Escalation
- When an SLA is breached and Story 24 triggers an escalation:
  - Assigned ticket → notify the responsible Manager.
  - Unassigned ticket → notify the Administrator responsible for the Unassigned Tickets Queue.
- First Response and Resolution breaches are separate events.

4. Customer Notifications
- Customers may receive customer-facing notifications for relevant ticket events.
- Customer notifications must not expose internal escalation details, internal workload information, or internal user/role information.
- Customer notification behavior should follow the existing supported notification channels and ticket communication rules.

Notifications should be triggered by business events rather than by duplicating SLA or assignment logic inside the notification system.

Example:

Customer Marwa creates Ticket #100 through WhatsApp.

Initial state:
- Status = New
- Category = General Inquiry
- AssignedAgent = NULL

Admin changes Category to Technical Issue.

Story 23 assigns the ticket to جيديا.

Story 25 sends an assignment notification to جيديا.

If the First Response SLA reaches its warning threshold:
- The assigned Agent can receive the warning notification.

If the First Response SLA is breached:
- The responsible Manager receives the escalation notification.

If the ticket is still unassigned when the SLA is breached:
- The responsible Administrator receives the escalation notification.

Notifications must not change the ticket SLA, assignment, category, priority, or status by themselves.
```

---

## Acceptance criteria

```text
- [ ] When a ticket is automatically assigned to an Agent, an assignment notification is created/sent for the assigned Agent.

- [ ] No assignment notification is sent when the ticket remains unassigned.

- [ ] SLA warning notifications are triggered when Story 24 produces an SLA warning event.

- [ ] First Response SLA warning and Resolution SLA warning are treated as separate notification events.

- [ ] For an assigned ticket, the SLA warning notification can be sent to the assigned Agent.

- [ ] For an unassigned ticket, the SLA warning notification is directed to the Administrator responsible for the Unassigned Tickets Queue.

- [ ] SLA breach/escalation notifications are triggered when Story 24 produces an escalation event.

- [ ] For an assigned ticket, SLA breach/escalation notification is sent to the responsible Manager for the Agent/Department.

- [ ] For an unassigned ticket, SLA breach/escalation notification is sent to the Administrator responsible for the Unassigned Tickets Queue.

- [ ] Customer notifications, when applicable, are separated from internal notifications.

- [ ] Customer notifications must not expose internal escalation details, internal workload information, or internal organizational information.

- [ ] Notification recipients are determined from the existing system roles and relationships:
      - Agent
      - Manager
      - Administrator
      - Customer

- [ ] The system must not introduce a new Supervisor or Team Lead role for notifications.

- [ ] Notification processing must be idempotent; retrying the same business event must not create duplicate notifications for the same recipient/event.

- [ ] Notification failure must not change or rollback the underlying ticket business operation.

- [ ] Notifications must not modify:
      - Ticket SLA
      - Ticket assignment
      - Ticket category
      - Ticket priority
      - Ticket status

- [ ] Notification logic must not recalculate SLA deadlines.

- [ ] Notification logic must not implement ticket assignment rules.

- [ ] Notification logic must not implement escalation threshold rules.

- [ ] The notification mechanism should be reusable for tickets regardless of the ticket source/channel, including WhatsApp, Email, Web Form, and Live Chat.
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| None                           |            |

---

## Dependencies

* **Blocked by / related ids:** None

* **Depends on code areas or other stories:**

  * Story 22 — SLA Response & Resolution Targets
  * Story 23 — Automatic Ticket Assignment
  * Story 24 — Escalation Rules
  * Existing Ticket, Agent, Manager, Administrator, Customer, Department, and Branch relationships
  * Existing authentication/authorization and notification infrastructure, if available

---

## Extra notes (optional)

* Assignment, SLA calculation, and escalation decisions belong to their respective stories.
* This story consumes the resulting business events and handles notification delivery.
* Use the existing `Agent`, `Manager`, `Administrator`, and `Customer` roles.
* Do not introduce `Supervisor` or `Team Lead` roles.
* The exact notification channels should reuse the channels already supported by the system rather than introducing a new channel without a business requirement.

---

## Technical hints (optional)

* APIs, screens, services already discussed. Repos/roots: `.`. Primary language: `C#`.
* Prefer a simple notification service/event-based flow that can consume:

  * TicketAssigned
  * SLABreachWarning
  * SLABreached / EscalationTriggered
* Reuse existing authentication/user/role relationships.
* Notification creation and delivery should be separated from the business operation when appropriate so notification failures do not break ticket assignment or SLA processing.
* Background processing may be used for delivery/retry if the existing application already uses Hangfire or a similar mechanism.
* Keep notification handling channel-agnostic where possible so the same business event can be delivered through the application's supported channels.

---

## Out of scope

* SLA target calculation or SLA deadline management
* Starting, stopping, or restarting SLA timers
* SLA warning/breach threshold calculation
* Ticket automatic assignment algorithm
* Category-to-Department mapping
* Agent workload calculation
* Round Robin assignment
* Escalation decision/routing rules
* Creating a Supervisor or Team Lead role
* Changing ticket status as part of notification processing
* Changing ticket assignment as part of notification processing
* AI-based notification or ticket classification
* Introducing new notification channels unless explicitly required by the existing product requirements
