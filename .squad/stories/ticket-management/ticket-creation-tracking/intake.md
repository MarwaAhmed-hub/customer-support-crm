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
Ticket Creation & Tracking
```

---

## Description

```text
Implement Ticket Creation & Tracking for the Customer Support CRM.

Authorized CRM users must be able to create, view, update, and track support tickets associated with a customer.

A Ticket represents a customer support request and must be associated with an existing Customer.

Each ticket should contain, at minimum, the information required by the existing CRM requirements and the previously implemented Ticket Categories & Priorities master data:

- Ticket ID
- Customer ID
- Subject
- Description
- Category ID
- Priority ID
- Created By User ID
- Created At
- Updated At
- Current status according to the project's ticket-status convention

Use the Ticket Category and Ticket Priority entities created by Story 10.

The implementation must follow the existing project conventions established by Stories 01–10:

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
- Existing customer feature structure

### Customer relationship

Every ticket must belong to an existing Customer.

A ticket cannot be created for a non-existent customer.

The customer detail page should provide a way to view the customer's tickets, or navigate to the ticket creation/list experience while preserving the customer relationship.

### Ticket creation

Authorized users with ticket-create permission can create a ticket by selecting:

- Customer
- Subject
- Description
- Category
- Priority

The selected CategoryId must reference an existing Ticket Category from Story 10.

The selected PriorityId must reference an existing Ticket Priority from Story 10.

Invalid customer/category/priority IDs must be rejected with 400 or 404 according to existing project conventions.

### Ticket tracking

Authorized users can:

- View ticket details
- View ticket lists
- Search/filter tickets using the fields supported by the existing requirements
- Update ticket information allowed by this story
- See created/updated timestamps
- See who created the ticket
- See the ticket's current status

Do not implement the complete status/escalation workflow here. Story 13 is responsible for Ticket Status & Escalation.

Story 11 may store the initial status required for a newly created ticket, using the project's agreed initial status (for example Open), but advanced status transitions belong to Story 13.

### IMPORTANT — Customer Interaction History integration

Creating a Ticket is a real customer activity and MUST create a corresponding CustomerInteraction record automatically.

This integration belongs to Story 11.

The expected flow is:

Create Ticket

    ↓

Validate Customer / Category / Priority

    ↓

Save Ticket

    ↓

Create CustomerInteraction

    ↓

Type = "ticket"

    ↓

Customer Interaction History

The CustomerInteraction entry must reference the same CustomerId as the Ticket.

The interaction should contain enough information for Story 08 — Customer Interaction History to display the activity meaningfully, using the existing CustomerInteraction entity/schema established by Story 08.

Do NOT create a separate interaction-history table.

Do NOT duplicate the CustomerInteraction entity.

Do NOT make the user manually enter an interaction after creating a ticket.

The interaction entry is an automatic side effect of successful Ticket creation.

If Ticket creation fails and the Ticket is not persisted, the corresponding CustomerInteraction must not be persisted.

The implementation should keep the Ticket creation and its automatic CustomerInteraction creation consistent according to the project's existing EF Core transaction/unit-of-work conventions.

### Interaction vs Audit Log

Do not confuse these two concepts:

- AuditLog records system/user actions for auditing purposes.
- CustomerInteraction represents customer activity shown in the customer's Interaction History.

Creating a Ticket may therefore produce BOTH:

1. An AuditLog entry recording the user/system action.
2. A CustomerInteraction entry representing the customer activity.

They serve different purposes and must not replace each other.

### Relationship with future Ticket History

Story 14 is responsible for Ticket History.

Story 11 must NOT implement TicketHistory as a substitute for Story 14.

A successful Ticket creation may ultimately result in three separate records:

1. AuditLog — created according to the existing audit conventions.
2. CustomerInteraction — created by Story 11 with Type = "ticket".
3. TicketHistory — created by Story 14 as part of the Ticket lifecycle history.

These records have different purposes and must remain separate.

Story 11 is responsible for the Ticket creation and the automatic CustomerInteraction integration.

Story 14 is responsible for the TicketHistory record.

