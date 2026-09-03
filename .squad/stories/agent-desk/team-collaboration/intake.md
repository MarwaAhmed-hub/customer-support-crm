# Story intake

* Folder: `.squad/stories/agent-desk/team-collaboration/intake.md`

## Feature

* **Feature name (display):** Agent Desk
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
Team Collaboration
```

## Description

```text
Implement Team Collaboration for support Agents and Managers working on Tickets.

The goal is to allow support team members to collaborate on a Ticket without changing the Ticket's Assignee automatically.

The feature should allow authorized team members to:
1. Add an internal comment/note to a Ticket for other support team members.
2. View the internal collaboration comments on the Ticket.
3. See who added each internal comment and when.
4. Reply to or add additional internal collaboration comments when appropriate.
5. Keep internal collaboration content separate from customer-facing responses.

Internal collaboration comments are visible only to authorized CRM staff and must never be exposed to the Customer.

The feature should follow the existing authentication, authorization, audit, and Ticket access rules.

Collaboration must not automatically reassign the Ticket or change its Status.
```

## Acceptance criteria

```text
- [ ] Authorized Agents/Managers can add an internal collaboration comment to a Ticket.
- [ ] Authorized team members can view internal collaboration comments.
- [ ] Each comment shows the author and creation date/time.
- [ ] Team members can add additional comments to continue the internal discussion.
- [ ] Internal comments are clearly distinguished from customer-facing responses.
- [ ] Internal comments are not visible to the Customer.
- [ ] Unauthorized users cannot access internal collaboration content.
- [ ] Adding a collaboration comment does not change the Ticket Status.
- [ ] Adding a collaboration comment does not change the Ticket Assignee.
- [ ] Collaboration actions follow the existing authorization model.
- [ ] Collaboration actions are recorded in Audit Logs according to existing audit conventions.
- [ ] Empty state is handled when a Ticket has no collaboration comments.
- [ ] Existing Ticket, Customer, Assignment, Status, and Escalation functionality is not broken.
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
  * Story 12 — Ticket Assignment
  * Story 13 — Ticket Status & Escalation
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

Story 18 — Team Collaboration
    → Internal Ticket collaboration
    → Internal comments visible to authorized CRM staff
    → Customer must NOT see internal comments

Keep these boundaries strict.

Team Collaboration is NOT customer communication.
It is internal CRM collaboration between Agents/Managers.
```

## Technical hints

* Reuse the existing Ticket detail page where appropriate.
* Follow existing authentication, authorization, validation, and audit conventions.
* Keep internal collaboration data separate from customer-facing Ticket communication.
* Primary language: `C#`
* Frontend: React + TypeScript
* Backend: ASP.NET Core Web API + EF Core
* Repos/roots: `.`

## Out of scope

* Customer-facing messages/responses.
* Email — Story 19.
* Web Forms — Story 19.
* WhatsApp/SMS — Story 20.
* Live Chat — Story 21.
* Notifications/Alerts — Story 25.
* Automatic ticket assignment/reassignment — Story 23.
* Escalation rules — Story 24.
* SLA functionality — Story 22.
* Quick Replies — Story 17.
* Tasks & Reminders — Story 16.
* AI Suggested Replies — Story 40.
* Automatic Status changes.
* Automatic Assignee changes.
* Customer Portal collaboration.
