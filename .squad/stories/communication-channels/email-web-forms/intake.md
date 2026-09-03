# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

* Folder: `.squad/stories/communication-channels/email-web-forms/intake.md`

* Binaries (screenshots, PDFs, exports): put them in `attachments/` next to this file and list them below.

* Do **not** rely on external links (tracker URLs, wiki, chat) — the planner cannot open them. Paste the content you want considered.

This is **not** an implementation prompt. It is the input to the plan-generation meta-prompt bundled with squad-kit (`generate-plan.md` in the installed package).

---

## Feature

* **Feature name (display):** Communication Channels
* **Feature slug (folder under `plans/`):** `communication-channels`

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
Email & Web Forms
```

---

## Description

```text
Implement the Email and Web Form communication channels for the Customer Support CRM.

The goal is to allow customers to contact the CRM through Email or a public Web Form and have those incoming requests enter the normal CRM Ticket workflow.

Supported flows:

1. Email — Incoming Customer Request

- Receive an incoming customer email.
- Identify the customer using the available email address.
- If the customer already exists, link the incoming request to that customer.
- If the customer does not exist, create a new customer using the available contact information.
- Create a new Ticket for a new support request.
- If the incoming message clearly belongs to an existing Ticket/conversation, link/update the existing Ticket instead of creating an unnecessary duplicate Ticket.
- Store the incoming communication as a CustomerInteraction.
- Preserve the original message content and relevant metadata such as sender, recipient, subject, timestamp, and channel.
- Make the resulting Ticket visible through the existing Ticket Management and Agent Desk flows.

2. Web Form — Incoming Customer Request

- Provide a customer-facing Web Form for submitting a support request.
- The form collects the minimum information required to identify/contact the customer and create a support request, such as name, email/contact information, subject, and description.
- Validate required fields.
- Find an existing customer by the submitted contact information or create a new customer when appropriate.
- Create a Ticket from the submitted request.
- Store the submission as a CustomerInteraction with channel = Web Form.
- Make the resulting Ticket visible through the existing Ticket Management and Agent Desk flows.

3. Agent Reply — Email Tickets

When an Agent opens a Ticket that came through Email:

- The Agent can see the Customer and Ticket information.
- The Agent can compose a reply using the existing reply composer.
- The Agent can type the reply manually.
- The Agent can use Quick Replies from Story 17 to insert reusable text into the reply composer.
- Selecting a Quick Reply only inserts its text into the composer. It does not send the message automatically.
- The Agent can edit the inserted Quick Reply text before sending.
- The Agent explicitly sends the final reply.
- The final reply is sent to the customer through the Email channel.
- The sent reply is persisted and associated with the existing Customer and Ticket.
- Sending a reply does not create a new Ticket for the same conversation.
- The Email communication history remains associated with the existing Ticket and Customer.

Story 17 owns Quick Reply template management and insertion behavior.

This story owns the actual Email communication and sending of the final Agent reply.

4. Web Form Reply Behavior

- Web Form is an inbound customer communication channel.
- A Web Form submission creates or links a Customer and creates a Ticket.
- The Agent can work on the resulting Ticket through Agent Desk.
- Web Form does not require a separate Web Form-specific outbound reply mechanism.
- If the Agent needs to contact the customer after a Web Form submission, the response uses an available supported customer contact channel, such as Email, according to the communication data available for the Ticket.

5. Common CRM behavior

- Email and Web Form requests must enter the same core Ticket workflow already established by Ticket Management.
- Ticket creation must respect the existing Customer, Ticket, and CustomerInteraction domain ownership.
- Story 11 owns CustomerInteraction creation for manually/internal-created tickets.
- This story owns CustomerInteraction creation for Email and Web Form communications.
- Story 14 (Ticket History) must not create CustomerInteraction records.
- The communication channel must be identifiable on the CustomerInteraction and/or related communication record.
- The implementation must avoid creating duplicate customers when the same customer contacts the CRM repeatedly.
- The implementation must avoid creating duplicate tickets when an incoming email belongs to an existing Ticket.
- The implementation should use abstractions for email receiving and sending so the concrete email provider can be configured independently.
- Web Form submission must be protected against invalid input and obvious abuse/spam scenarios using the project's existing security/validation conventions.

The resulting Email flow is:

Customer
  -> Email
  -> Receive Email
  -> Find or Create Customer
  -> Create or Link Ticket
  -> Create CustomerInteraction
  -> Ticket appears in Agent Desk
  -> Agent opens Ticket
  -> Compose Reply
  -> Optional: Insert Quick Reply from Story 17
  -> Agent reviews/edits reply
  -> Agent explicitly sends reply
  -> Send Email to Customer
  -> Persist outgoing communication
  -> Continue existing Ticket

The resulting Web Form flow is:

Customer
  -> Web Form
  -> Submit Request
  -> Find or Create Customer
  -> Create Ticket
  -> Create CustomerInteraction
  -> Ticket appears in Agent Desk
  -> Agent works on Ticket

This story does not implement automatic agent assignment. Automatic assignment belongs to Story 23.
```

---

## Acceptance criteria

```text
### Email — Incoming Communication

- [ ] The system can receive an incoming email through an application-level email channel abstraction.
- [ ] The sender email address is captured.
- [ ] The email subject and body are preserved.
- [ ] The received timestamp and channel are preserved.
- [ ] If the sender matches an existing customer, the request is linked to that customer.
- [ ] If the sender does not match an existing customer, a customer can be created using the available information.
- [ ] A new support request creates a Ticket.
- [ ] The created Ticket contains the appropriate subject/description derived from the incoming email.
- [ ] The incoming email creates exactly one CustomerInteraction associated with the correct customer/ticket.
- [ ] The CustomerInteraction identifies Email as its communication channel.
- [ ] A reply or follow-up email that can be reliably matched to an existing Ticket updates/links the existing Ticket instead of creating an unnecessary duplicate Ticket.
- [ ] The original email content and relevant metadata remain available to authorized CRM users.
- [ ] Email-created Tickets are available through the existing Ticket Management and Agent Desk flows.

### Email — Agent Reply

- [ ] An Agent can open an Email-created Ticket through Agent Desk.
- [ ] The Agent can compose a reply using the Ticket reply composer.
- [ ] The Agent can type a reply manually.
- [ ] The Agent can use Quick Replies from Story 17 to insert reusable text into the reply composer.
- [ ] Selecting a Quick Reply inserts its text into the composer only.
- [ ] Selecting a Quick Reply does not automatically send the message.
- [ ] The Agent can edit the inserted Quick Reply text before sending.
- [ ] The Agent must explicitly trigger the Send action.
- [ ] The Send action sends the final reply through Email.
- [ ] The outgoing Email is associated with the existing Customer and Ticket.
- [ ] Sending the reply does not create a new Ticket for the existing conversation.
- [ ] The outgoing communication is persisted according to the existing communication/history model.
- [ ] The Agent can continue working on the same Ticket after sending the reply.

### Web Form

- [ ] A customer can submit a support request through the Web Form without being an authenticated CRM Agent.
- [ ] Required fields are validated.
- [ ] Invalid submissions are rejected with useful validation feedback.
- [ ] An existing customer is reused when the submitted contact information matches an existing customer.
- [ ] A new customer can be created when no matching customer exists.
- [ ] A Ticket is created from a valid Web Form submission.
- [ ] The Ticket contains the submitted subject and description.
- [ ] Exactly one CustomerInteraction is created for the submission.
- [ ] The CustomerInteraction identifies Web Form as the communication channel.
- [ ] The resulting Ticket is visible through the existing CRM Ticket/Agent Desk flows.
- [ ] Web Form does not require a separate outbound Web Form reply mechanism.

### CustomerInteraction ownership

- [ ] Email incoming communication creates its own CustomerInteraction.
- [ ] Email outgoing Agent communication is persisted using the existing communication/history model.
- [ ] Web Form communication creates its own CustomerInteraction.
- [ ] Story 14 Ticket History is not used to create CustomerInteraction records.
- [ ] Existing CustomerInteraction records created by this story are not duplicated by Ticket History.

### Quick Reply integration

- [ ] Story 17 remains responsible for Quick Reply template management.
- [ ] Story 17 remains responsible for inserting Quick Reply text into the composer.
- [ ] This story consumes the Quick Reply functionality when composing an Email reply.
- [ ] Quick Reply does not send Email automatically.
- [ ] The Agent must explicitly send the final Email reply.
- [ ] The final Email reply uses the Email communication channel.

### Integration with existing CRM

