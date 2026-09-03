# Story intake

* Folder: `.squad/stories/sla-and-automation/automatic-ticket-assignment/intake.md`

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

```text id="k6v5q2"
Automatic Ticket Assignment
```

---

## Description

```text id="5y2s8r"
Implement automatic assignment of Tickets to eligible Agents after the Ticket has been reviewed and classified by an Admin.

When a Ticket is initially created from a customer channel such as WhatsApp, Email, Web Form, Live Chat, or another supported channel, the Ticket is created without an assigned Agent.

The initial Ticket state is:

- Status = New
- Category = General Inquiry (default Category)
- Assigned Agent = NULL

The Ticket appears in the Admin Unassigned Tickets Queue for review.

The Admin is responsible for reviewing the Ticket content and changing the default Category "General Inquiry" to the appropriate business Category.

The Admin does NOT select an Agent.

After the Admin saves a valid business Category, the system automatically determines the appropriate Agent and assigns the Ticket.

Example:

Customer Marwa sends a message through WhatsApp:

"عندي مشكلة في الخدمة ومحتاجة مساعدة."

The system creates:

Ticket #100
- Customer = Marwa
- Source = WhatsApp
- Status = New
- Category = General Inquiry
- Assigned Agent = NULL

The Ticket appears in the Unassigned Tickets Queue.

The Admin reviews Ticket #100 and determines that the correct Category is:

Technical Issue

The Admin changes:

Category:
General Inquiry → Technical Issue

and clicks Save.

After the Category is saved, Automatic Ticket Assignment is triggered.

The system determines the Department associated with the selected Category.

Example:

Technical Issue
→ Technical Support Department

The system then finds eligible Agents in that Department only.

Example Agents:

- الجوريزا
  - Department = Billing
  - Active Tickets = 4

- عزم
  - Department = Technical Support
  - Active Tickets = 6

- جيديا
  - Department = Technical Support
  - Active Tickets = 3

الجوريزا must not be considered because she belongs to Billing, while the Ticket requires Technical Support.

The eligible Agents are therefore:

- عزم = 6 active Tickets
- جيديا = 3 active Tickets

The system selects جيديا because she has the lowest active Ticket workload.

Ticket #100 is then assigned to جيديا.

Final state:

- Customer = Marwa
- Source = WhatsApp
- Category = Technical Issue
- Assigned Agent = جيديا
- Status = Open (according to the existing Ticket status rules)

The assignment decision must be performed by the system. The Admin only determines the Ticket Category.

Agent eligibility rules:

1. The Agent must belong to the Department associated with the Ticket Category.
2. The Agent must be active/enabled.
3. The Agent must be available for receiving Tickets.
4. The Agent must not have exceeded the configured maximum active Ticket capacity, if a capacity rule exists.
5. Among eligible Agents, select the Agent with the lowest number of active Tickets.
6. If multiple eligible Agents have the same workload, use Round Robin as the tie-breaker to distribute assignments fairly.

The system must not assign the Ticket to an Agent from another Department just because that Agent has a lower workload.

If no eligible Agent is available, the Ticket remains unassigned and continues to appear in the Unassigned Tickets Queue. The system must not assign it to an Agent from an unrelated Department as a fallback.

Automatic assignment must happen only after the Ticket has been reviewed/classified with a valid business Category. The default Category "General Inquiry" must not cause automatic assignment.

The Ticket SLA from Story 22 is independent of Agent Assignment. SLA timing starts when the Ticket is created and is not reset when an Agent is assigned.

Story 23 is responsible for Agent selection and assignment only. SLA calculation belongs to Story 22, escalation belongs to Story 24, and notifications belong to Story 25.
```

---

## Acceptance criteria

