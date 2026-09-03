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
Ticket Assignment
```

---

## Description

```text
Implement Ticket Assignment for the Customer Support CRM.

Authorized CRM users must be able to assign support tickets to an appropriate support agent/user and view the current assignment of each ticket.

A Ticket may be unassigned when it is first created. Assignment is handled separately from Ticket Creation & Tracking and must not be implemented as part of Story 11.

The implementation must follow the existing project conventions established by Stories 01–11:

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

### Assignment model

Each ticket can have zero or one currently assigned user/agent.

The assignment should reference an existing CRM User.

The implementation must inspect the existing User entity and relationships before deciding the exact foreign-key structure.

Do not create a duplicate User or Agent entity if the existing Users model can be reused.

The assigned user must be a valid CRM user according to the existing project's user model and authorization conventions.

### Ticket assignment

Authorized users with the appropriate ticket-management/assignment permission can:

- View the current assignee of a ticket.
- Assign an unassigned ticket to a user.
- Reassign an existing ticket to another valid user.
- Unassign a ticket when allowed by the existing requirements/conventions.
- View assignment information in ticket details and ticket lists where appropriate.

Assignment operations must validate that:

- The ticket exists.
- The selected user exists.
- The selected user is eligible to receive ticket assignments according to the existing CRM user/role conventions.
- The operation is authorized by the current user's permissions.

Invalid ticket IDs or user IDs must be rejected according to existing project conventions.

### Assignment history vs current assignment

Story 12 is responsible for the current ticket assignment.

Do not implement a complete ticket history/audit timeline UI in this story. Story 14 is responsible for Ticket History.

Assignment mutations must still be audit logged using the existing AuditLogService conventions.

Audit logging and Ticket History are separate concerns:

- AuditLog records the system/user action.
- Ticket History will later represent the ticket's business history/timeline.

Do not use AuditLog as a replacement for Story 14's Ticket History.

### Customer Interaction History integration

Assigning or reassigning a ticket is an internal CRM workflow action.

It must NOT automatically create a CustomerInteraction record unless the existing requirements explicitly establish assignment as customer activity.

CustomerInteraction is reserved for meaningful customer activity such as the ticket itself, communication, meeting, call, etc.

Therefore:

- Story 11 creates the CustomerInteraction for successful Ticket creation.
- Story 12 does not create another CustomerInteraction for assignment/reassignment.
- Do not create duplicate Interaction History entries when a ticket is assigned or reassigned.

### Assignment permissions

Use the existing Permissions.cs catalogue and inspect the actual existing ticket permissions before adding anything.

Potential permission conventions may include:

- tickets.view
- tickets.create
- tickets.manage

If an existing permission is appropriate for assignment, reuse it.

Do not create duplicate permission constants.

If a dedicated assignment permission is required by the existing requirements, add it consistently with the existing permission catalogue and DbSeeder conventions.

### Frontend

Add assignment functionality to the existing ticket UI rather than creating an unrelated standalone workflow.

Authorized users should be able to:

- See the current assignee.
- Assign a ticket.
- Reassign a ticket.
- Unassign a ticket if supported.
- Receive clear validation/permission/error feedback.

The UI must follow existing frontend patterns for loading, empty, error, success, forms, permissions, and API calls.

The assignment control should only be visible or usable when the current user has the required permission.

After successful assignment, the ticket detail/list state must reflect the new assignee.

### Persistence

Use EF Core and the existing SQL Server database.

The planner must inspect the existing Ticket entity created by Story 11 before deciding whether assignment requires:

- adding an AssignedUserId foreign key directly to Ticket, or
- another structure already established by the project.

Keep the implementation simple.

Do not introduce a separate assignment service/database/table unless the existing architecture or requirements require assignment history as a separate persisted concept.

If assignment is represented by a nullable AssignedUserId on Ticket, configure the foreign key appropriately and ensure deleting/deactivating a user does not unexpectedly delete tickets.

