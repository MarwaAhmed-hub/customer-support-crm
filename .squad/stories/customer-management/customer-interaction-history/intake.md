# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

* Folder: `.squad/stories/customer-management/customer-interaction-history/intake.md`

* Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.

* Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

* **Feature name (display):** Customer Management

* **Feature slug (folder under `plans/`):** `customer-management`

## Tracker (metadata only)

* **Tracker type:** `none`

* **Work item id:** ``

* **Work item type:** ``

* **Status:** ``

* **Assignee:** ``

* **Labels:** ``

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

```text
Customer Interaction History
```

---

## Description

```text
The Customer Management feature must provide authorized CRM users with access to a history of interactions associated with each customer.

This story covers the interaction history portion of Customer Management.

The system should allow authorized users to view a customer's previous interactions in a clear chronological history so that support users can understand the customer's previous communication and support activity.

Each interaction should provide the relevant available information needed to understand what happened, when it happened, and the interaction context.

The interaction history should be accessible from the customer profile/details area and should follow the project's existing authentication, authorization, API, database, and frontend patterns.

This story is part of the Customer Management feature, which includes:
- Customer profiles
- Contact details
- Interaction history
- Notes and attachments

Only Interaction History is in scope for this story. Customer Profiles & Contact Details belong to Story 07, and Notes & Attachments belong to Story 09.
```

---

## Acceptance criteria

```text
- [ ] Authorized users can view the interaction history for a specific customer.
- [ ] Interactions are displayed in chronological order, with the newest interaction available first unless the existing project convention requires otherwise.
- [ ] Each interaction displays its relevant date/time.
- [ ] Each interaction identifies the available interaction type or channel when applicable.
- [ ] Each interaction provides the available summary/details needed to understand the interaction.
- [ ] Where applicable, the interaction identifies the CRM user/agent associated with the interaction.
- [ ] The interaction history is associated with the correct customer profile.
- [ ] A customer with no interactions receives an appropriate empty-state response in the UI.
- [ ] Interaction history is read-only within this story unless the existing requirements/codebase clearly require interaction creation or editing.
- [ ] Customer notes and attachments are not treated as interaction-history records.
- [ ] Ticket history is not duplicated or reimplemented as part of this story; integration with existing ticket data should only be considered where supported by the existing architecture.
- [ ] Interaction history APIs require authentication and enforce the appropriate permissions.
- [ ] Users without the required permissions cannot access protected interaction-history data.
- [ ] The frontend provides an appropriate interaction-history view from the customer profile/details area.
- [ ] Loading, empty, validation, and API error states follow the existing frontend conventions.
- [ ] Existing customer-management and previously implemented features continue to work without regression.
```

---

## Attachments

Place files in `attachments/` next to this `intake.md`, then list them here so the planner knows what to open.

| File (relative to this folder) | What it is                |
| ------------------------------ | ------------------------- |
| None                           | No additional attachments |

---

## Dependencies

* **Blocked by / related ids:** None

* **Depends on code areas or other stories:**

  * Story 07 — Customer Profiles & Contact Details
  * Existing customer entity/profile and contact-detail implementation
  * Existing authentication and authorization system
  * Existing backend API and EF Core persistence patterns
  * Existing frontend customer/profile UI patterns

## Extra notes (optional)

* This is Story 08 within the Customer Management feature.
* The interaction history should be associated with the customer created and managed by Story 07.
* Keep the implementation simple and consistent with the existing CRM architecture.
* The planner should inspect the existing repository and Story 07 implementation before deciding the exact interaction entity structure, interaction types, API routes, permissions, and UI design.
* Do not invent communication channels or interaction sources that are not supported by the existing project or requirements.
* If existing ticket, email, chat, SMS, or WhatsApp records already exist in the repository and are available for integration, the planner should assess whether they can be surfaced as interaction-history entries rather than duplicating data.
* Do not implement or introduce new communication-channel functionality as part of this story.
* Do not implement functionality belonging to Story 09.

## Technical hints (optional)

* APIs, screens, services already discussed. Repos/roots: `.`. Primary language: `C#`.
* Backend: ASP.NET Core Web API, EF Core, SQL Server.
* Frontend: React + TypeScript.
* Follow existing authentication, authorization, controller/service/DTO, persistence, routing, and UI conventions.
* Reuse existing customer/profile components and API patterns where appropriate.
* The planner should inspect the repository before deciding implementation details.

## Out of scope

* Creating or editing customer profiles — Story 07.
* Managing customer contact details — Story 07.
* Customer notes — Story 09.
* Customer attachments/files — Story 09.
* Ticket creation and tracking.
* Ticket assignment, status, escalation, and ticket-history implementation.
* Email, WhatsApp, SMS, live-chat, or web-form channel implementation.
* Customer Portal interaction-history features.
* AI-generated summaries or suggested replies.
* Reports and analytics based on interaction history.
* Unspecified interaction fields or business rules not supported by the project requirements or existing codebase.
