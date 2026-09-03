# Story intake

* Folder: `.squad/stories/agent-desk/quick-replies/intake.md`

## Feature

* **Feature name (display):** Agent Dashboard
* **Feature slug (folder under `plans/`):** `agent-desk`

## Tracker (metadata only)

* **Tracker type:** `none`
* **Work item id:** ``
* **Work item type:** ``
* **Status:** ``
* **Assignee:** ``
* **Labels:** ``

## Title

```text
Quick Replies
```

## Description

```text
Implement Quick Replies for support Agents.

Quick Replies are reusable response templates that help an Agent respond to common customer questions quickly and consistently.

The Agent should be able to:
1. View available quick replies.
2. Search or filter quick replies when needed.
3. Select a quick reply while working on a Ticket.
4. Insert the selected reply into the appropriate ticket/customer response input so the Agent can review or edit it before sending.

Quick replies should contain reusable text and a clear title/name.

The implementation should follow the existing authentication and authorization model and remain simple and consistent with the existing CRM architecture.

Quick Replies are templates only. They do not send messages automatically.
```

## Acceptance criteria

```text
- [ ] Agent can access the Quick Replies functionality.
- [ ] Agent can view available quick replies.
- [ ] Each quick reply has a clear title/name and reusable response text.
- [ ] Agent can search/filter quick replies.
- [ ] Agent can select a quick reply while working on a Ticket.
- [ ] Selecting a quick reply inserts its text into the appropriate response/composer field.
- [ ] Agent can edit the inserted text before sending.
- [ ] Selecting a quick reply does not automatically send a message.
- [ ] Empty state is handled when no quick replies are available.
- [ ] Quick reply access respects the existing authorization model.
- [ ] Existing Ticket and Agent Dashboard functionality is not broken.
```

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| None                           |            |

## Dependencies

* **Blocked by / related ids:** None
* **Depends on code areas or other stories:**

  * Story 01 — Authentication & Login
  * Story 03 — Roles & Permissions
  * Story 11 — Ticket Creation & Tracking
  * Story 15 — Agent Dashboard

## Extra notes

```text
Agent Desk boundaries:

Story 15 — Agent Dashboard
    → Assigned tickets
    → Customer information

Story 16 — Tasks & Reminders
    → Tasks
    → Reminders

Story 17 — Quick Replies
    → Reusable response templates
    → Insert template into ticket response/composer
    → Agent can edit before sending

Story 18 — Team Collaboration
    → Team collaboration

Quick Replies are templates only.
They must NOT automatically send Email, WhatsApp, SMS, or Live Chat messages.
Keep Stories 16 and 18 separate from this story.
```

## Technical hints

* Reuse the existing Ticket detail/composer UI where applicable.
* Follow existing authentication, authorization, CRUD, validation, and audit conventions.
* Primary language: `C#`
* Frontend: React + TypeScript
* Backend: ASP.NET Core Web API + EF Core
* Repos/roots: `.`

## Out of scope

* Automatic message sending.
* Email integration — Story 19.
* WhatsApp/SMS integration — Story 20.
* Live Chat — Story 21.
* AI Suggested Replies — Story 40.
* Tasks & Reminders — Story 16.
* Team Collaboration — Story 18.
* Notifications and Alerts — Story 25.
* Automatic ticket assignment — Story 23.
* Escalation rules — Story 24.
* Customer Portal functionality.
* Management reporting.
