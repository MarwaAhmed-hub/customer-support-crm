# Story intake

* Folder: `.squad/stories/knowledge-base/solutions-guides/intake.md`

## Feature

* **Feature name (display):** Knowledge Base
* **Feature slug (folder under `plans/`):** `knowledge-base`

## Tracker (metadata only)

* **Tracker type:** `none`
* **Work item id:** ``
* **Work item type:** `Story`
* **Status:** ``
* **Assignee:** ``
* **Labels:** `knowledge-base, solutions, guides`

---

## Title

```text
Solutions & Guides
```

---

## Description

```text
Implement the ability to create, manage, publish, and view Solutions and Guides in the Knowledge Base.

Solutions and Guides are structured knowledge content intended to help users solve a problem or follow a procedure.

The Knowledge Base is available to both internal support users and customers.

The existing system roles are:

- Administrator
- Manager
- Agent
- Customer

Permissions for this story:

- Administrator: can create, edit, publish, and view Solutions and Guides.
- Manager: can create, edit, publish, and view Solutions and Guides.
- Agent: can view and search published Solutions and Guides, but cannot create, edit, or publish them.
- Customer: can view and search published customer-facing Solutions and Guides, but cannot create, edit, or publish them.

A Solution or Guide should contain enough structured information to explain how to solve an issue or perform a procedure.

A Solution should contain at minimum:
- Title
- Problem/Issue
- Solution
- Category
- Publication status
- Visibility/Audience

A Guide should contain at minimum:
- Title
- Description/Introduction
- Steps or Instructions
- Category
- Publication status
- Visibility/Audience

The content should support a Draft/Published lifecycle.

Newly created Solutions and Guides start as Draft and are not available as published Knowledge Base content until they are published.

Administrator and Manager users can edit and publish the content according to their permissions.

Published content can be either:

1. Customer-facing
   - Visible to Customers.
   - Also visible to authorized internal users.

2. Internal-only
   - Visible to Administrator, Manager, and Agent.
   - Must not be visible to Customers.

Solutions and Guides are different from FAQs and Help Articles:

- FAQ provides a short question-and-answer format.
- Help Article provides general explanatory information.
- Solution focuses on solving a specific problem or issue.
- Guide provides a structured set of instructions or steps for completing a task.

Example Solution:

Title:
"Internet Connection Troubleshooting"

Problem:
"Customer cannot connect to the Internet."

Solution:
"Check the router connection, restart the router, verify the connection status, and perform the required troubleshooting steps."

Category:
Technical Support

Visibility:
Customer-facing

Status:
Draft

After publishing, the Solution becomes available to Customers and internal users.

Example internal Guide:

Title:
"Internal Guide — Handling Complex Internet Issues"

Description:
"Internal procedure for Agents handling advanced Internet connectivity cases."

Steps:
1. Review the customer's previous Tickets.
2. Verify the service status.
3. Perform the required diagnostic checks.
4. Escalate the case according to the internal procedure.

Category:
Technical Support

Visibility:
Internal-only

Status:
Published

This Guide is available to Agents, Managers, and Administrators but is not visible to Customers.

The Knowledge Base content created by this story will be consumed by Story 28 — Knowledge Base Search.

This story is responsible for creating and managing Solutions and Guides and controlling their publication and visibility. Search functionality belongs to Story 28.
```

---

## Acceptance criteria

