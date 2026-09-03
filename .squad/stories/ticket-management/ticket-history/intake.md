# Story intake

## Feature

* **Feature name (display):** Ticket Management
* **Feature slug (folder under** **`plans/`****):** `ticket-management`

## Tracker (metadata only)

* **Tracker type:** `none`
* **Work item id:**
* **Work item type:**
* **Status:**
* **Assignee:**
* **Labels:**

External tracker links are **not** followed by the planner. Keep the id for naming and traceability only.

---

## Title

```text
Ticket History
```

---

## Description

````text
Implement Ticket History for the Customer Support CRM.

Authorized CRM users must be able to view the chronological history of a ticket and understand what happened to that ticket over time.

Ticket History is a dedicated business history of changes and activities related to a specific Ticket.

It must remain completely separate from:

- AuditLog, which records system/user actions for auditing purposes.
- CustomerInteraction, which represents customer-facing activity shown in the customer's Interaction History.
- The Ticket current state/status, which represents the ticket's current state.

The implementation must follow the existing project conventions established by Stories 01–14:

- ASP.NET Core Web API
- EF Core
- SQL Server
- Controller → Service → DTO layering
- JWT authentication
- Permission-based authorization using [HasPermission]
- Audit logging for mutations
- React + TypeScript frontend
- Existing http client and API conventions
- Existing loading / empty / error / success UI patterns
- Existing permission-check patterns
- Existing customer and ticket feature structures

### Story numbering and ownership

The Ticket Management feature consists of:

- Story 10 — Ticket Categories & Priorities
- Story 11 — Ticket Creation & Tracking
- Story 12 — Ticket Assignment
- Story 13 — Ticket Status & Escalation
- Story 14 — Ticket History

Therefore, this intake represents:

- Story 14 — Ticket History

The planner MUST use the above story numbering when referring to dependencies or existing implementations.

### Ticket History purpose

Ticket History should provide a chronological, read-only timeline of important changes and activities related to a ticket.

Examples of history entries include:

- Ticket created
- Ticket updated
- Ticket assigned or reassigned
- Ticket status changed
- Ticket priority changed
- Ticket category changed
- Other meaningful ticket changes supported by the actual Ticket model and Stories 10–13

The history should identify:

- What happened
- When it happened
- Who performed the action, when available
- Relevant previous/new values when applicable

### History source

Ticket History must use a dedicated ticket-history concept/model if persisted history is required by the existing architecture.

Do NOT simply expose the global AuditLog table as Ticket History.

Do NOT use CustomerInteraction as Ticket History.

The planner MUST inspect:

- The existing AuditLog implementation from Story 05
- The actual Ticket entity
- Ticket DTOs
- Ticket service
- Ticket creation flow from Story 11
- Ticket assignment flow from Story 12
- Ticket status/escalation flow from Story 13
- Existing EF Core conventions
- Existing transaction/unit-of-work conventions

before finalizing the exact persistence and service approach.

If the existing project already has a suitable reusable history mechanism, reuse it instead of introducing unnecessary duplicate infrastructure.

### Automatic history recording

Important ticket mutations implemented by Story 11, Story 12, Story 13, or this story should result in appropriate Ticket History entries.

At minimum, the implementation must support history for:

- Ticket creation
- Ticket updates
- Ticket status changes
- Ticket assignment/reassignment
- Meaningful category changes
- Meaningful priority changes

Where a mutation is already implemented in Stories 11–13, Story 14 MUST integrate with the existing mutation flow rather than duplicating or reimplementing the operation.

Story 14 is responsible for adding the Ticket History side effect to those existing ticket lifecycle operations where required.

History entries must be created automatically as a side effect of the corresponding successful ticket mutation.

The user must NOT manually create a history entry.

If the related ticket mutation fails and is not persisted, its corresponding Ticket History entry must not be persisted.

Ticket mutation and history creation should follow the existing EF Core transaction/unit-of-work conventions.

