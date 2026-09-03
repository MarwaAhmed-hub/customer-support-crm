# Story 24 — Escalation Rules

## Feature

SLA & Automation

## Story Title

Escalation Rules

## Description

The system should automatically escalate tickets when their SLA is approaching breach or has been breached.

Escalation is based on the SLA timers defined in **Story 22 — SLA Response & Resolution Targets**.

The escalation logic must work independently from ticket assignment.

A ticket can be:

* Assigned to an Agent
* Unassigned and still waiting in the Unassigned Tickets Queue

SLA timers are not started or restarted by assignment, reassignment, or category changes.

### Escalation Thresholds

The default escalation thresholds are:

* **Warning:** 80% of the SLA duration has elapsed.
* **Breach:** 100% of the SLA duration has elapsed.

Both SLA types are handled independently:

* First Response SLA
* Resolution SLA

Example for a High Priority ticket:

* First Response SLA = 30 minutes
* Resolution SLA = 4 hours

If the ticket is created at 10:00:

* First Response Warning = 10:24
* First Response Breach = 10:30
* Resolution Warning = 13:12
* Resolution Breach = 14:00

## Escalation Routing

### Assigned Ticket

When the ticket is assigned to an Agent:

* The Agent can receive the SLA warning.
* When the SLA is breached, the escalation should go to the responsible **Manager** for that Agent/Department according to the existing system relationships.

The system must use the existing `Manager` role.

Do not introduce or create a new `Supervisor` or `Team Lead` role.

### Unassigned Ticket

If the ticket has no assigned Agent:

* The ticket can still reach SLA warning/breach.
* The escalation must not wait for an Agent to be assigned.
* On SLA breach, the escalation should go to the **Administrator** responsible for the Unassigned Tickets Queue.

The ticket remains in the Unassigned Tickets Queue until it is assigned through Story 23.

### Customer

The `Customer` role is not an escalation target.

Customers may receive normal ticket/SLA notifications according to Story 25, but escalation actions are internal.

---

## Business Flow

```text
Ticket Created
    |
    +--> SLA Starts
    |
    +--> Assigned Agent?
           |
           +-- No --> Unassigned Tickets Queue
           |             |
           |             +--> SLA Warning
           |             |
           |             +--> SLA Breach
           |                    |
           |                    +--> Escalate to Administrator
           |
           +-- Yes --> Assigned to Agent
                         |
                         +--> SLA Warning
                         |       |
                         |       +--> Agent
                         |
                         +--> SLA Breach
                                 |
                                 +--> Manager
```

## Acceptance Criteria

### 1. SLA Warning

* The system evaluates the First Response SLA and Resolution SLA independently.
* When 80% of an SLA duration has elapsed, the corresponding SLA warning is triggered.
* Warning must be triggered only once for the same SLA milestone.
* If the ticket is assigned, the warning can be directed to the assigned Agent.
* If the ticket is unassigned, the warning must still be processed and must not depend on assignment.

### 2. SLA Breach

* When 100% of an SLA duration has elapsed without satisfying the SLA, the SLA is considered breached.
* Breach escalation must be triggered only once for the same SLA milestone.
* If the ticket is assigned, the breach escalation goes to the responsible Manager for the Agent/Department.
* If the ticket is unassigned, the breach escalation goes to the Administrator responsible for the Unassigned Tickets Queue.
* The system must not wait for assignment before processing an SLA breach.

### 3. First Response Escalation

* First Response SLA is tracked independently from Resolution SLA.
* If the first response target is missed, the First Response SLA is marked as breached.
* Resolution SLA continues to be evaluated independently.
* A First Response breach must not automatically mark the Resolution SLA as breached.

### 4. Resolution Escalation

* Resolution SLA is tracked independently from First Response SLA.
* If the ticket is not resolved before the Resolution SLA deadline, the Resolution SLA is marked as breached.
* Resolution breach escalation follows the same routing rules:

  * Assigned ticket → responsible Manager.
  * Unassigned ticket → responsible Administrator.

### 5. Assignment Does Not Reset SLA

* Assigning a ticket to an Agent must not restart the SLA timers.
* Reassigning a ticket to another Agent must not restart the SLA timers.
* Changing the ticket Category must not restart the SLA timers.
* SLA deadlines remain based on the original Ticket creation time.

### 6. Unassigned Tickets

* A ticket can reach SLA warning while still unassigned.
* A ticket can reach SLA breach while still unassigned.
* Unassigned tickets must not be excluded from SLA escalation processing.
* Breached unassigned tickets are escalated to the Administrator responsible for the Unassigned Tickets Queue.

### 7. Idempotency

* The same SLA warning must not be triggered repeatedly.
* The same SLA breach must not create duplicate escalation actions.
* Background processing/retries must not result in duplicate escalation records/actions.

### 8. Existing Roles Only

The escalation logic must use the existing system roles:

* `ADMINISTRATOR`
* `MANAGER`
* `AGENT`
* `CUSTOMER`

Do not introduce:

* Supervisor
* Team Lead
* Any new escalation-specific role

The responsible Manager should be resolved using the existing Agent/Department/Branch relationships already available in the system.

### 9. No AI or Automatic Classification

Story 24 must not introduce:

* AI-based escalation
* AI-based ticket classification
* Keyword-based escalation
* Channel-specific escalation algorithms

Escalation is driven only by:

* Ticket SLA
* SLA progress
* Ticket assignment state
* Existing organizational relationships

---

## Technical Hints

* Reuse the SLA calculation/state from Story 22.
* Story 24 should not calculate or restart the original SLA independently.
* Keep First Response and Resolution escalation states separate.
* Escalation processing should be safe to run repeatedly because background jobs may retry.
* Use the existing `Manager`, `Administrator`, `Agent`, `Department`, and `Branch` relationships.
* Do not create a new Supervisor/TeamLead entity or role.
* Escalation processing can be implemented through the existing background job/scheduler mechanism.
* Notification delivery itself belongs to **Story 25 — Alerts & Notifications**.

## Dependencies

* **Story 22 — SLA Response & Resolution Targets**

  * Provides SLA targets and SLA state.
* **Story 23 — Automatic Ticket Assignment**

  * Provides automatic Agent assignment after Category selection.
* Existing:

  * Ticket
  * Agent
  * Manager
  * Administrator
  * Department
  * Branch
  * Category
  * Ticket Status
  * Priority

## Out of Scope

Story 24 does not include:

* SLA target calculation/definition
* Ticket assignment algorithm
* Category-to-Department assignment logic
* Notification implementation
* Customer notifications
* Email/WhatsApp/SMS notification delivery
* AI classification
* New roles such as Supervisor or Team Lead
* Manual Agent selection by Administrator
* Changing or restarting SLA timers