- [ ] Email/Web Form Ticket creation reuses the existing Ticket Management domain/services where appropriate rather than creating a parallel Ticket model.
- [ ] Created Tickets remain compatible with Story 12 manual assignment.
- [ ] Created Tickets can later be handled by Story 23 Automatic Ticket Assignment without requiring a different Ticket model.
- [ ] Created Tickets can appear in the Agent Dashboard through the existing Agent Desk implementation.
- [ ] Existing Ticket History can record subsequent Ticket changes without taking ownership of CustomerInteraction creation.
- [ ] Email reply functionality does not create a separate Ticket workflow.

### Error handling

- [ ] Invalid email payloads are rejected/handled safely.
- [ ] Invalid Web Form submissions do not create partial Customer/Ticket/Interaction data.
- [ ] Failures do not result in duplicate Tickets or duplicate CustomerInteraction records.
- [ ] Failed outgoing Email does not falsely appear as successfully delivered.
- [ ] The implementation follows the existing project's logging and error-handling conventions.
```

---

## Attachments

| File (relative to this folder) | What it is                               |
| ------------------------------ | ---------------------------------------- |
| None                           | No binary attachments currently provided |

---

## Dependencies

* **Blocked by / related ids:** None

* **Depends on code areas or other stories:**

  * Customer Management — Stories 07–09: Customer and CustomerInteraction models/services.
  * Ticket Management — Stories 10–14: Ticket creation, assignment, status and history.
  * Agent Desk — Story 15: Created Tickets must be visible to Agents.
  * Story 17 — Quick Replies: provides reusable response templates that can be inserted into the Email reply composer. Story 19 remains responsible for sending the final Email reply.
  * Story 23 — Automatic Ticket Assignment: future integration point only; do not implement automatic assignment in this story.

---

## Extra notes (optional)

* Email/Web Form are external communication entry points into the existing CRM Ticket workflow.

* Do not create a second/parallel Ticket workflow specifically for communication channels.

* CustomerInteraction ownership must remain explicit:

  * Story 11 → internally/manual-created Ticket interaction.
  * Story 19 → Email/Web Form communication interaction.
  * Story 20 → WhatsApp/SMS communication interaction.
  * Story 21 → Live Chat communication interaction.
  * Story 14 → TicketHistory only.

* Story 17 owns Quick Reply template management and insertion into the Agent reply composer.

* Story 19 owns the Email channel and the actual sending of the final Agent Email reply.

* A Quick Reply is never automatically sent.

* The Agent must review/edit the reply and explicitly press Send.

* Automatic agent selection is intentionally deferred to Story 23.

---

## Technical hints (optional)

* APIs, screens, services already discussed.
* Reuse existing Customer, CustomerInteraction, Ticket, TicketHistory and Agent Desk abstractions where available.
* Use application-level abstractions/interfaces for Email receiving and sending rather than coupling the domain/application layer to a specific Email provider.
* Public Web Form should expose only the minimum required API surface.
* Reuse the existing Ticket reply/composer UI.
* Reuse Story 17 Quick Reply functionality; do not create a second Quick Reply implementation.
* Persist outgoing Email communication.
* Do not mark an Email as successfully sent when the underlying Email provider reports failure.
* Primary language: `C#`.
* Frontend: React + TypeScript.
* Backend: ASP.NET Core Web API.
* Repos/roots: `.`

---

## Out of scope

* Automatic Ticket Assignment — Story 23.
* Manual Ticket Assignment/reassignment — already covered by Story 12.
* Ticket status workflow and escalation — Story 13.
* Ticket History implementation — Story 14.
* Quick Reply template CRUD/management — Story 17.
* Quick Reply insertion implementation — Story 17.
* WhatsApp integration — Story 20.
* SMS integration — Story 20.
* Live Chat — Story 21.
* SLA rules and automatic escalation — Stories 22–25.
* AI-generated replies, categorization, summaries or chatbot — Stories 39–43.
* Customer Portal authentication and portal features — Stories 29–33.
* ERP and external-system integrations — Stories 44–47.
* Building a separate ticketing system for Email/Web Form.
* Automatic sending of Quick Replies without explicit Agent action.
* Sending Email through Quick Reply itself.

```

دي كده النسخة اللي أنصح تستخدميها. **الجزء الحاسم هو إن Story 17 تجهّز الرد، وStory 19 هي اللي تعمل Send Email فعليًا.**
```
