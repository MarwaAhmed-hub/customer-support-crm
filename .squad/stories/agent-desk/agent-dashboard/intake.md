# Story intake

* Folder: `.squad/stories/agent-desk/agent-dashboard/intake.md`

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
Agent Dashboard
```

## Description

```text
Build the Agent Dashboard for support agents.

This story covers ONLY the agent's dashboard view for:
1. Assigned tickets.
2. Customer information related to the agent's assigned tickets.

The dashboard should help an Agent quickly see the tickets currently assigned to them and access the relevant customer information needed to work on those tickets.

The implementation should use the existing authentication, authorization, users, customers, and ticket functionality already implemented in previous stories.

The dashboard must respect the current user's permissions and must not expose tickets that are not accessible to the current Agent according to the existing authorization rules.

This story is about the dashboard/read experience and should not introduce automatic ticket assignment or new ticket assignment rules.
```

## Acceptance criteria

```text
- [ ] Agent can open the Agent Dashboard.
- [ ] Dashboard shows tickets assigned to the currently logged-in Agent.
- [ ] Each assigned ticket shows useful ticket information such as subject, status, priority, category, customer, and created date.
- [ ] Agent can open/navigate to the ticket details from the dashboard.
- [ ] Dashboard provides access to the customer information associated with an assigned ticket.
- [ ] Customer information includes the existing customer profile/contact details available from Customer Management.
- [ ] Agent cannot see unrelated/unassigned tickets through the Agent Dashboard.
- [ ] Dashboard respects the existing authorization/permission model.
- [ ] Dashboard handles the empty state when the Agent has no assigned tickets.
- [ ] Existing ticket and customer functionality is not broken.
- [ ] Audit behavior is not changed unnecessarily by simply viewing dashboard data.
```

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| None                           |            |

## Dependencies

* **Blocked by / related ids:** None
* **Depends on code areas or other stories:**

  * Story 02 — Users Management
  * Story 03 — Roles & Permissions
  * Story 07 — Customer Profiles & Contact Details
  * Story 11 — Ticket Creation & Tracking
  * Story 12 — Ticket Assignment

## Extra notes

```text
Agent Dashboard is the first story in the Agent Desk feature.

The Agent Desk feature is split as follows:

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

Keep these boundaries strict. Do not implement functionality belonging to Stories 16–18 early.
```

## Technical hints

* Existing ticket APIs and ticket detail screen should be reused where appropriate.
* Existing customer APIs and customer detail screen should be reused where appropriate.
* Primary language: `C#`
* Frontend: React + TypeScript
* Backend: ASP.NET Core Web API + EF Core
* Repos/roots: `.`

## Out of scope

* Automatic ticket assignment — Story 23.
* Automatic reassignment/routing — Story 23.
* SLA-based assignment — Story 22/23 as applicable.
* Tasks — Story 16.
* Reminders — Story 16.
* Quick replies — Story 17.
* Team collaboration — Story 18.
* Notifications/alerts — Story 25.
* Automatic escalation rules — Story 24.
* New ticket status/escalation behavior — Story 13.
* New ticket assignment functionality — Story 12/23.
* Reports or management dashboards — Stories 34–38.
* Customer portal functionality — Stories 29–33.