Story 14 must integrate with the existing Ticket creation flow rather than creating a second CustomerInteraction.

### Future integrations

Future communication stories may also create CustomerInteraction records when real customer activities occur, for example:

- Email activity → CustomerInteraction
- WhatsApp activity → CustomerInteraction
- SMS activity → CustomerInteraction
- Meeting activity → CustomerInteraction
- Call activity → CustomerInteraction

Those integrations are outside Story 11 unless explicitly required by later stories.

Story 11 specifically covers the Ticket → CustomerInteraction integration.

### No manual interaction CRUD

Story 11 must NOT add a generic UI for manually creating/editing/deleting CustomerInteraction records.

Interaction History remains governed by Story 08.

This story only creates the automatic interaction entry resulting from successful ticket creation.
```

---

## Acceptance criteria

```text
### Ticket creation

- [ ] Authenticated users with the required create permission can create a ticket.

- [ ] A ticket must reference an existing Customer.

- [ ] A ticket must reference an existing Ticket Category from Story 10.

- [ ] A ticket must reference an existing Ticket Priority from Story 10.

- [ ] Subject is required and cannot be empty or whitespace.

- [ ] Description follows the project's configured validation/max-length rules.

- [ ] Invalid CustomerId returns the project's appropriate 400/404 response.

- [ ] Invalid CategoryId returns the project's appropriate 400/404 response.

- [ ] Invalid PriorityId returns the project's appropriate 400/404 response.

- [ ] CreatedByUserId is taken from the authenticated user's claims.

- [ ] CreatedAt and UpdatedAt are stored as UTC.

- [ ] A newly created ticket receives the agreed initial status.

- [ ] Successful creation returns the created ticket DTO.

### Ticket retrieval and tracking

- [ ] Authorized users can retrieve a ticket by ID.

- [ ] Authorized users can retrieve/list tickets.

- [ ] Ticket list results include the relevant customer, category, priority, status, and creation information.

- [ ] Ticket details show customer, subject, description, category, priority, creator, timestamps, and current status.

- [ ] Non-existent ticket IDs return 404.

- [ ] Ticket/customer relationships are preserved correctly.

- [ ] Search/filter behavior follows the requirements and existing project conventions.

### Ticket updates

- [ ] Authorized users with manage/update permission can update fields allowed by this story.

- [ ] Updates validate referenced CategoryId and PriorityId when changed.

- [ ] UpdatedAt changes on successful update.

- [ ] CreatedAt and CreatedByUserId are not overwritten during update.

- [ ] Updating a non-existent ticket returns 404.

- [ ] Ticket update mutations are audit logged.

- [ ] Story 11 does not implement the complete status/escalation workflow owned by Story 13.

### Authentication and permissions

- [ ] All ticket endpoints require authentication.

- [ ] Read endpoints require the appropriate ticket view permission.

- [ ] Create/update endpoints require the appropriate ticket create/manage permission.

- [ ] Unauthenticated requests return 401.

- [ ] Authenticated users without the required permission receive 403.

- [ ] Permission slugs follow the existing Permissions.cs convention.

- [ ] Required permissions are included in the permission catalogue and seeded consistently.

- [ ] Admin receives the required permissions using the existing DbSeeder convention.

### Automatic CustomerInteraction creation

- [ ] Every successfully created Ticket automatically creates exactly one CustomerInteraction entry.

- [ ] The CustomerInteraction.CustomerId equals the Ticket.CustomerId.

- [ ] The CustomerInteraction type is `ticket`.

- [ ] The interaction is created automatically without any manual user action.

- [ ] The interaction contains a meaningful summary describing the ticket activity.

- [ ] The interaction author/agent information uses the authenticated ticket creator where supported by the existing CustomerInteraction model.

- [ ] The interaction timestamp corresponds to the ticket creation activity and is stored as UTC.

- [ ] Creating a Ticket does not require a separate manual "Log Interaction" action.

- [ ] Failed Ticket creation does not leave an orphan CustomerInteraction.

- [ ] Ticket creation and CustomerInteraction creation follow the existing EF Core transaction/unit-of-work conventions so the operation does not intentionally leave inconsistent data.

