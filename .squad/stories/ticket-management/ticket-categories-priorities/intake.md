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
Ticket Categories & Priorities
```

---

## Description

```text
Implement the foundational configuration for Ticket Management by allowing authorized CRM users to manage Ticket Categories and Ticket Priorities.

Ticket Categories represent the classification/type of a support ticket, such as:

- Technical Support

- Billing

- Account / Access

- General Inquiry

- Complaint

- Feature Request

Ticket Priorities represent the urgency/severity of a ticket, such as:

- Low

- Medium

- High

- Urgent

This story is configuration/master-data only.

It must provide backend APIs and frontend management screens for authorized users to:

- View categories

- Create categories

- Edit categories

- Activate/deactivate categories if the existing project convention supports an active/inactive state

- View priorities

- Create priorities

- Edit priorities

- Activate/deactivate priorities if the existing project convention supports an active/inactive state

The implementation must follow the existing project conventions established by Stories 01–09:

- ASP.NET Core Web API

- EF Core

- SQL Server

- Controller → Service → DTO layering

- JWT authentication

- Permission-based authorization using [HasPermission]

- Audit logging for mutations

- React + TypeScript frontend

- Existing http client and feature-module conventions

- Existing loading / empty / error / success UI patterns

Categories and priorities are independent configuration entities.

Do not implement ticket creation, ticket assignment, ticket status, escalation, ticket history, or customer interaction creation in this story.

The entities created here will be referenced by later Ticket Management stories, especially Story 11 — Ticket Creation & Tracking.

When Story 11 creates a ticket, the selected CategoryId and PriorityId should reference the master data created by this story.

Interaction History integration is NOT implemented here.

Later stories may create CustomerInteraction records automatically when real customer activity occurs. For example, Story 11 may follow:

Create Ticket

    ↓

Save Ticket

    ↓

Create CustomerInteraction

    ↓

Type = "ticket"

    ↓

Customer Interaction History

That integration belongs to the later ticket/activity story, Story 11.
```

---

## Acceptance criteria

```text
### Ticket Categories

- [ ] Authorized users can retrieve the list of ticket categories.

- [ ] Authorized users can retrieve a single ticket category.

- [ ] Authorized users with manage permission can create a ticket category.

- [ ] Authorized users with manage permission can update a ticket category.

- [ ] If the project uses active/inactive master-data conventions, authorized users can activate/deactivate a category.

- [ ] Category names are required and cannot be empty or whitespace.

- [ ] Category names cannot contain unintended duplicate values according to the project's existing uniqueness conventions.

- [ ] Category records have stable IDs.

- [ ] Category creation/update mutations are audit logged.

- [ ] APIs return 404 when requesting a category that does not exist.

- [ ] Validation failures return 400 using the project's existing validation/error conventions.

### Ticket Priorities

- [ ] Authorized users can retrieve the list of ticket priorities.

- [ ] Authorized users can retrieve a single ticket priority.

- [ ] Authorized users with manage permission can create a ticket priority.

- [ ] Authorized users with manage permission can update a ticket priority.

- [ ] If the project uses active/inactive master-data conventions, authorized users can activate/deactivate a priority.

- [ ] Priority names are required and cannot be empty or whitespace.

- [ ] Priority values have a deterministic ordering suitable for displaying Low → Medium → High → Urgent or the configured equivalent.

- [ ] Priority records have stable IDs.

- [ ] Priority creation/update mutations are audit logged.

- [ ] APIs return 404 when requesting a priority that does not exist.

- [ ] Validation failures return 400 using the project's existing validation/error conventions.

### Authentication and permissions

- [ ] All category and priority endpoints require authentication.

- [ ] Read endpoints require dedicated view permissions.

- [ ] Create/update/activate/deactivate endpoints require dedicated manage permissions.

- [ ] An unauthenticated request returns 401.

- [ ] An authenticated user without the required permission receives 403.

- [ ] Permission names follow the existing Permissions.cs naming convention.

- [ ] New permissions are included in the permission catalogue / All collection and seeded consistently with the existing project.

- [ ] Admin receives the new permissions using the existing DbSeeder convention.

### Persistence

- [ ] TicketCategory and TicketPriority entities are persisted through EF Core.

- [ ] DbSets are added to CrmDbContext.

- [ ] Entity configuration follows the existing Departments / Branches / Customer conventions.

- [ ] Required fields have appropriate SQL constraints and maximum lengths.

- [ ] Appropriate indexes / unique constraints are added where required.

- [ ] A new EF Core migration is generated and applies cleanly.

- [ ] The migration does not modify unrelated existing tables or data.

### Frontend

- [ ] A Ticket Categories management screen is available to users with the appropriate view permission.

- [ ] A Ticket Priorities management screen is available to users with the appropriate view permission.

- [ ] Users with manage permission can create and edit categories.

- [ ] Users with manage permission can create and edit priorities.

- [ ] Manage actions are hidden/disabled when the user lacks the corresponding manage permission.

- [ ] The UI handles loading, empty, error, and success states using existing project conventions.

- [ ] Validation errors from the backend are displayed clearly.

- [ ] Category and priority lists refresh correctly after create/update operations.

- [ ] Priority ordering is displayed consistently according to the configured order.

- [ ] Existing application navigation/layout conventions are reused.

### Integration with future Ticket stories

- [ ] Category and Priority IDs are designed to be referenced by the future Ticket entity.