### Critical ownership rule for CustomerInteraction

Story 14 MUST NOT create or recreate CustomerInteraction records.

The existing Ticket → CustomerInteraction integration belongs to:

- Story 08 — Customer Interaction History, which defines the CustomerInteraction model and customer interaction semantics.
- Story 11 — Ticket Creation & Tracking, which defines that successful Ticket creation automatically creates exactly ONE CustomerInteraction with Type = "ticket".

Story 14 must preserve this behavior.

When a Ticket is created, the overall system may produce:

1. Ticket record
2. Exactly one CustomerInteraction with Type = "ticket", created according to Story 11
3. One Ticket History entry for "Ticket Created", created according to Story 14
4. An AuditLog entry according to the existing audit conventions

These are four separate records/concepts with different responsibilities.

Story 14 MUST NOT:

- Create a second CustomerInteraction for Ticket creation.
- Move CustomerInteraction creation from Story 11 into Story 14.
- Replace CustomerInteraction with TicketHistory.
- Use CustomerInteraction as TicketHistory.
- Modify the CustomerInteraction model established by Story 08.
- Redefine Story 11's Ticket → CustomerInteraction behavior.

The existing Story 11 CustomerInteraction behavior remains the single source of truth for the Ticket-created customer interaction.

### Ticket History vs Audit Log vs Customer Interaction

These concepts must remain separate.

1. AuditLog
   - Technical/system auditing
   - Records user/system actions
   - Used for administrative auditing

2. Ticket History
   - Business history of a specific ticket
   - Shows the chronological evolution of the ticket
   - Used by CRM users to understand what happened to the ticket

3. CustomerInteraction
   - Customer activity
   - Shown in Customer Interaction History
   - Uses the existing model established by Story 08
   - Ticket creation creates exactly one CustomerInteraction with Type = "ticket" according to Story 11

The same business operation may therefore produce separate records in these different systems.

Example:

Create Ticket
    |
    +-- Ticket
    |
    +-- CustomerInteraction
    |      +-- Type = "ticket"
    |      +-- Created by Story 11 integration
    |      +-- Shown in Customer Interaction History
    |
    +-- TicketHistory
    |      +-- "Ticket Created"
    |      +-- Shown in Ticket History
    |
    +-- AuditLog
           +-- System/User audit record

These records must NOT be treated as duplicates.

### Important distinction

```text
CustomerInteraction
    → What happened with the customer?

TicketHistory
    → What happened to this specific ticket?

AuditLog
    → What did the user/system do?

Ticket current status
    → What is the ticket's current state now?
````

### Ticket History retrieval

Authorized users can retrieve the history of a specific ticket.

The history should:

* Be ordered chronologically according to the project's agreed convention.
* Clearly show the event type/action.
* Show timestamp.
* Show actor/user when available.
* Show meaningful details for changes.
* Return an empty state when a valid ticket has no history entries.
* Return 404 when the ticket does not exist.

History retrieval must respect ticket permissions and existing authorization conventions.

### Frontend

The Ticket Details page should contain a Ticket History section or tab.

The UI should display the history as a readable chronological timeline or equivalent list.

Each entry should show, where available:

* Date/time
* Action/event
* User/actor
* Relevant details such as old value → new value

The UI must follow existing CRM patterns for:

* Loading
* Empty
* Error
* Permission denied
* Date/time formatting

The history is read-only from the UI.

There must be no generic "Add History Entry" form.

### No manual history CRUD

Story 14 must NOT add generic UI functionality for manually creating, editing, or deleting Ticket History records.

Ticket History is generated automatically by ticket activities.

Users only view the history.

### Relationship with Customer Interaction History

Ticket History and Customer Interaction History are different features.

For example:

Creating a Ticket:

* Story 11 creates the Ticket according to the Ticket Creation & Tracking flow.
* Story 11 creates exactly one CustomerInteraction with Type = "ticket".
* Story 14 creates one Ticket History entry such as "Ticket Created".
* The existing audit conventions create an AuditLog entry.

Story 14 must NOT create the CustomerInteraction.

Changing a ticket status:

* Story 13 updates the Ticket current status.
* Story 14 creates a Ticket History entry describing the status change.
* The existing audit conventions may create an AuditLog entry.
* No CustomerInteraction is created merely because a Ticket History entry was created.

The planner must preserve the existing Story 08 CustomerInteraction model/semantics and Story 11 Ticket → CustomerInteraction integration.

````

---

## Acceptance criteria

```text
### Ticket History retrieval