### Audit logging

Assignment, reassignment, and unassignment mutations must be audit logged according to the existing AuditLogService conventions.

The audit information should make the action understandable, including the affected ticket and relevant assignee information where supported by the existing audit model.

Do not duplicate audit records.

### No automatic CustomerInteraction

Assignment is not a customer interaction.

Do not create CustomerInteraction entries for:

- Assigning a ticket
- Reassigning a ticket
- Unassigning a ticket

The CustomerInteraction created by Story 11 when the ticket itself is created must remain the only automatic interaction related to ticket creation.

### No complete assignment automation

Do not implement automatic ticket assignment rules in this story.

Automatic assignment is a future automation concern and belongs to Story 24 — Automatic Ticket Assignment.

Story 12 provides manual/current ticket assignment only.

### No status/escalation workflow

Do not implement ticket status transitions, escalation rules, SLA logic, or automatic escalation.

Those concerns belong to Story 13 and later SLA/automation stories.

### No ticket history implementation

Do not implement the complete Ticket History feature.

Assignment changes may be audit logged, but the business-facing ticket history/timeline belongs to Story 14.
```

---

## Acceptance criteria

```text
### Ticket assignment

- [ ] Authenticated users with the required assignment/manage permission can assign a ticket.
- [ ] A ticket can have zero or one current assignee.
- [ ] A ticket can be assigned to an existing valid CRM user.
- [ ] Assigning an unassigned ticket stores the selected assignee correctly.
- [ ] Reassigning a ticket replaces the current assignee correctly.
- [ ] Unassignment is supported if allowed by the existing requirements/conventions.
- [ ] Invalid TicketId returns the project's appropriate 400/404 response.
- [ ] Invalid AssignedUserId returns the project's appropriate 400/404 response.
- [ ] Assignment cannot create a reference to a non-existent user.
- [ ] Assignment respects the existing CRM user/role eligibility rules.

### Ticket retrieval

- [ ] Authorized users can view the current assignee when retrieving a ticket.
- [ ] Ticket list results expose the current assignee where appropriate.
- [ ] Ticket details expose the current assignee.
- [ ] An unassigned ticket is represented clearly as unassigned.
- [ ] Assignment information is consistent between ticket list and ticket detail views.

### Assignment updates

- [ ] Authorized users can assign an unassigned ticket.
- [ ] Authorized users can reassign an already assigned ticket.
- [ ] Authorized users can unassign a ticket if supported by the existing requirements.
- [ ] Assignment mutations update the ticket correctly.
- [ ] Assignment does not modify CreatedAt or CreatedByUserId.
- [ ] Assignment does not modify unrelated ticket fields.
- [ ] Assignment does not change ticket status.
- [ ] Assignment does not perform status/escalation logic.
- [ ] Updating a non-existent ticket returns 404 according to project conventions.

### Authentication and permissions

- [ ] All assignment endpoints require authentication.
- [ ] Ticket retrieval continues to require the appropriate ticket view permission.
- [ ] Assignment mutations require the appropriate existing ticket manage/assignment permission.
- [ ] Unauthenticated requests return 401.
- [ ] Authenticated users without the required permission receive 403.
- [ ] Permission slugs follow the existing Permissions.cs convention.
- [ ] Existing ticket permissions are reused where appropriate.
- [ ] No duplicate permission constants are introduced.
- [ ] Required permissions are seeded consistently using the existing DbSeeder convention.

### User validation

- [ ] The selected assignee must exist in the existing Users table/model.
- [ ] The implementation does not create a duplicate Agent entity.
- [ ] The implementation follows the existing project's rules for active/eligible users.
- [ ] Invalid/ineligible assignees are rejected with a clear validation/error response.
- [ ] Assignment does not bypass existing user authorization rules.

### Audit Log