- [ ] The resulting CustomerInteraction is visible through the Story 08 Interaction History for the associated customer.

### Audit Log vs Interaction History

- [ ] Ticket creation is audit logged according to the existing AuditLogService conventions.

- [ ] The AuditLog entry and CustomerInteraction entry are treated as separate records with separate purposes.

- [ ] No existing AuditLog record is incorrectly used as the customer's Interaction History entry.

- [ ] No duplicate CustomerInteraction is created because of the audit operation.

### Ticket History boundary

- [ ] Story 11 does not create or persist TicketHistory records.

- [ ] Story 11 does not implement the Ticket History retrieval API or UI.

- [ ] Story 11 does not duplicate CustomerInteraction creation for the purpose of Ticket History.

- [ ] Story 14 remains responsible for TicketHistory creation and retrieval.

- [ ] Story 14 can integrate with the successful Ticket creation flow without replacing Story 11's CustomerInteraction behavior.

### Frontend

- [ ] Authorized users can access the ticket list.

- [ ] Authorized users can open ticket details.

- [ ] Authorized users with create permission can create a ticket.

- [ ] The create form allows selecting Customer, Category, and Priority from available records.

- [ ] The form validates required fields before submission.

- [ ] Backend validation errors are displayed clearly.

- [ ] Loading, empty, error, and success states follow existing project conventions.

- [ ] After successful ticket creation, the newly created ticket can be viewed.

- [ ] The UI does not ask the user to manually create an Interaction History entry for the ticket.

- [ ] Customer Interaction History for the ticket's customer reflects the newly created ticket activity.

### Persistence

- [ ] Ticket entity is persisted through EF Core.

- [ ] Ticket DbSet is added to CrmDbContext.

- [ ] Foreign keys to Customer, Ticket Category, and Ticket Priority are configured appropriately.

- [ ] Required fields have appropriate SQL constraints and maximum lengths.

- [ ] Appropriate indexes are added for common ticket lookups.

- [ ] EF migration is generated and applies cleanly.

- [ ] Migration does not modify unrelated tables or data.

### Verification

- [ ] Backend production code builds successfully.

- [ ] EF migration generates successfully and applies successfully.

- [ ] Ticket creation API smoke test succeeds with valid Customer/Category/Priority.

- [ ] Ticket retrieval/list API smoke tests succeed.

- [ ] Ticket update API smoke test succeeds.

- [ ] Authentication and permission smoke checks confirm 401/403 behavior.

- [ ] Validation smoke checks confirm invalid customer/category/priority and required-field handling.

- [ ] After creating a ticket, the corresponding CustomerInteraction can be retrieved through the customer Interaction History API/UI.

- [ ] The interaction has Type = `ticket`.

- [ ] No duplicate interaction is created.

- [ ] Frontend build succeeds.

- [ ] Manual end-to-end ticket creation and tracking flow succeeds.

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
  * Story 03 — Roles & Permissions
  * Story 05 — Audit Logs
  * Story 07 — Customer Profiles & Contact Details
  * Story 08 — Customer Interaction History
  * Story 10 — Ticket Categories & Priorities
  * Existing EF Core / SQL Server infrastructure
  * Existing React customer feature and permission patterns

Story 10 must be completed before Story 11 because tickets reference Ticket Categories and Ticket Priorities.

Story 08 must remain available because Story 11 creates the automatic CustomerInteraction entry consumed by Interaction History.

Story 14 is related to Story 11 because Ticket History must later record the successful Ticket creation event. However, Story 14 must not duplicate the CustomerInteraction behavior owned by Story 11.

---

## Extra notes

* Keep implementation simple and consistent with the existing CRM architecture.
* Do not introduce CQRS, repositories, message brokers, microservices, or other new architecture unless the existing project already uses them.
* Do not create unit tests.
* Verification must use build checks, migration checks, API smoke tests, frontend build, manual end-to-end testing, and manual regression checks.
* Do not seed fake ticket activity merely to demonstrate Interaction History.
* Creating a real ticket through the application must be sufficient to produce the Interaction History entry.
* Do not add generic manual CustomerInteraction CRUD.
* Do not modify Story 08's existing Interaction History semantics.
* The automatic interaction is an application/domain side effect of successful Ticket creation.
* The interaction must be associated with the same Customer as the Ticket.
* Avoid creating both a manual and automatic interaction for the same ticket creation.
* Story 11 owns the automatic Ticket → CustomerInteraction integration.
* Story 14 owns Ticket History and must not recreate or duplicate the CustomerInteraction integration.