- [ ] Authenticated users with the required ticket view permission can retrieve history for a ticket.
- [ ] Ticket history is associated with the correct TicketId.
- [ ] History entries are returned in the agreed chronological order.
- [ ] Each history entry includes an event/action type.
- [ ] Each history entry includes a timestamp.
- [ ] Each history entry identifies the actor/user when available.
- [ ] Relevant change details are included when applicable.
- [ ] A non-existent TicketId returns 404.
- [ ] A valid ticket with no history entries returns an appropriate empty result.
- [ ] Ticket history retrieval respects existing permission rules.

### Automatic history creation

- [ ] Successful Ticket creation creates an appropriate Ticket History entry.
- [ ] Successful Ticket updates create appropriate Ticket History entries.
- [ ] Successful Ticket status changes create a Ticket History entry.
- [ ] Successful Ticket assignment/reassignment creates a Ticket History entry.
- [ ] Successful meaningful Category/Priority changes create appropriate Ticket History entries.
- [ ] History entries are created automatically without manual user input.
- [ ] Failed ticket mutations do not leave orphan Ticket History entries.
- [ ] Ticket mutation and history creation follow existing EF Core transaction/unit-of-work conventions.
- [ ] Duplicate Ticket History entries are not created for a single mutation.

### History details

- [ ] Ticket creation history identifies the ticket creation event.
- [ ] Update history identifies what meaningful field/change occurred.
- [ ] Status history includes previous and new status when applicable.
- [ ] Assignment history includes previous and new assignee when applicable.
- [ ] Category/Priority history includes previous and new values when applicable.
- [ ] History timestamps are stored using the project's UTC convention.
- [ ] Actor information is taken from the authenticated user where supported.

### Audit Log vs Ticket History

- [ ] Ticket History is not implemented by directly exposing the AuditLog table.
- [ ] AuditLog remains responsible for system/user auditing.
- [ ] Ticket History remains responsible for business history of a specific ticket.
- [ ] CustomerInteraction remains separate from Ticket History.
- [ ] Creating a ticket can produce both AuditLog and Ticket History records without treating them as duplicates.
- [ ] Story 11's CustomerInteraction behavior remains unchanged.
- [ ] No CustomerInteraction is created merely because a Ticket History entry was created.
- [ ] Story 14 does not create a second CustomerInteraction for Ticket creation.
- [ ] Story 14 does not modify or replace the existing CustomerInteraction model from Story 08.

### Persistence

- [ ] Ticket History is persisted using the project's agreed EF Core approach.
- [ ] History records reference the correct Ticket.
- [ ] Foreign keys are configured appropriately.
- [ ] Required fields have appropriate SQL constraints and maximum lengths.
- [ ] Appropriate indexes are added for common ticket-history lookups.
- [ ] EF migration is generated if schema changes are required.
- [ ] Migration applies cleanly.
- [ ] Migration does not modify unrelated tables or data.

### API

- [ ] An authorized endpoint exists to retrieve the history of a ticket.
- [ ] The exact route follows existing controller/routing conventions.
- [ ] Authentication is required.
- [ ] Appropriate ticket-view permission is required.
- [ ] Non-existent tickets return 404.
- [ ] Validation and error responses follow existing API conventions.
- [ ] API response uses dedicated DTOs appropriate for Ticket History.