- [ ] Ticket assignment is audit logged according to existing AuditLogService conventions.
- [ ] Ticket reassignment is audit logged.
- [ ] Ticket unassignment is audit logged when supported.
- [ ] Audit records identify the affected ticket.
- [ ] Audit logging does not create CustomerInteraction records.
- [ ] Audit logging does not create duplicate records.

### Customer Interaction History

- [ ] Assigning a ticket does not create a CustomerInteraction record.
- [ ] Reassigning a ticket does not create a CustomerInteraction record.
- [ ] Unassigning a ticket does not create a CustomerInteraction record.
- [ ] The CustomerInteraction created by Story 11 for ticket creation remains unchanged.
- [ ] No duplicate Interaction History entry is created as a side effect of assignment.
- [ ] Story 12 does not add generic CustomerInteraction CRUD.

### Automatic assignment

- [ ] Story 12 does not implement automatic ticket assignment.
- [ ] No assignment rules engine is introduced.
- [ ] No automatic assignment based on department, workload, round-robin, SLA, or other rules is implemented.
- [ ] Automatic assignment remains reserved for Story 24.

### Ticket Status and Escalation

- [ ] Story 12 does not implement ticket status transitions.
- [ ] Story 12 does not implement escalation.
- [ ] Story 12 does not implement SLA calculations or escalation rules.
- [ ] Assignment does not automatically change ticket status unless explicitly required by the existing Story 12 requirements.

### Ticket History

- [ ] Story 12 does not implement the complete Ticket History feature.
- [ ] No separate business-facing ticket history/timeline UI is introduced.
- [ ] Assignment mutations may be audit logged but are not treated as a replacement for Story 14 Ticket History.

### Persistence

- [ ] Ticket persistence is updated using EF Core.
- [ ] The assignment relationship uses the existing User entity.
- [ ] The Ticket entity has the appropriate nullable assignment relationship if supported by the existing architecture.
- [ ] Foreign key behavior is configured safely.
- [ ] Deleting/deactivating a user does not unexpectedly delete the associated ticket.
- [ ] Appropriate indexes are added if required by common assignment/ticket queries.
- [ ] EF migration is generated if schema changes are required.
- [ ] Migration applies successfully.
- [ ] Migration does not modify unrelated tables or data.

### Frontend

- [ ] Authorized users can view ticket assignment information.
- [ ] Authorized users with the required permission can assign a ticket.
- [ ] Authorized users can reassign a ticket.
- [ ] Unassignment is available if supported.
- [ ] Assignment UI uses existing CRM user data.
- [ ] The UI does not allow selecting a non-existent user.
- [ ] Permission-gated users cannot perform assignment mutations.
- [ ] Loading state is displayed while assignment data is loading.
- [ ] Submission/loading state is displayed while assignment is being changed.
- [ ] Backend validation/errors are displayed clearly.
- [ ] Success feedback follows existing frontend conventions.
- [ ] Ticket details refresh/update after successful assignment.
- [ ] Ticket list reflects the current assignee where displayed.
- [ ] No manual CustomerInteraction action is shown after assignment.

### Verification

- [ ] Backend production code builds successfully.
- [ ] EF migration generates successfully if schema changes are required.
- [ ] EF migration applies successfully.
- [ ] Ticket retrieval smoke test shows the current assignee.
- [ ] Ticket assignment API smoke test succeeds with a valid ticket and valid user.
- [ ] Ticket reassignment API smoke test succeeds.
- [ ] Ticket unassignment API smoke test succeeds if supported.
- [ ] Invalid ticket/user validation smoke checks succeed.
- [ ] Authentication and permission smoke checks confirm 401/403 behavior.
- [ ] Audit log smoke check confirms assignment mutations are logged.
- [ ] Customer Interaction History smoke check confirms assignment does not create a duplicate interaction.
- [ ] Frontend build succeeds.
- [ ] Manual end-to-end assignment/reassignment flow succeeds.
- [ ] Manual regression checks confirm existing login, users, roles, departments, branches, customers, interaction history, notes, attachments, and ticket creation/tracking remain functional.
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
  * Existing User entity and user-management implementation
  * Existing EF Core / SQL Server infrastructure
  * Existing React ticket/customer feature and permission patterns