- [ ] Story 10 does not create the Ticket entity.

- [ ] Story 10 does not implement ticket creation.

- [ ] Story 10 does not implement ticket assignment.

- [ ] Story 10 does not implement ticket status or escalation.

- [ ] Story 10 does not implement Ticket History.

- [ ] Story 10 does not insert CustomerInteraction records.

- [ ] Story 10 does not modify the existing Story 08 Interaction History behavior.

### Verification

- [ ] Backend production code builds successfully.

- [ ] EF migration is generated and applies successfully.

- [ ] Category API smoke checks pass for authentication, permissions, CRUD, validation, and 404 handling.

- [ ] Priority API smoke checks pass for authentication, permissions, CRUD, validation, ordering, and 404 handling.

- [ ] Frontend builds successfully.

- [ ] Manual end-to-end verification confirms category and priority management works from the UI.

- [ ] Manual regression checks confirm existing login, users, roles, departments, branches, customers, interaction history, notes, attachments, and system settings remain functional.

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

  * Story 02 — Users Management

  * Story 03 — Roles & Permissions

  * Story 05 — Audit Logs

  * Story 07 — Customer Profiles & Contact Details

  * Existing EF Core / SQL Server infrastructure

  * Existing React feature-module and permission-check patterns

Story 11 — Ticket Creation & Tracking will depend on the Category and Priority entities created here.

---

## Extra notes

* Ticket Categories and Ticket Priorities are **master/configuration data**, not tickets themselves.

* Keep the implementation simple and consistent with existing Departments / Branches patterns.

* Do not introduce unnecessary abstractions, repositories, CQRS, or new architectural patterns.

* Do not create unit tests for this story.

* Verification should use production builds, migration verification, API smoke checks, frontend build, and manual end-to-end/regression checks.

* Do not seed arbitrary demo tickets or customer interactions.

* Initial category/priority seed values should only be added if the existing project convention requires master-data seeding. If seeded, keep the values minimal and clearly defined.

* Do not confuse `CustomerInteraction` with `AuditLog`.

  * `AuditLog` records system/user actions.

  * `CustomerInteraction` represents customer-facing/internal customer activity shown in Interaction History.

* Future Ticket stories should consider whether creating/updating/closing a ticket should automatically create an appropriate `CustomerInteraction` entry. This is intentionally outside Story 10.

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

* Reference implementations:

  * Departments

  * Branches

  * Users / Roles permission management

  * System Settings

* Follow existing naming, routing, DTO, service, DI, error-handling, and frontend API conventions rather than introducing new patterns.

Potential permission slugs should follow the existing convention, for example:

```text
tickets.categories.view

tickets.categories.manage

tickets.priorities.view

tickets.priorities.manage
```

The planner must inspect `Permissions.cs` before finalizing the exact permission constants/slugs and must not blindly assume these names if the existing convention differs.

---

## Out of scope

* Ticket creation

* Ticket editing/tracking

* Ticket assignment

* Ticket status management

* Ticket escalation

* Ticket History

* SLA rules

* Automatic ticket assignment

* Notifications

* Email / SMS / WhatsApp

* Live Chat

* Customer Portal

* AI features

* Reports

* Customer Interaction History CRUD

* Manual creation/editing/deletion of CustomerInteraction records

* Automatic CustomerInteraction creation

* Changes to Story 08 Interaction History

* Customer Notes & Attachments

* Customer profile/contact details

* Unit tests

* Frontend component tests

* Backend integration tests

* Regression test-suite execution

The implementation must remain limited to **Ticket Categories & Ticket Priorities master data**.

---

## Project Story Sequence

```text
CYCLE 0 — FOUNDATION
│
└── FEATURE — foundation-and-administration
    │
    ├── Story 01 — Authentication & Login
    ├── Story 02 — Users Management
    ├── Story 03 — Roles & Permissions
    ├── Story 04 — Departments & Branches
    ├── Story 05 — Audit Logs
    └── Story 06 — System Configuration & Branding
```

```text
CYCLE 1 — CUSTOMER MANAGEMENT
│
└── FEATURE — customer-management
    │
    ├── Story 07 — Customer Profiles & Contact Details
    ├── Story 08 — Customer Interaction History
    └── Story 09 — Customer Notes & Attachments
```

```text
CYCLE 2 — TICKET MANAGEMENT
│
└── FEATURE — ticket-management
    │
    ├── Story 10 — Ticket Categories & Priorities
    │
    ├── Story 11 — Ticket Creation & Tracking
    │
    ├── Story 12 — Ticket Assignment
    │
    ├── Story 13 — Ticket Status & Escalation
    │
    └── Story 14 — Ticket History
```

### Story 10 Boundary

```text
Story 10
Ticket Categories & Priorities
        │
        ├── TicketCategory
        ├── TicketPriority
        ├── CRUD / management
        ├── Permissions
        ├── Audit logging
        ├── EF Core persistence
        ├── Migration
        └── Frontend management UI
```

Story 10 must **not** implement the Ticket entity or any Ticket lifecycle functionality.

### Story 11 Dependency

```text
Story 10
Categories + Priorities
        │
        ↓
Story 11
Ticket Creation & Tracking
        │
        ├── Ticket
        ├── Customer relationship
        ├── Category
        ├── Priority
        └── Automatic CustomerInteraction
```