### Frontend

- [ ] Authorized users can view Ticket History from the Ticket Details page.
- [ ] Ticket History is displayed in a clear chronological timeline/list.
- [ ] Date/time is displayed consistently with existing CRM conventions.
- [ ] Actor/user information is displayed when available.
- [ ] Meaningful change details are displayed.
- [ ] Loading state is displayed according to existing conventions.
- [ ] Empty history state is handled clearly.
- [ ] API errors are displayed according to existing conventions.
- [ ] Users without the required permission cannot access the history data.
- [ ] The UI does not provide manual create/edit/delete controls for history entries.
- [ ] No manual "Log History" action is required from the user.

### Verification

- [ ] Backend production code builds successfully.
- [ ] EF migration generates successfully if schema changes are required.
- [ ] EF migration applies successfully.
- [ ] Ticket History retrieval API smoke test succeeds for an existing ticket.
- [ ] Ticket History returns 404 for a non-existent ticket.
- [ ] Authentication and permission smoke checks confirm 401/403 behavior.
- [ ] Ticket creation produces the expected Ticket History entry.
- [ ] Ticket creation continues to produce exactly one CustomerInteraction with Type = "ticket" according to Story 11.
- [ ] Ticket update produces the expected Ticket History entry.
- [ ] Ticket status change produces the expected Ticket History entry.
- [ ] Ticket assignment change produces the expected Ticket History entry.
- [ ] Relevant Category/Priority changes produce the expected Ticket History entries.
- [ ] No duplicate Ticket History entry is created for a single mutation.
- [ ] No duplicate CustomerInteraction is created by Story 14.
- [ ] Ticket History is separate from AuditLog.
- [ ] Customer Interaction History remains separate from Ticket History.
- [ ] Existing Story 11 Ticket → CustomerInteraction integration remains functional.
- [ ] Frontend build succeeds.
- [ ] Manual end-to-end Ticket History flow succeeds.
- [ ] Manual regression checks confirm existing login, users, roles, departments, branches, customers, interaction history, notes, attachments, ticket categories, ticket priorities, ticket creation, ticket assignment, and ticket status functionality remain functional.
- [ ] No unit-test creation or test-suite execution is required for this story.
````

---

## Attachments

| File (relative to this folder) | What it is                      |
| ------------------------------ | ------------------------------- |
| None                           | No binary attachments required. |

---

## Dependencies

* **Blocked by / related ids:** None.

* **Depends on code areas or other stories:**

  * Story 01 — Authentication & Login
  * Story 03 — Roles & Permissions
  * Story 05 — Audit Logs
  * Story 07 — Customer Profiles & Contact Details
  * Story 08 — Customer Interaction History
  * Story 10 — Ticket Categories & Priorities
  * Story 11 — Ticket Creation & Tracking
  * Story 12 — Ticket Assignment
  * Story 13 — Ticket Status & Escalation
  * Existing EF Core / SQL Server infrastructure
  * Existing React ticket feature structure
  * Existing permission and audit patterns

Story 14 depends primarily on Stories 11–13 because Ticket History must reflect the ticket lifecycle and mutations implemented by those stories.

Story 08 remains a dependency only to preserve the existing CustomerInteraction model and Customer Interaction History semantics.

## Story 10 is relevant because Ticket History may need to record meaningful category/priority changes, but Story 14 does not implement Category/Priority management itself.

## Extra notes

