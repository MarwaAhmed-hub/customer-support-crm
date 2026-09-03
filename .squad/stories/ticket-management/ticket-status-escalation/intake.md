# Story intake

## Feature

* **Feature name (display):** Ticket Management
* **Feature slug (folder under `plans/`):** `ticket-management`

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
Ticket Status & Escalation
```

---

## Description

```text
Implement Ticket Status & Escalation for the Customer Support CRM.

Authorized CRM users must be able to view and update the current status of a support ticket according to the project's agreed ticket-status lifecycle.

The implementation must also provide the foundation for ticket escalation without implementing the complete SLA/automation rules that belong to later stories.

The implementation must follow the existing project conventions established by Stories 01–12:

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

### Ticket status

Each ticket must have a current status.

The planner must inspect the existing Ticket entity from Story 11 and reuse the existing status field/convention if one already exists.

Do not introduce a duplicate status property or a second status model if the existing Ticket implementation already has one.

The status lifecycle should follow the project's agreed ticket-status convention.

At minimum, the implementation should support the statuses required by the existing CRM requirements, such as:

- Open
- In Progress
- Pending
- Resolved
- Closed

The planner must inspect the existing project/requirements before finalizing the exact status values.

Do not blindly add statuses if an existing status convention is already implemented.

### Status transitions

Authorized users must be able to change a ticket's status according to the allowed transition rules.

The implementation must prevent invalid status transitions.

For example, the exact transition rules should be defined according to the project's requirements rather than allowing every status to transition freely.

A possible lifecycle is:

Open
  ↓
In Progress
  ↓
Pending
  ↓
In Progress
  ↓
Resolved
  ↓
Closed

The actual allowed transitions must be confirmed from the existing requirements/project conventions before implementation.

If reopening is supported, the allowed reopening path must also be explicitly defined.

### Status update

A status update must:

- Validate that the ticket exists.
- Validate that the requested status is a supported status.
- Validate that the transition from the current status to the requested status is allowed.
- Update the ticket's current status.
- Update UpdatedAt.
- Preserve CreatedAt and CreatedByUserId.
- Be audit logged.

Status updates must not bypass permission checks.

### Ticket Assignment interaction

Changing a ticket's status must not create a CustomerInteraction record automatically.

CustomerInteraction represents customer activity and is not a replacement for ticket workflow state.

Story 11 creates the CustomerInteraction when the ticket itself is created.

Story 12 handles assignment.

Story 13 handles ticket status/escalation.

Do not create duplicate Interaction History entries for:

- Status changes
- Escalation
- Assignment
- Reassignment

### Ticket History integration

Status changes are meaningful ticket business events and must be represented in a way that can later be consumed by Story 14 — Ticket History.

However, Story 13 must not implement the complete Ticket History UI.

The planner must inspect the existing Ticket History design/conventions before deciding whether status changes should be persisted through an existing history mechanism or another simple structure.

Do not use AuditLog as a substitute for Ticket History if Story 14 requires business-facing ticket history.

If Story 14's persistence model has not yet been implemented, keep Story 13's implementation compatible with it without prematurely implementing the full Story 14 feature.

### Escalation

Story 13 must support the basic concept of a ticket being escalated according to the project's requirements.

The planner must inspect the existing requirements and project conventions before deciding the exact escalation representation.

Escalation may require information such as:

- IsEscalated
- EscalatedAt
- EscalatedBy
- Escalation reason
- Escalation level

Only implement fields/concepts that are actually required by the existing requirements.

Do not introduce a complex escalation rules engine.

### Manual escalation

If the requirements require manual escalation, authorized users must be able to escalate a ticket through the UI/API.

A manual escalation must:

- Validate the ticket exists.
- Validate the escalation request.
- Update the appropriate escalation state.
- Audit log the escalation action.
- Not create a CustomerInteraction record.

If the requirements support de-escalation, it must follow the same authorization and audit conventions.

### Automatic escalation

Do not implement automatic SLA-driven escalation in this story.

Automatic escalation based on response/resolution targets, timers, business hours, or SLA rules belongs to the later SLA & Automation cycle.