```text
- [ ] Administrator can create a Solution.

- [ ] Administrator can edit a Solution.

- [ ] Administrator can publish a Solution.

- [ ] Administrator can view Solutions.

- [ ] Manager can create a Solution.

- [ ] Manager can edit a Solution.

- [ ] Manager can publish a Solution.

- [ ] Manager can view Solutions.

- [ ] Agent can view published Solutions.

- [ ] Agent cannot create, edit, or publish Solutions.

- [ ] Customer can view published customer-facing Solutions.

- [ ] Customer cannot create, edit, or publish Solutions.

- [ ] A Solution contains at minimum:
      - Title
      - Problem/Issue
      - Solution
      - Category
      - Publication status
      - Visibility/Audience

- [ ] Administrator can create a Guide.

- [ ] Administrator can edit a Guide.

- [ ] Administrator can publish a Guide.

- [ ] Administrator can view Guides.

- [ ] Manager can create a Guide.

- [ ] Manager can edit a Guide.

- [ ] Manager can publish a Guide.

- [ ] Manager can view Guides.

- [ ] Agent can view published Guides.

- [ ] Agent cannot create, edit, or publish Guides.

- [ ] Customer can view published customer-facing Guides.

- [ ] Customer cannot create, edit, or publish Guides.

- [ ] A Guide contains at minimum:
      - Title
      - Description/Introduction
      - Steps/Instructions
      - Category
      - Publication status
      - Visibility/Audience

- [ ] Newly created Solutions and Guides have Draft status by default.

- [ ] Draft Solutions and Guides are not visible as published Knowledge Base content to Agents or Customers.

- [ ] Administrator and Manager can publish Draft Solutions and Guides.

- [ ] Published customer-facing Solutions and Guides are visible to Customers.

- [ ] Published customer-facing Solutions and Guides are visible to authorized internal users.

- [ ] Published internal-only Solutions and Guides are visible to Administrator, Manager, and Agent.

- [ ] Published internal-only Solutions and Guides are not visible to Customers.

- [ ] The system must distinguish between Customer-facing and Internal-only content.

- [ ] Customer-facing content must not expose internal procedures, internal notes, internal escalation information, or other internal operational information.

- [ ] The system must distinguish between Solution and Guide content types.

- [ ] Category is stored with every Solution and Guide.

- [ ] Only Administrator and Manager roles can create, edit, and publish Solutions and Guides.

- [ ] Agent and Customer roles are read-only for Solutions and Guides.

- [ ] Published Solutions and Guides can be consumed by Story 28 — Knowledge Base Search.

- [ ] This story does not implement Knowledge Base search.
```

---

## Attachments

| File (relative to this folder) | What it is     |
| ------------------------------ | -------------- |
| None                           | No attachments |

---

## Dependencies

* **Blocked by / related ids:** None

* **Depends on code areas or other stories:**

  * Existing authentication and authorization/role system.
  * Existing Category functionality, if available.
  * Existing user/role model.
  * Story 26 — FAQs & Help Articles: related Knowledge Base content and shared publication/visibility concepts.
  * Story 28 — Knowledge Base Search: consumes published Solutions and Guides.

---

## Extra notes (optional)

* Solutions and Guides are Knowledge Base content, but they should remain distinguishable from FAQs and Help Articles.
* Administrator and Manager are the content owners.
* Agent and Customer are consumers/readers.
* `Draft` and `Published` are the minimum required publication states.
* Visibility must distinguish between `Customer-facing` and `Internal-only`.
* Customer-facing content can be used for customer self-service.
* Internal-only content is intended for internal support operations.
* Search is handled separately by Story 28.
* This story should not introduce new roles.

## Technical hints (optional)

* APIs/screens/services involved:

  * Knowledge Base content management
  * Solution management
  * Guide management
  * Publication/visibility management
  * Role-based authorization
* Repository root: `.`
* Primary language: `C#`.
* Reuse the existing authorization mechanism and existing roles.
* Prefer reusing the Knowledge Base content structure established by Story 26 where appropriate.
* Content type should distinguish Solution from Guide.
* Store publication status and visibility/audience explicitly.
* Authorization must be enforced server-side, not only through UI visibility.
* Published content should be available to Story 28's search functionality.
* Guide steps should preserve their order.

## Out of scope

* Knowledge Base search implementation — Story 28
* Search ranking/relevance
* FAQs & Help Articles — Story 26
* AI-generated Solutions or Guides
* Automatic generation of Solutions or Guides from Tickets
* Automatic classification of Solutions or Guides
* Customer creation or editing of Solutions/Guides
* Agent creation or editing of Solutions/Guides
* Notifications related to publishing
* Approval workflow beyond Administrator/Manager publishing permissions
* Automatic linking of Solutions/Guides to Tickets
* Automatic recommendation of Solutions/Guides to Agents or Customers