* Keep implementation simple and consistent with the existing CRM architecture.
* Do not introduce CQRS, repositories, message brokers, microservices, or other new architecture unless the existing project already uses them.
* Do not create unit tests.
* Verification must use build checks, migration checks, API smoke tests, frontend build, manual end-to-end testing, and manual regression checks.
* Ticket History is read-only from the UI.
* Do not seed fake Ticket History merely to demonstrate the feature.
* Creating or modifying a real ticket through the application must be sufficient to generate the corresponding history entries.
* Do not add generic manual Ticket History CRUD.
* Do not expose AuditLog as a substitute for Ticket History.
* Do not use CustomerInteraction as a substitute for Ticket History.
* Preserve the existing Story 08 Customer Interaction History semantics.
* Preserve Story 11's automatic Ticket → CustomerInteraction integration.
* Story 14 must not create CustomerInteraction records.
* History creation should be an automatic application/domain side effect of successful ticket mutations.
* Where Stories 11–13 already implement a mutation, Story 14 must integrate with that existing flow instead of duplicating the mutation logic.
* Do not move ticket creation, assignment, status, or CustomerInteraction responsibilities into Story 14.

### Critical business rule

The Ticket Management story ownership is:

```text
Story 10 — Ticket Categories & Priorities
Story 11 — Ticket Creation & Tracking
Story 12 — Ticket Assignment
Story 13 — Ticket Status & Escalation
Story 14 — Ticket History
```

The responsibilities during the ticket lifecycle are:

```text
Ticket Creation
     │
     ├── Story 11
     │      ├── Save Ticket
     │      ├── Create exactly one CustomerInteraction
     │      │      └── Type = "ticket"
     │      └── Existing AuditLog behavior
     │
     └── Story 14 integration
            └── Create one TicketHistory entry
                   └── "Ticket Created"
```

```text
Ticket Update
     │
     ├── Existing Story 11 ticket update flow
     │
     ├── Create TicketHistory
     │      └── Describe meaningful changes
     │
     └── Existing AuditLog behavior
```

```text
Ticket Assignment / Reassignment
     │
     ├── Story 12
     │      └── Save assignment
     │
     ├── Story 14
     │      └── Create TicketHistory
     │
     └── Existing AuditLog behavior
```

```text
Ticket Status Change
     │
     ├── Story 13
     │      └── Save new status
     │
     ├── Story 14
     │      └── Create TicketHistory
     │             └── Previous Status → New Status
     │
     └── Existing AuditLog behavior
```

The complete conceptual relationship is:

```text
                    TICKET LIFECYCLE
                           │
          ┌────────────────┼────────────────┐
          │                │                │
          ▼                ▼                ▼
   Customer Activity   Ticket Lifecycle   System Audit
          │                │                │
          ▼                ▼                ▼
CustomerInteraction   TicketHistory      AuditLog
          │                │                │
          │                │                │
    Story 08/11         Story 14        Story 05/
    ownership           ownership       existing flows
```

For Ticket creation specifically:

```text
Create Ticket
     │
     ├── Ticket
     │
     ├── CustomerInteraction
     │      └── Type = "ticket"
     │             └── CREATED BY STORY 11
     │
     ├── TicketHistory
     │      └── "Ticket Created"
     │             └── CREATED BY STORY 14
     │
     └── AuditLog
            └── CREATED BY EXISTING AUDIT CONVENTION
```

Therefore:

```text
CustomerInteraction
    → What happened with the customer?

TicketHistory
    → What happened to this specific ticket?

AuditLog
    → What did the user/system do?

Ticket.Status
    → What is the current state of the ticket?
```

These four concepts must remain separate.

### CustomerInteraction ownership rule

This is a critical rule for Story 14:

```text
Story 08
    ↓
Defines CustomerInteraction and Customer Interaction History

Story 11
    ↓
Creates exactly one CustomerInteraction for successful Ticket creation
Type = "ticket"

Story 14
    ↓
MUST NOT create CustomerInteraction
MUST NOT duplicate CustomerInteraction
MUST NOT replace CustomerInteraction
MUST NOT redefine CustomerInteraction
MUST ONLY create TicketHistory for the ticket lifecycle
```

If Story 14 needs to modify the Story 11 ticket-creation flow to add TicketHistory, that modification must be an integration change only.

