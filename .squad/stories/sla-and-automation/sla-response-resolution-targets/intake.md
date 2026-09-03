# Story intake

* Folder: `.squad/stories/sla-and-automation/sla-response-resolution-targets/intake.md`

## Feature

* **Feature name (display):** SLA & Automation
* **Feature slug (folder under `plans/`):** `sla-and-automation`

## Tracker (metadata only)

* **Tracker type:** `none`
* **Work item id:** ``
* **Work item type:** ``
* **Status:** ``
* **Assignee:** ``
* **Labels:** ``

---

## Title

```text
SLA Response & Resolution Targets
```

---

## Description

```text
Implement the SLA rules for Tickets to track First Response and Resolution targets.

When a Ticket is created, the applicable SLA must start from the Ticket creation time, regardless of whether the Ticket has been assigned to an Agent yet.

A newly created Ticket may have:
- Status = New
- Category = General Inquiry (default Category)
- Assigned Agent = NULL

The SLA must still be created/started for this Ticket while it is unassigned.

Example:

Customer Marwa creates Ticket #100 through WhatsApp at 10:00 AM.

Initial Ticket:
- Customer = Marwa
- Source = WhatsApp
- Status = New
- Category = General Inquiry
- Assigned Agent = NULL

If the applicable SLA policy is:
- First Response Target = 30 minutes
- Resolution Target = 4 hours

Then the system calculates:
- First Response Due = 10:30 AM
- Resolution Due = 02:00 PM

The SLA clock starts at 10:00 AM. It does NOT wait for Agent Assignment.

The Ticket may remain unassigned while it appears in the Admin Unassigned Tickets Queue.

Later, the Admin reviews the Ticket and changes the Category from General Inquiry to a real business Category such as Technical Issue. Story 23 then performs Automatic Ticket Assignment and may assign the Ticket to an eligible Agent such as جيديا.

The SLA must continue from the original Ticket creation time after assignment. Assignment does not reset the SLA timers.

The system must support tracking the First Response and Resolution SLA independently.

First Response SLA:
- Measures the time until the first valid Agent/Support response to the Customer.
- If a response is made before the target time, the SLA is considered met.
- If no valid response is made before the target time, the SLA is considered breached.

Resolution SLA:
- Measures the time until the Ticket is resolved according to the business Ticket resolution rules.
- If the Ticket is resolved before the target time, the SLA is considered met.
- If the Ticket is not resolved before the target time, the SLA is considered breached.

SLA status must be trackable for each target so the system can determine whether the target is running, met, or breached.

The SLA implementation must not require AssignedAgentId to be populated.

If a Ticket remains unassigned and its SLA target is reached, the SLA may still become breached. The system must not pause the SLA simply because no Agent has been assigned.

SLA breach/escalation behavior belongs to Story 24 — Escalation Rules.

Notifications related to SLA warnings or breaches belong to Story 25 — Alerts & Notifications.

Story 22 is responsible for SLA target calculation and SLA tracking only. It does not perform automatic Ticket assignment.
```

---

## Acceptance criteria