Story 23 — SLA Response & Resolution Targets

Story 24 — Automatic Ticket Assignment

Story 25 — Escalation Rules

Story 26 — Alerts & Notifications

Story 13 should provide only the status/escalation capabilities explicitly required here and must not implement the full automation engine.

### Permissions

Use the existing Permissions.cs catalogue.

Inspect existing ticket permissions before adding anything.

Potential permissions may include:

- tickets.view
- tickets.create
- tickets.manage

If `tickets.manage` already exists and is appropriate for status/escalation mutations, reuse it.

Do not create duplicate permission constants.

If a dedicated status/escalation permission is required by the existing requirements, add it consistently with Permissions.cs and DbSeeder conventions.

### Audit logging

Status changes must be audit logged.

Escalation and de-escalation must be audit logged when supported.

Audit records must remain separate from CustomerInteraction and Ticket History.

### Frontend

Extend the existing ticket detail/list UI.

Authorized users should be able to:

- View the current status.
- Change the status when permitted.
- See clear validation when a transition is not allowed.
- See escalation state when supported.
- Perform manual escalation/de-escalation when supported by the requirements.

The UI must follow the existing CRM patterns for:

- Permission gating
- Loading
- Empty states
- Error handling
- Success feedback
- API calls
- Forms/controls

Do not create a separate unrelated ticket workflow application.

### Persistence

Use EF Core and the existing SQL Server database.

The planner must inspect the existing Ticket entity from Story 11 before deciding whether schema changes are required.

Reuse existing status/escalation fields if already present.

Only add migrations when required.

Foreign keys and indexes must follow existing project conventions.

Do not modify unrelated tables or data.
```

---

## Acceptance criteria

```text
### Ticket Status

- [ ] Every ticket has a valid current status.
- [ ] The implementation reuses the existing Ticket status field/convention when already available.
- [ ] Supported status values follow the project's agreed ticket-status convention.
- [ ] At minimum, the required lifecycle statuses from the existing requirements are supported.
- [ ] Invalid status values are rejected.
- [ ] A ticket's current status can be retrieved through the ticket API.
- [ ] Authorized users can update ticket status when they have the required permission.

### Status transitions

- [ ] Allowed status transitions are explicitly defined according to project requirements.
- [ ] Valid status transitions succeed.
- [ ] Invalid status transitions are rejected.
- [ ] The API does not allow arbitrary status changes that violate the transition rules.
- [ ] Status changes update UpdatedAt.
- [ ] CreatedAt is not changed by a status update.
- [ ] CreatedByUserId is not changed by a status update.
- [ ] Updating a non-existent ticket returns 404.
- [ ] Status updates do not modify unrelated ticket fields.

### Authentication and permissions

- [ ] All status/escalation endpoints require authentication.
- [ ] Ticket retrieval requires the appropriate ticket view permission.
- [ ] Status mutation requires the appropriate ticket manage/status permission.
- [ ] Escalation mutation requires the appropriate ticket manage/escalation permission.
- [ ] Unauthenticated requests return 401.
- [ ] Authenticated users without the required permission receive 403.
- [ ] Existing permission slugs are reused where appropriate.
- [ ] No duplicate permission constants are introduced.
- [ ] Required permissions are seeded consistently using DbSeeder.

### Audit Log

- [ ] Every successful status change is audit logged.
- [ ] Manual escalation is audit logged when supported.
- [ ] Manual de-escalation is audit logged when supported.
- [ ] Audit records identify the affected ticket and action where supported by the existing AuditLog model.
- [ ] Failed status changes do not create misleading successful audit records.
- [ ] Audit logging does not create CustomerInteraction records.

### Customer Interaction History

- [ ] Status changes do not create CustomerInteraction records.
- [ ] Escalation does not create CustomerInteraction records.
- [ ] De-escalation does not create CustomerInteraction records.
- [ ] Assignment/reassignment remains governed by Story 12.
- [ ] The CustomerInteraction created by Story 11 for ticket creation remains unchanged.
- [ ] No duplicate Interaction History entries are created by Story 13.
- [ ] No generic CustomerInteraction CRUD is introduced.