### Critical business rule

A successful Ticket creation must result in the following distinct business/system records:

```text
Create Ticket
     │
     ├── Ticket
     │
     ├── CustomerInteraction
     │       └── Type = "ticket"
     │              └── Customer Interaction History
     │
     └── AuditLog
             └── System/User Audit
```

Ticket History is a separate responsibility owned by Story 14.

When Story 14 is implemented, the complete conceptual result of Ticket creation is:

```text
Create Ticket
     │
     ├── Save Ticket
     │
     ├── CustomerInteraction
     │       └── Type = "ticket"
     │              └── Customer Interaction History
     │
     ├── TicketHistory
     │       └── "Ticket Created"
     │              └── Ticket History
     │
     └── AuditLog
             └── System/User Audit
```

The important ownership boundary is:

```text
Story 11
    → Creates the Ticket
    → Creates exactly one CustomerInteraction for Ticket creation
    → Creates AuditLog according to existing audit conventions

Story 14
    → Creates TicketHistory for the Ticket lifecycle
    → Does NOT create another CustomerInteraction
    → Does NOT replace or modify the Story 11 CustomerInteraction semantics
    → Does NOT use CustomerInteraction as TicketHistory
    → Does NOT use AuditLog as TicketHistory
```

These three concepts must remain separate:

```text
CustomerInteraction
    → What happened with the customer?

TicketHistory
    → What happened to this specific ticket?

AuditLog
    → What did the user/system do?
```

The Ticket creation flow must not produce two CustomerInteraction records merely because Ticket History is also being recorded.

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
* Customer: Story 07 implementation
* CustomerInteraction: Story 08 implementation
* Ticket Category / Priority: Story 10 implementation
* Ticket History: Story 14 implementation

The planner must inspect the actual existing `CustomerInteraction` entity, DTOs, service, and database configuration from Story 08 before implementing the integration.

The planner must also inspect the actual Ticket Category and Ticket Priority entities created by Story 10 before finalizing Ticket foreign keys and DTOs.

The planner must inspect the existing ticket-related permissions in `Permissions.cs` and reuse them if they already exist.

Potential permission slugs should follow the existing `Permissions.cs` convention, for example:

```text
tickets.view
tickets.create
tickets.manage
```

Do not create duplicate permission constants if equivalent permissions already exist.

Potential API structure:

```text
GET    /api/tickets
GET    /api/tickets/{id}
POST   /api/tickets
PUT    /api/tickets/{id}
```

The planner must inspect existing controller routing conventions before finalizing the exact routes.

The planner must preserve a clean integration boundary for Story 14 Ticket History.

Story 11 should expose or structure its successful Ticket creation flow so Story 14 can attach the corresponding `TicketHistory` creation without reimplementing Ticket creation or creating a duplicate `CustomerInteraction`.

---

## Out of scope

* Ticket Categories & Priorities management — Story 10
* Ticket Assignment — Story 12
* Advanced Ticket Status workflow — Story 13
* Ticket Escalation — Story 13
* Ticket History — Story 14
* SLA configuration and automation
* Automatic ticket assignment
* Notifications
* Email integration
* SMS integration
* WhatsApp integration
* Live Chat
* Customer Portal
* AI features
* Reports
* Knowledge Base
* Manual CustomerInteraction CRUD
* CustomerInteraction editing/deletion UI
* Generic manual "Log Interaction" feature
* Automatic interactions for Email/SMS/WhatsApp/Meetings/Calls
* Changes to Story 08 Interaction History other than consuming its existing CustomerInteraction model/service
* Customer Notes & Attachments
* Unit tests
* Frontend component tests
* Backend integration tests
* Regression test-suite execution

```
```
