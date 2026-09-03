# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

* Folder: `.squad/stories/customer-management/customer-profiles-contact-details/intake.md`

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
Customer Profiles & Contact Details
```

---

## Description

```text
The Customer Management feature must allow authorized CRM users to create, view, update, and manage customer profiles and their contact details.

This story covers the customer profile and contact information portion of Customer Management.

Customer profiles should contain the essential information needed by support agents and other authorized users to identify and work with customers.

The system should support storing and managing customer contact details such as email address and phone number.

The customer information should be available through the CRM UI and backend APIs and should follow the project's existing authentication, authorization, validation, and CRUD patterns.

This story is part of the Customer Management feature, which also includes:
- Customer profiles
- Contact details
- Interaction history
- Notes and attachments

Only Customer Profiles and Contact Details are in scope for this story. Interaction History belongs to Story 08, and Notes & Attachments belong to Story 09.
```

---

## Acceptance criteria

```text
- [ ] Authorized users can view a list of customers.
- [ ] Authorized users can search and/or filter customers using the available customer identification or contact information.
- [ ] Authorized users can view the details of an individual customer.
- [ ] Authorized users can create a new customer profile.
- [ ] Authorized users can update an existing customer profile.
- [ ] Customer profiles contain the essential customer identification information required by the CRM.
- [ ] Customer contact details include, at minimum, email address and phone number where applicable.
- [ ] Customer email addresses are validated using the project's existing validation conventions.
- [ ] Customer phone numbers are validated according to the project's existing conventions.
- [ ] Required customer fields cannot be saved when empty or invalid.
- [ ] The system prevents or appropriately handles duplicate customer records only when an existing project/domain rule or established pattern defines how duplicates are identified and handled.
- [ ] Customer data is persisted in SQL Server through EF Core following the existing project architecture.
- [ ] Customer APIs require authentication and enforce the appropriate permissions.
- [ ] Users without the required permissions cannot perform protected customer-management operations.
- [ ] The frontend provides screens/forms for customer listing, viewing, creating, and editing.
- [ ] API errors and validation errors are displayed using the existing frontend conventions.
- [ ] Existing authentication, authorization, UI layout, and previously implemented features continue to work without regression.
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

  * Story 01 — Authentication & Login
  * Story 02 — Users Management
  * Story 03 — Roles & Permissions
  * Existing backend API, EF Core, SQL Server, and frontend architecture
  * Story 04 — Departments & Branches may provide existing patterns for assigning/associating organizational data if applicable to customers

## Extra notes (optional)

* Customer Management is the first feature in Cycle 1.
* Keep the implementation consistent with the existing CRM architecture and patterns rather than introducing a new architectural approach.
* Prefer simple, maintainable CRUD behavior suitable for the current CRM scope.
* Do not implement functionality belonging to later Customer Management stories.
* The planner should inspect the existing repository patterns before deciding exact entity fields, API routes, permissions, DTOs, and UI structure.
* Do not assume fields or business rules that are not supported by the project requirements or existing codebase.

## Technical hints (optional)

* APIs, screens, services already discussed. Repos/roots: `.`. Primary language: `C#`.
* Backend: ASP.NET Core Web API, EF Core, SQL Server.
* Frontend: React + TypeScript.
* Follow existing authentication and permission patterns.
* Follow existing CRUD/service/controller/DTO patterns already used by Departments, Branches, Users, and other implemented features.
* Reuse existing frontend API, form, routing, validation, and UI conventions.
* The planner should inspect the existing repository before implementation planning.

## Out of scope

* Interaction history — covered by Story 08.
* Customer notes — covered by Story 09.
* Customer attachments/files — covered by Story 09.
* Ticket creation, tracking, assignment, status, escalation, and ticket history — covered by Ticket Management.
* Email, WhatsApp, SMS, live chat, and other communication-channel implementation — covered by Communication Channels.
* Customer Portal functionality.
* Customer satisfaction/reporting functionality.
* AI features related to customers or tickets.
* Multi-tenant customer isolation unless explicitly required by the existing project architecture.
* Unspecified customer fields or business rules that are not supported by the project requirements.