### Ticket History

- [ ] Status changes are implemented in a way compatible with Story 14 Ticket History.
- [ ] If an existing Ticket History persistence mechanism is available, status changes use it according to project conventions.
- [ ] Story 13 does not implement the complete Ticket History UI.
- [ ] AuditLog is not incorrectly used as a replacement for Ticket History.

### Escalation

- [ ] The implementation follows the project's defined escalation requirements.
- [ ] The ticket can represent its current escalation state when required.
- [ ] Authorized users can manually escalate a ticket if manual escalation is part of the requirements.
- [ ] Manual escalation validates the ticket before changing escalation state.
- [ ] Escalation changes are audit logged.
- [ ] De-escalation is supported only if required by the project's requirements.
- [ ] Escalation does not create CustomerInteraction records.
- [ ] Escalation does not unexpectedly change the ticket assignment unless explicitly required.
- [ ] Escalation does not unexpectedly change the ticket status unless explicitly required.

### Automatic escalation boundaries

- [ ] Story 13 does not implement SLA timers.
- [ ] Story 13 does not implement automatic escalation rules.
- [ ] Story 13 does not implement business-hours calculations.
- [ ] Story 13 does not implement automatic ticket assignment.
- [ ] Story 13 does not implement notifications triggered by escalation.
- [ ] These concerns remain available for Stories 23–26.

### Frontend

- [ ] Authorized users can view ticket status.
- [ ] Authorized users with the required permission can change ticket status.
- [ ] The UI prevents or clearly rejects invalid status transitions.
- [ ] Current escalation state is displayed when applicable.
- [ ] Authorized users can manually escalate/de-escalate when supported.
- [ ] Permission-gated users cannot perform status/escalation mutations.
- [ ] Loading states follow existing frontend conventions.
- [ ] API validation errors are displayed clearly.
- [ ] Success feedback follows existing frontend conventions.
- [ ] Ticket details refresh/update after a successful status/escalation operation.
- [ ] No manual CustomerInteraction action is shown for status/escalation.

### Persistence

- [ ] Ticket status is persisted through EF Core.
- [ ] Escalation state is persisted only when required.
- [ ] Existing Ticket fields are reused where appropriate.
- [ ] Database constraints prevent invalid persisted status values where appropriate.
- [ ] Required indexes are added if needed by status/escalation queries.
- [ ] EF migration is generated if schema changes are required.
- [ ] Migration applies successfully.
- [ ] Migration does not modify unrelated tables or data.

### Verification

- [ ] Backend production code builds successfully.
- [ ] EF migration generates successfully if schema changes are required.
- [ ] EF migration applies successfully.
- [ ] Ticket retrieval smoke test confirms current status.
- [ ] Valid status transition API smoke tests succeed.
- [ ] Invalid status transition smoke test is rejected.
- [ ] Invalid status value smoke test is rejected.
- [ ] Authentication and permission smoke checks confirm 401/403 behavior.
- [ ] Audit log smoke check confirms status/escalation mutations are recorded.
- [ ] Customer Interaction History smoke check confirms status/escalation does not create duplicate interactions.
- [ ] Frontend build succeeds.
- [ ] Manual end-to-end status workflow succeeds.
- [ ] Manual escalation/de-escalation workflow succeeds when supported.
- [ ] Manual regression checks confirm existing login, users, roles, departments, branches, customers, interaction history, notes, attachments, ticket creation/tracking, and ticket assignment remain functional.
- [ ] No unit-test creation or test-suite execution is required for this story.
```

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
  * Existing Ticket entity and ticket-management implementation
  * Existing EF Core / SQL Server infrastructure
  * Existing React ticket feature and permission patterns

Story 11 must be completed because Story 13 operates on the Ticket entity created there.

Story 12 should be completed so status/escalation behavior does not conflict with the ticket assignment implementation.

Story 14 is related because status changes may later appear in Ticket History, but Story 13 must not implement the complete Story 14 feature.

---

## Extra notes

* Keep implementation simple and consistent with the existing CRM architecture.
* Do not introduce CQRS, repositories, message brokers, microservices, or other new architecture unless the existing project already uses them.
* Do not create unit tests.
* Verification must use build checks, migration checks, API smoke tests, frontend build, manual end-to-end testing, and manual regression checks.
* Do not seed fake status changes or escalation records merely to demonstrate the feature.
* Use real tickets for manual verification.
* Do not create CustomerInteraction records for status changes or escalation.
* Do not add generic CustomerInteraction CRUD.
* Do not modify Story 08 Interaction History semantics.
* Do not implement automatic SLA escalation.
* Do not implement automatic ticket assignment.
* Do not implement notification automation.
* Do not implement the complete Ticket History feature.

### Critical business rules

```text
Ticket
   │
   ├── Status
   │      └── Controlled by allowed status transitions
   │
   ├── Escalation
   │      └── Manual only where required
   │
   ├── AuditLog
   │      └── Status / escalation actions
   │
   └── CustomerInteraction
          └── NO NEW ENTRY
