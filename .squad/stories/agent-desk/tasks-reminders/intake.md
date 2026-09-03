# Story intake

* Folder: `.squad/stories/agent-desk/tasks-reminders/intake.md`

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
Tasks & Reminders
```

## Description

```text
Implement Tasks & Reminders for support agents.

An Agent should be able to create and manage personal work tasks related to their support work and set reminders for those tasks.

The feature should allow the Agent to:
1. Create a task.
2. View their tasks.
3. Edit a task.
4. Mark a task as completed.
5. Delete a task when appropriate.
6. Set a reminder date/time for a task.
7. View upcoming and overdue reminders.
8. Clearly distinguish completed, pending, upcoming, and overdue tasks/reminders.

Tasks and reminders should be associated with the current Agent where applicable and must respect the existing authentication and authorization model.

Keep the implementation simple and consistent with the existing CRM architecture.
```

## Acceptance criteria

```text
- [ ] Agent can open the Tasks & Reminders section.
- [ ] Agent can create a task with a title and required task information.
- [ ] Agent can view their tasks.
- [ ] Agent can edit an existing task.
- [ ] Agent can mark a task as completed.
- [ ] Agent can delete a task when permitted.
- [ ] Agent can set a reminder date/time for a task.
- [ ] Upcoming reminders are clearly identifiable.
- [ ] Overdue reminders are clearly identifiable.
- [ ] Completed tasks are clearly distinguishable from pending tasks.
- [ ] Tasks belong to the appropriate Agent and are protected by authorization.
- [ ] Empty states are handled when there are no tasks or reminders.
- [ ] Invalid required task/reminder data is rejected with clear validation.
- [ ] Existing Agent Dashboard, Ticket, and Customer functionality continues to work.
```

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| None                           |            |

## Dependencies

* **Blocked by / related ids:** None
* **Depends on code areas or other stories:**

  * Story 01 — Authentication & Login
  * Story 02 — Users Management
  * Story 03 — Roles & Permissions
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
    → Quick replies

Story 18 — Team Collaboration
    → Team collaboration

Keep these boundaries strict.
Tasks and reminders should not be mixed with ticket assignment, quick replies,
or team collaboration.
```

## Technical hints

* Reuse the existing authentication/current-user mechanism.
* Follow existing CRUD, validation, authorization, audit, and UI conventions.
* Primary language: `C#`
* Frontend: React + TypeScript
* Backend: ASP.NET Core Web API + EF Core
* Repos/roots: `.`

## Out of scope

* Assigned tickets dashboard functionality — Story 15.
* Customer information/dashboard functionality — Story 15.
* Automatic ticket assignment — Story 23.
* Ticket reassignment/routing — Story 12/23.
* Quick replies — Story 17.
* Team collaboration — Story 18.
* Notifications and alerts — Story 25.
* SLA functionality — Story 22.
* Automatic escalation — Story 24.
* Customer portal tasks/reminders.
* Management reporting or analytics.