The planner must not move the existing CustomerInteraction creation from Story 11 into Story 14.

Future communication features such as:

* Email
* WhatsApp
* SMS
* Calls
* Meetings

may create CustomerInteraction entries when their corresponding business activities are implemented.

Those integrations are outside Story 14 unless explicitly required by a future story.

---

## Technical hints

* Backend root: `backend/`
* API project: `backend/src/CustomerSupportCrm.Api/`
* Domain project: `backend/src/CustomerSupportCrm.Domain/`
* Infrastructure project: `backend/src/CustomerSupportCrm.Infrastructure/`
* Frontend root: `frontend/`
* Primary language: `C#`
* Frontend: React + TypeScript
* Database: SQL Server
* ORM: EF Core
* Authentication: JWT
* Authorization: `[HasPermission("...")]`
* Audit: existing `IAuditLogService` / `AuditLogService`
* Ticket: Story 11 implementation
* Ticket Assignment: Story 12 implementation
* Ticket Status: Story 13 implementation
* Ticket History: Story 14 implementation
* CustomerInteraction: Story 08 model / Story 11 integration

The planner MUST inspect the actual:

* Ticket entity
* Ticket DTOs
* Ticket service
* Ticket creation implementation
* Ticket update implementation
* Assignment implementation
* Status implementation
* Category/Priority fields
* AuditLog implementation
* CustomerInteraction implementation
* Existing EF Core conventions
* Existing transaction/unit-of-work conventions
* Existing frontend Ticket Details implementation

before finalizing the Ticket History design.

The planner should reuse existing patterns wherever possible and avoid duplicating existing audit/history infrastructure.

### Potential API structure

```text
GET /api/tickets/{id}/history
```

The planner must inspect existing controller routing conventions before finalizing the exact route.

### Potential history fields

```text
TicketHistory
    Id
    TicketId
    Action / EventType
    Description / Summary
    PreviousValue (nullable)
    NewValue (nullable)
    PerformedByUserId (nullable)
    CreatedAt
```

These are suggestions only.

The planner must inspect the actual project conventions and existing entities before finalizing the schema.

### Potential history events

```text
Created
Updated
Assigned
Reassigned
StatusChanged
PriorityChanged
CategoryChanged
```

The planner should only implement events supported by the actual Ticket model and Stories 10–13.

---

## Out of scope

* Ticket Categories & Priorities management — Story 10
* Ticket Creation & Tracking implementation — Story 11
* Ticket Assignment implementation — Story 12
* Ticket Status & Escalation implementation — Story 13
* SLA configuration and automation
* Automatic ticket assignment rules
* Notifications
* Email integration
* SMS integration
* WhatsApp integration
* Live Chat
* Customer Portal
* AI features
* Reports
* Knowledge Base
* Customer Interaction History redesign
* Changes to Story 08 CustomerInteraction semantics
* Generic CustomerInteraction CRUD
* Generic manual Ticket History CRUD
* Manual "Log History" feature
* Replacing AuditLog with Ticket History
* Replacing CustomerInteraction with Ticket History
* Moving Story 11 CustomerInteraction creation into Story 14
* Creating duplicate CustomerInteraction records from Story 14
* Customer Notes & Attachments
* Unit tests
* Frontend component tests
* Backend integration tests
* Regression test-suite execution
* New communication-channel integrations
* Advanced reporting or analytics for ticket history

```

**أهم تعديلين هنا:**  
1. كل الترقيم أصبح مطابقًا للـ Cycle 2: **10 Categories → 11 Creation → 12 Assignment → 13 Status → 14 History**.  
2. فصلت ownership بتاع `CustomerInteraction` بشكل صريح جدًا: **Story 11 تنشئه، Story 14 لا تنشئه نهائيًا**؛ Story 14 تضيف فقط `TicketHistory` وتعمل integration مع flows الموجودة.
```