```text id="v9d3k1"
- [ ] A newly created Ticket has AssignedAgentId = NULL.

- [ ] A newly created Ticket has Status = New.

- [ ] A newly created Ticket has Category = General Inquiry as the default Category.

- [ ] A newly created Ticket with Category = General Inquiry must remain unassigned.

- [ ] A newly created Ticket appears in the Admin Unassigned Tickets Queue when it has no assigned Agent.

- [ ] Admin can review an unassigned Ticket and change its Category.

- [ ] Admin does not need to select an Agent when changing the Ticket Category.

- [ ] Automatic Assignment is triggered after the Admin saves a valid business Category while the Ticket has no assigned Agent.

- [ ] The system determines the Department associated with the selected Ticket Category.

- [ ] Only Agents belonging to the Category's associated Department are considered for assignment.

- [ ] Agents from other Departments are excluded even if they have a lower workload.

- [ ] Inactive/disabled Agents are excluded from automatic assignment.

- [ ] Agents who are not available to receive Tickets are excluded from automatic assignment.

- [ ] Agents who have reached their configured maximum active Ticket capacity are excluded when a capacity limit is configured.

- [ ] Among eligible Agents, the system selects the Agent with the lowest number of active Tickets.

- [ ] Example:
      عزم = Technical Support = 6 active Tickets
      جيديا = Technical Support = 3 active Tickets

      Technical Issue requires Technical Support.

      Expected result:
      Ticket is assigned to جيديا.

- [ ] An Agent from another Department is never selected as a fallback because of lower workload.

- [ ] If two or more eligible Agents have the same active Ticket count, Round Robin is used as the tie-breaker.

- [ ] Round Robin distributes assignments fairly among eligible Agents with equal workload.

- [ ] After successful automatic assignment, AssignedAgentId contains the selected Agent.

- [ ] Automatic Assignment does not require the Admin to manually choose an Agent.

- [ ] If no eligible Agent is available, AssignedAgentId remains NULL.

- [ ] If no eligible Agent is available, the Ticket remains visible in the Unassigned Tickets Queue.

- [ ] The system does not automatically assign an unassigned Ticket to an unrelated Department as a fallback.

- [ ] Changing the Category from General Inquiry to a valid business Category does not reset the Ticket SLA.

- [ ] Agent assignment does not start or restart the Ticket SLA. SLA timing is controlled by Story 22 from the original Ticket creation time.

- [ ] Automatic assignment is not triggered while the Ticket remains categorized as General Inquiry.

- [ ] Automatic assignment does not implement AI-based Category classification or message analysis.

- [ ] Automatic assignment does not implement SLA calculation.

- [ ] Automatic assignment does not implement escalation rules.

- [ ] Automatic assignment does not implement notification delivery.
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

  * Existing Ticket entity and Ticket creation flows.
  * Existing Agent entity and Agent Department/availability information.
  * Existing Category entity and Category-to-Department relationship/routing rule.
  * Existing Ticket Status and Priority rules.
  * Existing active Ticket counting/query logic, if available.
  * Admin Ticket review/update flow.
  * Story 22 — SLA Response & Resolution Targets: related; SLA starts independently from Ticket creation and must not depend on assignment.
  * Story 24 — Escalation Rules: related; escalation may act on Tickets regardless of when assignment occurs.
  * Story 25 — Alerts & Notifications: related; assignment events may produce Agent notifications.

## Extra notes (optional)

* `General Inquiry` is the system default Category for newly created Tickets. It is an initial/default state, not a Category that should trigger automatic Agent assignment.
* The Admin performs classification by changing `General Inquiry` to the appropriate business Category.
* The Admin does not select the Agent.
* Automatic Assignment begins only after a valid business Category has been selected and saved.
* The Unassigned Tickets Queue does not need to be a separate database entity. It can be an existing Ticket list/filter showing Tickets with no assigned Agent and requiring Admin review.
* Example end-to-end flow:

```text id="u7s2n4"
Marwa
  ↓
WhatsApp Message
  ↓
Ticket Created
  ↓
Status = New
Category = General Inquiry
AssignedAgentId = NULL
  ↓
Unassigned Tickets Queue
  ↓
Admin Reviews Ticket
  ↓
Category = Technical Issue
  ↓
Save
  ↓
Automatic Assignment
  ↓
Technical Issue → Technical Support
  ↓
Find eligible Technical Support Agents
  ↓
عزم = 6
جيديا = 3
  ↓
Select جيديا
  ↓
Assign Ticket to جيديا
  ↓
Ticket continues under its original SLA
```

* If eligible Agents are tied on workload:

```text id="x2r5m9"
عزم = 5
جيديا = 5
      ↓
Round Robin
      ↓
Select next eligible Agent
```

* If all eligible Agents are unavailable:

```text id="m6k1q8"
Technical Support
      ↓
No eligible Agent available
      ↓
AssignedAgentId = NULL
      ↓
Ticket remains in Unassigned Tickets Queue
```

## Technical hints (optional)

* APIs/screens/services involved:

  * Admin Unassigned Tickets Queue
  * Ticket Category update/save flow
  * Automatic Assignment service/domain logic
  * Agent availability/workload lookup
* Primary language: `C#`
* Repository root: `.`
* Reuse existing Ticket, Agent, Department, Category, Status, and Priority models/services where possible.
* Keep the assignment algorithm simple and deterministic.
* The Category-to-Department relationship must be used to determine which Agents are eligible.
* Do not introduce AI/ML or keyword-based classification.
* Assignment should be performed server-side; the client should only submit the Ticket update/Category change.
* Avoid duplicating assignment logic in individual channel implementations. WhatsApp, Email, Web Form, Live Chat, and other channels should use the same Automatic Assignment mechanism after Ticket classification.
* Live Chat and other channel-specific flows must not implement a separate assignment algorithm.

## Out of scope

* SLA target calculation and SLA timers — Story 22
* SLA breach/escalation rules — Story 24
* Alerts and Notifications delivery — Story 25
* AI/ML-based Ticket classification
* Automatic Category detection from customer messages
* Customer selecting the Agent
* Admin manually selecting the Agent as part of this automatic assignment flow
* Automatic assignment across unrelated Departments
* Redesigning the Ticket creation flow for WhatsApp, Email, Web Form, Live Chat, or other channels
* Creating a separate Unassigned Tickets database entity
* Changing the existing Agent Department/Branch model
