# Story intake

Fill this template for each story you want planned. Keep it copy-paste-friendly: the planner reads **this file and the files in `attachments/`**, nothing else.

* Folder: `.squad/stories/communication-channels/whatsapp-sms/intake.md`

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

---

## Title

```text
WhatsApp & SMS
```

---

## Description

```text
Implement WhatsApp and SMS as inbound and outbound communication channels for the Customer Support CRM.

The goal is to allow customers to contact the CRM through WhatsApp or SMS, have incoming messages enter the existing CRM workflow, associate the communication with the correct customer and ticket, and allow authorized agents to respond through the same channel.

The implementation must reuse the existing Customer, Ticket, CustomerInteraction and Agent Desk concepts rather than introducing a separate ticketing workflow.

1. WhatsApp — inbound

- Receive incoming WhatsApp messages through an application-level channel abstraction.
- Identify the customer using the WhatsApp phone number and/or available contact information.
- If the customer already exists, link the communication to that customer.
- If the customer does not exist, create a new customer using the available information.
- Determine whether the message starts a new support request or belongs to an existing ticket/conversation.
- Create a new Ticket for a new support request.
- Link/update the existing Ticket when the message belongs to an existing conversation.
- Create a CustomerInteraction for the WhatsApp communication.
- Preserve the message body and relevant metadata such as sender, recipient, timestamp, channel and external message identifier.
- Make the resulting Ticket available through the existing Ticket Management and Agent Desk flows.

2. SMS — inbound

- Receive incoming SMS messages through an application-level channel abstraction.
- Identify the customer using the sender phone number and/or available contact information.
- If the customer already exists, link the communication to that customer.
- If the customer does not exist, create a new customer using the available information.
- Determine whether the message starts a new support request or belongs to an existing ticket/conversation.
- Create a new Ticket for a new support request.
- Link/update the existing Ticket when the message belongs to an existing conversation.
- Create a CustomerInteraction for the SMS communication.
- Preserve the message body and relevant metadata such as sender, recipient, timestamp, channel and external message identifier.
- Make the resulting Ticket available through the existing Ticket Management and Agent Desk flows.

3. Ticket classification and assignment boundary

When an inbound WhatsApp or SMS message creates a new Ticket:

- The customer does NOT have to provide Department or Category.
- If Department, Category or other routing information is not known, leave those values Unassigned.
- Do not invent or automatically guess Department or Category in this story.
- The Ticket can first be handled by the existing manual workflow from Ticket Management.
- Authorized users can manually set the required Department and Category.
- Manual Agent assignment remains owned by Story 12.
- Automatic Agent assignment remains owned by Story 23.
- This story must not introduce its own assignment logic.

The expected inbound flow is:

Customer
  -> WhatsApp / SMS
  -> Receive Message
  -> Find or Create Customer
  -> Create or Link Ticket
  -> Department / Category may remain Unassigned
  -> Create CustomerInteraction
  -> Ticket appears in CRM
  -> Existing Ticket Management / Agent Desk flow handles the Ticket.

4. Outbound WhatsApp/SMS replies

Agents must be able to respond to customers from the existing Ticket detail / Agent Desk experience.

The response flow is:

Agent opens assigned Ticket
  -> Reads customer communication
  -> Opens Reply Composer
  -> Optionally selects a Quick Reply
  -> Quick Reply text is inserted into the composer
  -> Agent can edit the message
  -> Agent explicitly clicks Send
  -> CRM determines the Ticket communication channel
  -> If channel = WhatsApp, send through WhatsApp channel abstraction
  -> If channel = SMS, send through SMS channel abstraction
  -> Record the outbound communication
  -> Keep it linked to the same Customer and Ticket.

Quick Reply behavior:

- Story 17 owns Quick Reply templates.
- Quick Reply only inserts reusable text into the existing reply composer.
- Selecting a Quick Reply must NOT send the message automatically.
- The Agent must explicitly press Send.
- The Send action belongs to the communication channel / ticket reply workflow, not to the Quick Reply feature.
- The same Quick Reply mechanism may be used for WhatsApp, SMS and other supported channels where appropriate.

The system must NOT create a separate Quick Reply sending mechanism.

5. Outbound WhatsApp

- Allow an authorized Agent to send a response for a Ticket whose communication channel is WhatsApp.
- Use the WhatsApp channel abstraction to send the message.
- Record the outbound WhatsApp communication.
- Associate the outbound communication with the correct Customer and Ticket.
- Preserve provider/external message identifiers and delivery information when available.

6. Outbound SMS

- Allow an authorized Agent to send a response for a Ticket whose communication channel is SMS.
- Use the SMS channel abstraction to send the message.
- Record the outbound SMS communication.
- Associate the outbound communication with the correct Customer and Ticket.
- Preserve provider/external message identifiers and delivery information when available.

7. Common CRM behavior

Both WhatsApp and SMS must enter the same core Ticket workflow already established by Ticket Management.

Customer
  -> WhatsApp / SMS
  -> Receive Message
  -> Find or Create Customer
  -> Create or Link Ticket
  -> Create CustomerInteraction
  -> Ticket appears in CRM
  -> Existing Ticket Management / Agent Desk flow
  -> Agent works on Ticket
  -> Agent writes reply or inserts Quick Reply
  -> Agent explicitly sends reply
  -> CRM sends through the Ticket's communication channel
  -> Outbound communication is recorded.

CustomerInteraction ownership is explicit:

- Story 11 owns CustomerInteraction creation for internally/manual-created tickets.
- Story 19 owns CustomerInteraction creation for Email and Web Form.
- Story 20 owns CustomerInteraction creation for WhatsApp and SMS.
- Story 21 will own CustomerInteraction creation for Live Chat.
- Story 14 owns TicketHistory only and must not create CustomerInteraction records.

8. Reliability

- Incoming webhook/message processing must be safe to retry.
- Duplicate external messages must not create duplicate Tickets or duplicate CustomerInteraction records.
- Provider failures must not leave partially-created CRM records.
- Invalid or malformed inbound messages must be handled safely.
- Logging must follow existing CRM conventions.

9. Assignment boundary

This story does NOT implement automatic Agent selection.

Created WhatsApp/SMS Tickets must remain compatible with:

- Story 12 — Manual Ticket Assignment.
- Story 15 — Agent Dashboard.
- Story 23 — Automatic Ticket Assignment.

Story 23 will later determine which eligible Agent receives a Ticket.

Do not create a separate assignment mechanism inside this story.
```