Story 11 must be completed before Story 12 because Story 12 assigns tickets created by Story 11.

Story 12 must not depend on Story 13 or Story 14 for its core implementation.

---

## Extra notes

* Keep implementation simple and consistent with the existing CRM architecture.
* Do not introduce CQRS, repositories, message brokers, microservices, or other new architecture unless the existing project already uses them.
* Do not create unit tests.
* Verification must use build checks, migration checks, API smoke tests, frontend build, manual end-to-end testing, and manual regression checks.
* Do not seed fake assignment activity merely to demonstrate the feature.
* Create/use real tickets and existing CRM users for manual verification.
* Assignment is a ticket workflow action, not a customer interaction.
* Do not create CustomerInteraction records for assignment, reassignment, or unassignment.
* Do not add generic CustomerInteraction CRUD.
* Do not modify Story 08 Interaction History semantics.
* Do not implement automatic assignment rules.
* Do not implement ticket status/escalation workflow.
* Do not implement the complete Ticket History feature.

### Critical business rule

Manual ticket assignment must result in:

```text
Ticket
   │
   ├── Assign / Reassign
   │       │
   │       └── AssignedUserId
   │
   ├── AuditLog
   │       └── Assignment action
   │
   └── CustomerInteraction
           └── NO NEW ENTRY
```

The existing CustomerInteraction created by Story 11 represents the customer activity of creating the ticket.

Assignment itself is an internal CRM workflow action and must not create another Interaction History record.

### Future automation

Automatic assignment rules are intentionally excluded from Story 12.

Future flow:

```text
New / Existing Ticket
        ↓
Automatic Assignment Rules
        ↓
Select Agent
        ↓
Assign Ticket
```

This belongs to Story 24 — Automatic Ticket Assignment and must not be implemented here.

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
* User: inspect existing Story 02 implementation
* Ticket: Story 11 implementation
* Ticket Category / Priority: Story 10 implementation
* CustomerInteraction: Story 08 implementation

The planner must inspect the actual existing Ticket entity, DTOs, service, controller, and database configuration from Story 11 before implementing assignment.

The planner must inspect the actual existing User entity and user-management conventions from Story 02 before deciding which users are eligible for assignment.

The planner must inspect `Permissions.cs` and reuse existing ticket permissions if they already support assignment. Do not create duplicate permission constants.

Potential permission conventions:

```text
tickets.view
tickets.create
tickets.manage
```

The planner must inspect the actual project before finalizing whether `tickets.manage` is sufficient or a dedicated assignment permission is required.

Potential API structure:

```text
GET    /api/tickets/{id}
PUT    /api/tickets/{id}/assignment
```

The planner must inspect existing controller routing and update conventions before finalizing exact routes.

Potential request structure:

```text
{
  "assignedUserId": "..."
}
```

If unassignment is supported:

```text
{
  "assignedUserId": null
}
```

The planner must follow the actual existing DTO and API conventions rather than blindly using these examples.

---

## Out of scope

* Ticket Categories & Priorities management — Story 10
* Ticket Creation & Tracking — Story 11
* Ticket Status workflow — Story 13
* Ticket Escalation — Story 13
* Ticket History — Story 14
* SLA configuration and automation
* Automatic ticket assignment — Story 24
* Assignment rules engine
* Workload/round-robin assignment
* Notifications
* Email integration
* SMS integration
* WhatsApp integration
* Live Chat
* Customer Portal
* AI features
* Reports
* Knowledge Base
* CustomerInteraction creation for assignment
* CustomerInteraction editing/deletion UI
* Generic manual CustomerInteraction CRUD
* Changes to Story 08 Interaction History semantics
* Customer Notes & Attachments
* Unit tests
* Frontend component tests
* Backend integration tests
* Regression test-suite execution

```
```