```text
- [ ] When a Ticket is created, the applicable SLA starts from the Ticket creation timestamp.

- [ ] SLA creation/tracking does not require the Ticket to have an AssignedAgentId.

- [ ] A Ticket with:
      Status = New
      Category = General Inquiry
      AssignedAgentId = NULL
  can have an active SLA.

- [ ] First Response SLA target is calculated from the Ticket SLA start time.

- [ ] Resolution SLA target is calculated from the Ticket SLA start time.

- [ ] Example:
      Ticket created at 10:00 AM
      First Response Target = 30 minutes
      Resolution Target = 4 hours

      Expected:
      First Response Due = 10:30 AM
      Resolution Due = 02:00 PM

- [ ] The SLA clock continues while the Ticket is unassigned.

- [ ] Assigning the Ticket to an Agent does not reset or restart the SLA timers.

- [ ] If a Ticket is assigned at 10:15 AM after being created at 10:00 AM, the remaining First Response SLA is still based on the original 10:00 AM SLA start time.

- [ ] If the Ticket remains unassigned until the First Response target is reached, the First Response SLA can become breached.

- [ ] If the Ticket remains unresolved until the Resolution target is reached, the Resolution SLA can become breached.

- [ ] A successful first response marks the First Response SLA as met when it occurs within the applicable target.

- [ ] Resolving the Ticket within the applicable target marks the Resolution SLA as met.

- [ ] First Response SLA and Resolution SLA are tracked independently.

- [ ] SLA status can represent at least:
      Running
      Met
      Breached

- [ ] SLA tracking continues independently of Story 23 Automatic Ticket Assignment.

- [ ] Story 22 does not select or assign an Agent.

- [ ] Story 22 does not implement Escalation Rules. SLA breach information must be available for Story 24 to apply escalation rules.

- [ ] Story 22 does not implement Notifications. SLA-related notifications are handled by Story 25.

- [ ] The default Category "General Inquiry" does not prevent SLA creation or tracking.

- [ ] Changing the Category from General Inquiry to a specific Category does not reset the existing SLA timers.

- [ ] The SLA behavior is based on the applicable SLA policy/target for the Ticket, including the applicable Priority/business rules.
```

---

## Attachments

| File (relative to this folder) | What it is     |
| ------------------------------ | -------------- |
| None                           | No attachments |

---

## Dependencies

* **Blocked by / related ids:** None

* **Depends on code areas or other stories:**

  * Ticket creation flow must provide the Ticket creation timestamp and applicable Ticket information.
  * Story 23 — Automatic Ticket Assignment is related but must not be a prerequisite for starting the SLA.
  * Story 24 — Escalation Rules consumes SLA state/target information to determine when escalation rules should execute.
  * Story 25 — Alerts & Notifications consumes SLA-related events/states for notifications.
  * Existing Ticket Status and Priority rules should be reused where applicable.

## Extra notes (optional)

* SLA is a Ticket-level concern and must not depend on Agent Assignment.
* `General Inquiry` is the default Category for a newly created Ticket. It represents the initial state before Admin review/classification; it does not delay SLA start.
* The Admin may review an unassigned Ticket later and change its Category. This Category change must not restart the SLA.
* Example business flow:

```text
Marwa
  ↓
Ticket Created
  ↓
Status = New
Category = General Inquiry
AssignedAgentId = NULL
  ↓
SLA Starts Immediately
  ↓
Unassigned Tickets Queue
  ↓
Admin Reviews Ticket
  ↓
Category = Technical Issue
  ↓
Story 23: Automatic Assignment
  ↓
جيديا assigned
  ↓
SLA continues from original creation time
  ↓
Response / Resolution
  ↓
SLA Met or Breached
  ↓
Story 24: Escalation
  ↓
Story 25: Notification
```

## Technical hints (optional)

* APIs, screens, services already discussed:

  * Ticket creation flow
  * Ticket SLA tracking
  * Existing Ticket Status/Priority handling
* Primary language: `C#`
* Repository root: `.`
* Prefer a simple domain/service implementation that keeps SLA calculation and state tracking separate from Agent Assignment and Escalation logic.
* Do not introduce AI classification or automatic Category detection as part of this story.
* Do not make Agent Assignment a prerequisite for SLA creation.

## Out of scope

* Automatic Ticket Assignment — Story 23
* Agent selection or workload calculation
* Category classification by AI or automatic message analysis
* Admin Ticket classification workflow itself, except where needed to ensure Category changes do not reset SLA
* Escalation Rules — Story 24
* Alerts & Notifications — Story 25
* Changing or redesigning the existing Ticket creation channels
* Customer-facing SLA configuration
* Implementing a separate Unassigned Tickets entity; the unassigned queue may be represented by the existing Ticket filtering/listing logic