---

## Acceptance criteria

```text
### WhatsApp inbound

- [ ] The system can receive an incoming WhatsApp message through an application-level channel abstraction.
- [ ] The sender phone number is captured.
- [ ] The message body is preserved.
- [ ] Timestamp and channel information are preserved.
- [ ] An external/provider message identifier is captured when available.
- [ ] An existing customer is reused when the sender matches a known customer.
- [ ] A new customer can be created when no matching customer exists.
- [ ] A new support request creates a Ticket.
- [ ] A message belonging to an existing ticket/conversation is linked to that existing Ticket.
- [ ] The WhatsApp message creates exactly one CustomerInteraction.
- [ ] The CustomerInteraction identifies WhatsApp as the communication channel.
- [ ] Reprocessing the same external message does not create duplicate Tickets or CustomerInteractions.
- [ ] A newly-created Ticket does not require Department or Category to be known.
- [ ] Unknown Department or Category remains Unassigned.
- [ ] The story does not automatically guess or invent Department or Category.

### SMS inbound

- [ ] The system can receive an incoming SMS through an application-level channel abstraction.
- [ ] The sender phone number is captured.
- [ ] The SMS body is preserved.
- [ ] Timestamp and channel information are preserved.
- [ ] An external/provider message identifier is captured when available.
- [ ] An existing customer is reused when the sender matches a known customer.
- [ ] A new customer can be created when no matching customer exists.
- [ ] A new support request creates a Ticket.
- [ ] A message belonging to an existing ticket/conversation is linked to that existing Ticket.
- [ ] The SMS creates exactly one CustomerInteraction.
- [ ] The CustomerInteraction identifies SMS as the communication channel.
- [ ] Reprocessing the same external message does not create duplicate Tickets or CustomerInteractions.
- [ ] A newly-created Ticket does not require Department or Category to be known.
- [ ] Unknown Department or Category remains Unassigned.
- [ ] The story does not automatically guess or invent Department or Category.

### Agent reply

- [ ] An authorized Agent can open the reply composer for an eligible Ticket.
- [ ] The Agent can manually type a reply.
- [ ] The Agent can optionally select a Quick Reply from Story 17.
- [ ] Selecting a Quick Reply inserts its text into the reply composer.
- [ ] Selecting a Quick Reply does not send the message.
- [ ] The Agent can edit the inserted text before sending.
- [ ] The Agent must explicitly click Send to send the response.
- [ ] The Send action uses the communication channel associated with the Ticket.
- [ ] A WhatsApp Ticket sends through the WhatsApp channel abstraction.
- [ ] An SMS Ticket sends through the SMS channel abstraction.
- [ ] The outbound communication is recorded and linked to the correct Customer and Ticket.

### Outbound WhatsApp/SMS

- [ ] An authorized Agent can send a WhatsApp response for an eligible WhatsApp Ticket.
- [ ] An authorized Agent can send an SMS response for an eligible SMS Ticket.
- [ ] The correct communication channel is selected from the Ticket/channel context.
- [ ] The outbound communication is recorded according to the existing communication/interactions model.
- [ ] The outbound record is associated with the correct Customer and Ticket.
- [ ] Provider/external message identifiers are retained when available.
- [ ] Provider failures are handled without corrupting the Ticket or Customer data.

### CustomerInteraction ownership

- [ ] WhatsApp inbound communication creates its own CustomerInteraction.
- [ ] SMS inbound communication creates its own CustomerInteraction.
- [ ] Outbound WhatsApp/SMS communication is represented according to the existing communication model.
- [ ] Story 14 Ticket History does not create CustomerInteraction records.
- [ ] CustomerInteraction records are not duplicated when Ticket History is updated.

### CRM integration

- [ ] WhatsApp-created Tickets use the existing Ticket Management workflow.
- [ ] SMS-created Tickets use the existing Ticket Management workflow.
- [ ] Created Tickets are compatible with Story 12 manual assignment.
- [ ] Created Tickets are visible to the Agent Desk/Story 15 where applicable.
- [ ] Created Tickets can later be processed by Story 23 Automatic Ticket Assignment.
- [ ] No separate Ticket model or parallel assignment workflow is introduced.
- [ ] No separate Quick Reply sending mechanism is introduced.

### Reliability and validation

- [ ] Duplicate webhook/message delivery is handled idempotently.
- [ ] Invalid inbound payloads are rejected or safely ignored according to project conventions.
- [ ] Partial failures do not leave inconsistent Customer/Ticket/Interaction data.
- [ ] Provider communication errors are logged using existing application logging conventions.
```

