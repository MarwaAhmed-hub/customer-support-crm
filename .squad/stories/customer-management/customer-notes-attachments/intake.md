# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

* Folder: `.squad/stories/customer-management/customer-notes-attachments/intake.md`

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

```text id="p4eqwm"
Customer Notes & Attachments
```

---

## Description

```text id="s7r3up"
The Customer Management feature must allow authorized CRM users to add, view, update, and manage notes and attachments associated with a customer.

This story covers the notes and attachments portion of Customer Management.

Users should be able to record useful notes about a customer and associate relevant files with the customer's profile so that authorized support users can access important customer-related information from one place.

Notes should be associated with the correct customer and should include the relevant author and creation/update information where supported by the existing project conventions.

Attachments should be associated with the correct customer and provide the information necessary for authorized users to identify and access the uploaded file.

This story is part of the Customer Management feature, which includes:
- Customer profiles
- Contact details
- Interaction history
- Notes and attachments

Only Notes and Attachments are in scope for this story. Customer Profiles & Contact Details belong to Story 07, and Interaction History belongs to Story 08.
```

---

## Acceptance criteria

```text id="n6g2ab"
- [ ] Authorized users can view notes associated with a specific customer.
- [ ] Authorized users can create a new customer note.
- [ ] Authorized users can update an existing customer note where permitted by the project's authorization rules.
- [ ] Authorized users can delete or otherwise remove a customer note where permitted by the project's existing conventions.
- [ ] Each note is associated with the correct customer.
- [ ] Each note identifies its author where user identity is available.
- [ ] Notes expose appropriate creation/update timestamps.
- [ ] Authorized users can view attachments associated with a specific customer.
- [ ] Authorized users can upload an attachment associated with a customer.
- [ ] Authorized users can access/download an attachment they are authorized to access.
- [ ] Attachment metadata identifies the file name and relevant file information supported by the existing project conventions.
- [ ] The system validates uploaded files according to the project's existing file-upload/security conventions.
- [ ] Invalid or unsupported file uploads are rejected with a clear validation error.
- [ ] Notes and attachments cannot be associated with a non-existent customer.
- [ ] Customer notes and attachments are protected by authentication and the appropriate permissions.
- [ ] Users without the required permissions cannot access or modify protected customer notes or attachments.
- [ ] The frontend provides an appropriate notes and attachments section from the customer profile/details area.
- [ ] The UI provides appropriate loading, empty, success, and error states following existing frontend conventions.
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
  * Existing customer/profile implementation
  * Existing authentication and authorization system
  * Existing file-upload/storage implementation, if available
  * Existing backend API and EF Core persistence patterns
  * Existing frontend customer/profile UI patterns

## Extra notes (optional)

* This is Story 09 within the Customer Management feature.
* Notes and attachments must belong to a specific customer.
* Keep the implementation simple and consistent with the existing CRM architecture.
* The planner should inspect the existing repository and previous customer-management stories before deciding the exact entity structure, API routes, permissions, file-storage approach, limits, and UI design.
* Reuse existing file upload/storage infrastructure if the project already has one rather than introducing a second mechanism.
* Notes should not be treated as interaction-history records.
* Attachments should not be duplicated across unrelated customer, ticket, or interaction entities unless the existing architecture requires it.
* Do not implement functionality belonging to Story 07 or Story 08.

## Technical hints (optional)

* APIs, screens, services already discussed. Repos/roots: `.`. Primary language: `C#`.
* Backend: ASP.NET Core Web API, EF Core, SQL Server.
* Frontend: React + TypeScript.
* Follow existing authentication, authorization, controller/service/DTO, persistence, file-upload, routing, and UI conventions.
* Reuse existing customer/profile components and API patterns where appropriate.
* The planner should inspect the repository before deciding implementation details.

## Out of scope

* Creating or editing customer profiles — Story 07.
* Managing customer contact details — Story 07.
* Customer interaction history — Story 08.
* Ticket notes and ticket attachments unless explicitly shared by the existing domain architecture.
* Ticket creation, tracking, assignment, status, escalation, and ticket history.
* Email, WhatsApp, SMS, live chat, and web-form implementation.
* Customer Portal functionality.
* AI-generated notes, summaries, or suggestions.
* Reports and analytics.
* Unspecified file-storage providers or external document-management integrations.
* Unspecified note fields, attachment metadata, file types, or business rules that are not supported by the project requirements or existing codebase.
