# Story intake

* Folder: `.squad/stories/knowledge-base/faqs-help-articles/intake.md`

## Feature

* **Feature name (display):** Knowledge Base
* **Feature slug (folder under `plans/`):** `knowledge-base`

## Tracker (metadata only)

* **Tracker type:** `none`
* **Work item id:** ``
* **Work item type:** `Story`
* **Status:** ``
* **Assignee:** ``
* **Labels:** `knowledge-base, faq, help-articles`

---

## Title

```text
FAQs & Help Articles
```

---

## Description

```text
Implement the ability to create, manage, publish, and view FAQs and Help Articles in the Knowledge Base.

The Knowledge Base is available to both internal support users and customers.

The existing system roles are:

- Administrator
- Manager
- Agent
- Customer

Permissions for this story:

- Administrator: can create, edit, publish, and view FAQs and Help Articles.
- Manager: can create, edit, publish, and view FAQs and Help Articles.
- Agent: can view and search published FAQs and Help Articles, but cannot create, edit, or publish them.
- Customer: can view and search published customer-facing FAQs and Help Articles, but cannot create, edit, or publish them.

A Knowledge Base item must have enough information to be useful and discoverable.

An FAQ should contain:
- Question
- Answer
- Category
- Publication status

A Help Article should contain:
- Title
- Content
- Category
- Publication status

The content should support a Draft/Published lifecycle.

Newly created content starts as Draft and is not visible to Agents or Customers as published Knowledge Base content until it is published.

Administrator and Manager users can edit Draft and Published content according to their permissions.

Publishing makes the content available to its intended audience.

Knowledge Base content can be customer-facing or internal.

Customer-facing content can be viewed by Customers and internal users.

Internal-only content can be viewed by internal support users (Administrator, Manager, and Agent) and must not be visible to Customers.

The story should preserve the distinction between published customer-facing content and internal-only content.

Example:

A Manager creates an FAQ:

Question:
"How can I reset my password?"

Answer:
"Open Settings, select Security, and choose Change Password."

Category:
Account

Status:
Draft

The FAQ is not visible in the published Knowledge Base yet.

The Manager publishes it.

The FAQ becomes available to the intended audience.

Another example:

An Administrator creates an internal Help Article:

Title:
"Handling Account Verification Escalations"

Content:
"Internal procedure for handling account verification cases..."

Visibility:
Internal

Status:
Published

This article can be viewed by Administrator, Manager, and Agent users but must not be visible to Customers.

Customer-facing Help Articles and FAQs must not expose internal procedures, internal notes, internal escalation information, or internal operational information.

The Knowledge Base content created by this story will be consumed by Story 28 — Knowledge Base Search.

This story is responsible for FAQ and Help Article content management and access. Search implementation belongs to Story 28.
```

---

## Acceptance criteria

```text
- [ ] Administrator can create an FAQ.

- [ ] Administrator can edit an FAQ.

- [ ] Administrator can publish an FAQ.

- [ ] Administrator can view FAQs.

- [ ] Manager can create an FAQ.

- [ ] Manager can edit an FAQ.

- [ ] Manager can publish an FAQ.

- [ ] Manager can view FAQs.

- [ ] Agent can view published FAQs.

- [ ] Agent cannot create, edit, or publish FAQs.

- [ ] Customer can view published customer-facing FAQs.

- [ ] Customer cannot create, edit, or publish FAQs.

- [ ] An FAQ contains at minimum:
      - Question
      - Answer
      - Category
      - Publication status

- [ ] Administrator can create a Help Article.

- [ ] Administrator can edit a Help Article.

- [ ] Administrator can publish a Help Article.

- [ ] Administrator can view Help Articles.

- [ ] Manager can create a Help Article.

- [ ] Manager can edit a Help Article.

- [ ] Manager can publish a Help Article.

- [ ] Manager can view Help Articles.

- [ ] Agent can view published Help Articles.

- [ ] Agent cannot create, edit, or publish Help Articles.

- [ ] Customer can view published customer-facing Help Articles.

- [ ] Customer cannot create, edit, or publish Help Articles.

- [ ] A Help Article contains at minimum:
      - Title
      - Content
      - Category
      - Publication status

- [ ] Newly created FAQs and Help Articles have Draft status by default.

- [ ] Draft content is not visible as published Knowledge Base content to Agents or Customers.

- [ ] Publishing an item changes it to Published.

- [ ] Published customer-facing content is visible to Customers and internal users according to their permissions.

- [ ] Internal-only content is visible to Administrator, Manager, and Agent users.

- [ ] Internal-only content is not visible to Customers.

- [ ] Customer-facing content must not expose internal notes, internal procedures, internal escalation information, or other internal operational information.

- [ ] Category is stored with the Knowledge Base item and can be used by the Knowledge Base.

- [ ] The system must distinguish between FAQ and Help Article content types.

- [ ] The system must distinguish between customer-facing and internal-only content.

- [ ] Only Administrator and Manager roles can manage Knowledge Base content.

- [ ] Agent and Customer roles are read-only for Knowledge Base content.

- [ ] Knowledge Base content can be consumed by Story 28 — Knowledge Base Search.

- [ ] This story does not require implementation of the Knowledge Base search functionality.
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
  * Story 28 — Knowledge Base Search: consumes published Knowledge Base content.
  * Story 27 — Solutions & Guides: related Knowledge Base content type but should remain separately scoped.

---

## Extra notes (optional)

* The Knowledge Base is intended for both internal support usage and customer self-service.
* Administrator and Manager are the content owners.
* Agent and Customer are consumers/readers.
* `Draft` and `Published` are the minimum required publication states.
* Visibility must distinguish between `Customer-facing` and `Internal-only`.
* The default audience should not expose internal-only content to Customers.
* Search is handled separately by Story 28.
* Solutions and Guides are handled separately by Story 27.

## Technical hints (optional)

* APIs/screens/services involved:

  * Knowledge Base content management
  * FAQ management
  * Help Article management
  * Publication/visibility management
  * Role-based authorization
* Repository root: `.`
* Primary language: `C#`.
* Reuse the existing authorization mechanism and roles.
* Prefer a shared Knowledge Base content model where appropriate, with a content type distinguishing FAQ from Help Article.
* Store publication status and audience/visibility explicitly.
* Authorization must be enforced server-side, not only through UI visibility.
* Published content should be available to Story 28's search functionality.

## Out of scope

* Knowledge Base search implementation — Story 28
* Search ranking/relevance
* Solutions & Guides — Story 27
* AI-generated FAQs or Help Articles
* Automatic generation of Knowledge Base content from Tickets
* Automatic classification of Knowledge Base content
* Customer creation or editing of Knowledge Base content
* Agent creation or editing of Knowledge Base content
* Notifications related to Knowledge Base publishing
* Version history unless already supported by the existing application
* Approval workflow beyond the Administrator/Manager publish permission defined above