---

## Attachments

| File (relative to this folder) | What it is |
| ------------------------------ | ---------- |
| None                           |            |

---

## Dependencies

* **Blocked by / related ids:** None

* **Depends on code areas or other stories:**

  * Stories 07–09 — Customer Management.
  * Stories 10–14 — Ticket Management.
  * Story 12 — Manual Ticket Assignment.
  * Story 15 — Agent Dashboard.
  * Story 17 — Quick Replies.
  * Story 19 — Email & Web Forms.
  * Story 23 — Automatic Ticket Assignment, future integration point only.
  * Story 46 — Email / SMS / WhatsApp Integrations, for provider-specific integration concerns outside the core CRM channel behavior.

---

## Extra notes

* WhatsApp and SMS are communication entry points into the existing CRM; they are not separate ticketing systems.
* A customer does not have to specify Department or Category when contacting the CRM.
* Unknown routing information remains Unassigned until an authorized CRM user sets it.
* This story does not automatically categorize or classify customer messages.
* This story does not automatically assign an Agent.
* Manual assignment remains Story 12.
* Automatic assignment remains Story 23.
* Quick Replies are reusable text templates only.
* Quick Replies insert text into the existing reply composer.
* Quick Replies never send messages automatically.
* The Agent must explicitly press Send.
* The Send operation uses the Ticket's communication channel.
* CustomerInteraction ownership remains explicit:

  * Story 11 → internal/manual ticket interaction.
  * Story 19 → Email/Web Form interactions.
  * Story 20 → WhatsApp/SMS interactions.
  * Story 21 → Live Chat interactions.
  * Story 14 → TicketHistory only.

---

## Technical hints

* Use application-level interfaces/abstractions for WhatsApp and SMS receiving and sending.
* Do not couple core application/domain logic directly to a specific provider.
* Webhook processing should support idempotency using provider/external message identifiers where available.
* Reuse existing Customer, CustomerInteraction, Ticket and TicketHistory models/services.
* Reuse the existing Ticket reply/composer flow.
* Reuse Story 17 Quick Reply functionality; do not implement another Quick Reply system.
* The Send operation should determine the communication channel from the Ticket/context rather than from the Quick Reply.
* Preserve channel information so CRM users can distinguish Email, Web Form, WhatsApp, SMS and Live Chat communications.
* Primary language: `C#`.
* Backend: ASP.NET Core Web API.
* Frontend: React + TypeScript.
* Repos/roots: `.`

---

## Out of scope

* Automatic ticket assignment — Story 23.
* Manual ticket assignment/reassignment — Story 12.
* Automatic Department/Category prediction.
* AI categorization.
* Ticket status workflow and escalation — Story 13.
* Ticket history implementation — Story 14.
* Email and Web Forms — Story 19.
* Live Chat — Story 21.
* SLA response/resolution targets — Story 22.
* Escalation rules — Story 24.
* Alerts and notifications — Story 25.
* AI features — Stories 39–43.
* Customer Portal — Stories 29–33.
* ERP and external systems — Stories 44–47.
* Implementing provider-specific business logic inside the core CRM domain.
* Creating a separate Quick Reply feature or sending mechanism.

```

**كده الـbusiness واضح لـClaude:**  
**Quick Reply = يملأ الـcomposer فقط → الـAgent يعدّل → يضغط Send → الـCRM يعرف الـChannel من الـTicket → يرسل WhatsApp أو SMS.**
```
