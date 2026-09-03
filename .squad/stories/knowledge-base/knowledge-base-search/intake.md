# Story intake

* Folder: `.squad/stories/knowledge-base/knowledge-base-search/intake.md`

* Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.

* Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

---

## Feature

* **Feature name (display):** Knowledge Base
* **Feature slug (folder under `plans/`):** `knowledge-base`

## Tracker (metadata only)

* **Tracker type:** `none`
* **Work item id:** ``
* **Work item type:** `Story`
* **Status:** ``
* **Assignee:** ``
* **Labels:** `knowledge-base, search`

External tracker links are not followed by the planner. Keep the id for naming and traceability only.

---

## Title

```text id="n4v3fz"
Knowledge Base Search
```

---

## Description

```text id="e0w3cy"
Implement Knowledge Base search functionality that allows authorized users to find published Knowledge Base content.

The Knowledge Base contains multiple content types created and managed through Stories 26 and 27:

- FAQs
- Help Articles
- Solutions
- Guides

The existing system roles are:

- Administrator
- Manager
- Agent
- Customer

Search permissions:

- Administrator: can search Knowledge Base content available to internal users.
- Manager: can search Knowledge Base content available to internal users.
- Agent: can search published Knowledge Base content available to internal users.
- Customer: can search published customer-facing Knowledge Base content only.

The search must respect the visibility/audience of the Knowledge Base content.

Customer-facing content can be returned to Customers.

Internal-only content must never be returned to Customers.

Internal users (Administrator, Manager, and Agent) can search content available to internal users, including published internal-only content and published customer-facing content.

Only Published Knowledge Base content should appear in normal search results.

Draft content must not appear in search results.

The search should support searching across the available Knowledge Base content types.

A search query may match relevant content using:
- FAQ question and answer
- Help Article title and content
- Solution title, problem/issue, and solution
- Guide title, description/introduction, and instructions/steps

Example:

An Agent searches for:

"Internet connection"

The system may return:

1. Internet Connection Troubleshooting
   Type: Solution
   Category: Technical Support

2. Internet Connection FAQ
   Type: FAQ
   Category: Technical Support

3. Router Troubleshooting Guide
   Type: Guide
   Category: Technical Support

The search results should provide enough information for the user to identify the relevant item, such as:
- Title or question
- Content type
- Category
- Short relevant excerpt/summary where applicable

The user can select a search result to open the full Knowledge Base item, subject to the same visibility and authorization rules.

Search should support filtering by Knowledge Base content type where applicable:
- FAQ
- Help Article
- Solution
- Guide

Search should also support filtering by Category where applicable.

If the search query is empty, the system may return no results or provide the default Knowledge Base browsing behavior already supported by the application. The implementation should follow the existing application UX conventions rather than introducing unnecessary behavior.

The search must not expose Draft or unauthorized content through search results, result counts, snippets, autocomplete, or other search responses.

The search functionality is shared by internal users and customers but must apply role-based and visibility-based filtering before returning results.

This story is responsible for Knowledge Base search and retrieval. Creation, editing, publishing, and visibility management belong to Stories 26 and 27.
```

---

## Acceptance criteria

```text id="q3f5im"
- [ ] Administrator can search published Knowledge Base content available to internal users.

- [ ] Manager can search published Knowledge Base content available to internal users.

- [ ] Agent can search published Knowledge Base content available to internal users.

- [ ] Customer can search published customer-facing Knowledge Base content.

- [ ] Search can return published FAQs.

- [ ] Search can return published Help Articles.

- [ ] Search can return published Solutions.

- [ ] Search can return published Guides.

- [ ] Draft Knowledge Base content does not appear in search results.

- [ ] Internal-only content never appears in Customer search results.

- [ ] Customer-facing content can appear in Customer search results.

- [ ] Internal users can search content available to internal users, including published internal-only content.

- [ ] Search respects the visibility/audience configured for each Knowledge Base item.

- [ ] Search does not expose unauthorized content through:
      - Search results
      - Result counts
      - Search snippets
      - Autocomplete/suggestions, if implemented

- [ ] Search can match relevant content in FAQ question and answer.

- [ ] Search can match relevant content in Help Article title and content.

- [ ] Search can match relevant content in Solution title, problem/issue, and solution.

- [ ] Search can match relevant content in Guide title, description/introduction, and instructions/steps.

- [ ] Search results provide enough information to identify the Knowledge Base item, including at minimum:
      - Title or question
      - Content type
      - Category

- [ ] Search results can provide a relevant content excerpt/summary where supported by the implementation.

- [ ] User can open a Knowledge Base item from a search result.

- [ ] Opening a Knowledge Base item applies the same authorization and visibility rules as search.

- [ ] User can filter search results by content type:
      - FAQ
      - Help Article
      - Solution
      - Guide

- [ ] User can filter search results by Category.

- [ ] Search results remain restricted to Published content.

- [ ] Search works across all supported Knowledge Base content types rather than requiring separate searches.

- [ ] Search does not modify Knowledge Base content.

- [ ] Search does not modify ticket data.

- [ ] Search does not introduce or modify user roles.

- [ ] Search authorization is enforced server-side.

- [ ] The search implementation consumes the Knowledge Base content created by Stories 26 and 27.
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

  * Story 26 — FAQs & Help Articles
  * Story 27 — Solutions & Guides
  * Existing authentication and authorization/role system
  * Existing Category functionality, if available
  * Existing Knowledge Base content storage/repository

---

## Extra notes (optional)

* Knowledge Base Search is available to both internal users and Customers.
* Customers must only see customer-facing published content.
* Administrator, Manager, and Agent can search published content available to internal users.
* Draft content must never be exposed through search.
* Internal-only content must never be exposed to Customers.
* Search should cover all Knowledge Base content types through one search experience.
* Search is a read-only capability.
* No new roles should be introduced.
* The search experience should follow existing application UI conventions.

## Technical hints (optional)

* APIs/screens/services involved:

  * Knowledge Base search endpoint/service
  * Knowledge Base content repository
  * Category filtering
  * Content-type filtering
  * Existing authentication/authorization
* Repository root: `.`
* Primary language: `C#`.
* Reuse the existing authorization mechanism and existing roles.
* Apply publication and visibility filtering at the data/service layer before returning search results.
* Do not rely solely on frontend filtering to hide unauthorized content.
* Search should be implemented against the existing Knowledge Base content model created by Stories 26 and 27.
* If a search abstraction already exists in the application, reuse it rather than introducing a second search mechanism.
* Keep the search implementation extensible so additional Knowledge Base content types can be supported later.

## Out of scope

* Creating FAQs, Help Articles, Solutions, or Guides
* Editing Knowledge Base content
* Publishing Knowledge Base content
* Draft/Published workflow implementation
* Knowledge Base visibility management
* AI-powered semantic search
* AI-generated answers
* Automatic article recommendations
* Automatic linking of search results to Tickets
* Automatic suggestion of Knowledge Base content while an Agent replies to a Customer
* Search analytics and reporting
* Ranking customization beyond the basic relevance behavior supported by the selected search implementation
* Introducing new user roles