```

Creating the ticket in Story 11 creates the customer interaction:

```text
Create Ticket
      ↓
Save Ticket
      ↓
Create CustomerInteraction
      ↓
Type = "ticket"
      ↓
Customer Interaction History
```

After that, Story 13 status/escalation operations must not create additional CustomerInteraction records.

### Status lifecycle

The planner must inspect the actual requirements and existing code before finalizing the lifecycle.

A possible example is:

```text
Open
  ↓
In Progress
  ↓
Pending
  ↓
In Progress
  ↓
Resolved
  ↓
Closed
```

Do not assume this exact lifecycle if the existing requirements define different statuses or transitions.

### Future automation boundary

Story 13 provides the basic status/escalation capability.

Future automation may build on it:

```text
Ticket
  ↓
SLA / Automation Rules
  ↓
Automatic Escalation
  ↓
Notifications
```

Those concerns belong to Stories 23–26 and must not be implemented prematurely.

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
* Assignment: Story 12 implementation
* CustomerInteraction: Story 08 implementation

The planner must inspect the actual existing Ticket entity, DTOs, service, controller, and database configuration from Story 11 before implementing status/escalation.

The planner must inspect the actual Ticket Assignment implementation from Story 12 so status/escalation changes do not overwrite or conflict with assignment fields.

The planner must inspect the actual CustomerInteraction entity/service from Story 08 before making any integration decision.

The planner must inspect `Permissions.cs` and reuse existing ticket permissions if they already support status/escalation.

Potential permission convention:

```text
tickets.view
tickets.create
tickets.manage
```

The planner must inspect the actual project before deciding whether `tickets.manage` is sufficient or a dedicated status/escalation permission is required.

Potential API structure:

```text
GET    /api/tickets/{id}
PUT    /api/tickets/{id}/status
PUT    /api/tickets/{id}/escalation
```

These are examples only. The planner must inspect existing controller routing and API conventions before finalizing exact routes.

Potential status request:

```text
{
  "status": "InProgress"
}
```

Potential escalation request:

```text
{
  "isEscalated": true,
  "reason": "..."
}
```

These are examples only. The planner must follow the actual domain model and requirements.

The planner must not create a second Ticket status model if Story 11 already established one.

---

## Out of scope

* Ticket Categories & Priorities management — Story 10
* Ticket Creation & Tracking — Story 11
* Ticket Assignment — Story 12
* Complete Ticket History — Story 14
* SLA Response & Resolution Targets — Story 23
* Automatic Ticket Assignment — Story 24
* Escalation Rules / automatic escalation — Story 25
* Alerts & Notifications — Story 26
* Automatic SLA timers
* Business-hours/SLA calculations
* Automatic assignment
* Workload/round-robin assignment
* Notification automation
* Email integration
* SMS integration
* WhatsApp integration
* Live Chat
* Customer Portal
* AI features
* Reports
* Knowledge Base
* CustomerInteraction creation for status/escalation
* Generic manual CustomerInteraction CRUD
* CustomerInteraction editing/deletion UI
* Changes to Story 08 Interaction History semantics
* Customer Notes & Attachments
* Unit tests
* Frontend component tests
* Backend integration tests
* Regression test-suite execution
