using CustomerSupportCrm.Api.Audit;
using CustomerSupportCrm.Api.Auth;
using CustomerSupportCrm.Api.Authorization;
using CustomerSupportCrm.Api.Communications.Channels;
using CustomerSupportCrm.Api.Communications.Email;
using CustomerSupportCrm.Api.Notifications;
using CustomerSupportCrm.Api.Users;
using CustomerSupportCrm.Domain.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupportCrm.Api.Tickets.Tickets;

/// <summary>List/create/update tickets. See <see cref="TicketsService"/> for the business rules, including the create-time <c>CustomerInteraction</c> side effect.</summary>
[ApiController]
[Route("api/tickets")]
[Authorize]
public class TicketsController(
    ITicketsService ticketsService,
    IAuditLogService auditLogService,
    ITicketEmailReplyService emailReplyService,
    ITicketChannelReplyService channelReplyService,
    INotificationService notificationService) : ControllerBase
{
    [HasPermission(Permissions.Tickets.View)]
    [HttpGet]
    public async Task<ActionResult<PagedResult<TicketListItemDto>>> List(
        [FromQuery] Guid? customerId,
        [FromQuery] Guid? categoryId,
        [FromQuery] Guid? priorityId,
        [FromQuery] Guid? assignedUserId,
        [FromQuery] string? status,
        [FromQuery] bool? isEscalated,
        [FromQuery] string? search,
        [FromQuery] bool? unassignedOnly,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default) =>
        Ok(await ticketsService.ListAsync(customerId, categoryId, priorityId, assignedUserId, status, isEscalated, search, page, pageSize, unassignedOnly, cancellationToken));

    /// <summary>
    /// Story 15 (Agent Dashboard): the caller's own assigned tickets. Unlike the generic <see cref="List"/>
    /// filter above, <c>assignedUserId</c> here is derived from the JWT, never from the query string —
    /// this is what actually stops one agent from viewing another agent's dashboard data, since the
    /// generic list endpoint has no such restriction for any caller holding <c>tickets.view</c>.
    /// Route registered before <c>{id:guid}</c> would matter, but "mine" never satisfies the guid
    /// constraint, so the two never conflict.
    /// </summary>
    [HasPermission(Permissions.Tickets.View)]
    [HttpGet("mine")]
    public async Task<ActionResult<PagedResult<TicketListItemDto>>> Mine(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var actorUserId = User.GetUserId();
        if (actorUserId is null)
        {
            return Unauthorized();
        }

        return Ok(await ticketsService.ListAsync(
            customerId: null, categoryId: null, priorityId: null, assignedUserId: actorUserId.Value,
            status: null, isEscalated: null, search: null, page, pageSize, cancellationToken: cancellationToken));
    }

    [HasPermission(Permissions.Tickets.View)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TicketDetailDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var ticket = await ticketsService.GetAsync(id, cancellationToken);
        return ticket is null ? NotFound() : Ok(ticket);
    }

    [HasPermission(Permissions.Tickets.Create)]
    [HttpPost]
    public async Task<ActionResult<TicketDetailDto>> Create(CreateTicketRequest request, CancellationToken cancellationToken)
    {
        // [Authorize] guarantees a valid subject claim, so GetUserId() is non-null in practice; the
        // 401 Unauthorized fallback is defensive, matching SystemSettingsController's Update.
        var actorUserId = User.GetUserId();
        if (actorUserId is null)
        {
            return Unauthorized();
        }

        var result = await ticketsService.CreateAsync(request, actorUserId.Value, cancellationToken: cancellationToken);
        if (result.Outcome == TicketOperationOutcome.Success)
        {
            // Separate from the CustomerInteraction row TicketsService already persisted atomically
            // with the ticket — this is the audit trail, not the customer-facing activity feed.
            await auditLogService.RecordAsync(
                action: "create",
                summary: $"Ticket '{result.Ticket!.Subject}' created",
                entityType: "Ticket",
                entityId: result.Ticket.Id.ToString(),
                ct: cancellationToken);
        }
        return result.Outcome switch
        {
            TicketOperationOutcome.Success => CreatedAtAction(nameof(Get), new { id = result.Ticket!.Id }, result.Ticket),
            TicketOperationOutcome.CustomerNotFound => NotFound(new { error = "customer_not_found" }),
            TicketOperationOutcome.CategoryNotFound => NotFound(new { error = "category_not_found" }),
            TicketOperationOutcome.PriorityNotFound => NotFound(new { error = "priority_not_found" }),
            TicketOperationOutcome.InvalidSubject => Invalid("invalid_subject"),
            TicketOperationOutcome.InvalidDescription => Invalid("invalid_description"),
            _ => Problem(statusCode: 500),
        };
    }

    [HasPermission(Permissions.Tickets.Update)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TicketDetailDto>> Update(Guid id, UpdateTicketRequest request, CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        if (actorUserId is null)
        {
            return Unauthorized();
        }

        // Story 25: captured purely to detect whether this edit's category change triggered Story
        // 23's automatic assignment as a side effect (AssignedUserId null -> an agent) — same
        // before/after comparison UpdateAssignment below already does for its own audit payload.
        var before = await ticketsService.GetAsync(id, cancellationToken);

        var result = await ticketsService.UpdateAsync(id, request, actorUserId.Value, cancellationToken);
        if (result.Outcome == TicketOperationOutcome.Success)
        {
            await auditLogService.RecordAsync(
                action: "update",
                summary: $"Ticket '{result.Ticket!.Subject}' updated",
                entityType: "Ticket",
                entityId: result.Ticket.Id.ToString(),
                ct: cancellationToken);

            if (before?.AssignedUserId is null && result.Ticket.AssignedUserId is { } autoAssignedUserId)
            {
                await notificationService.NotifyTicketAssignedAsync(result.Ticket.Id, autoAssignedUserId, cancellationToken);
            }
        }
        return result.Outcome switch
        {
            TicketOperationOutcome.Success => Ok(result.Ticket),
            TicketOperationOutcome.NotFound => NotFound(),
            TicketOperationOutcome.CategoryNotFound => NotFound(new { error = "category_not_found" }),
            TicketOperationOutcome.PriorityNotFound => NotFound(new { error = "priority_not_found" }),
            TicketOperationOutcome.InvalidSubject => Invalid("invalid_subject"),
            TicketOperationOutcome.InvalidDescription => Invalid("invalid_description"),
            _ => Problem(statusCode: 500),
        };
    }

    /// <summary>Assign, reassign, or unassign (<c>assignedUserId: null</c>) a ticket. Story 12 — writes only an audit-log entry, never a <c>CustomerInteraction</c> (that is Create's one-time side effect from Story 11).</summary>
    [HasPermission(Permissions.Tickets.Assign)]
    [HttpPut("{id:guid}/assignment")]
    public async Task<ActionResult<TicketDetailDto>> UpdateAssignment(Guid id, UpdateTicketAssignmentRequest request, CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        if (actorUserId is null)
        {
            return Unauthorized();
        }

        // Captured before the mutation purely for the audit payload's previous/new comparison — a
        // second read, not a second write, so it does not affect the "touches only AssignedUserId"
        // guarantee TicketsService.UpdateAssignmentAsync itself provides.
        var before = await ticketsService.GetAsync(id, cancellationToken);

        var result = await ticketsService.UpdateAssignmentAsync(id, request.AssignedUserId, actorUserId.Value, cancellationToken);
        if (result.Outcome == TicketOperationOutcome.Success)
        {
            var assigneeDescription = result.Ticket!.AssignedUserName ?? "nobody (unassigned)";
            await auditLogService.RecordAsync(
                action: "assign",
                summary: $"Ticket '{result.Ticket.Subject}' assigned to {assigneeDescription}",
                entityType: "Ticket",
                entityId: result.Ticket.Id.ToString(),
                metadata: new { previousAssignedUserId = before?.AssignedUserId, assignedUserId = result.Ticket.AssignedUserId },
                ct: cancellationToken);

            // Story 25: notify the newly-assigned agent — covers both a first assignment and a
            // reassignment to someone new; an unassign (AssignedUserId -> null) never notifies anyone.
            if (result.Ticket.AssignedUserId is { } newlyAssignedUserId && before?.AssignedUserId != newlyAssignedUserId)
            {
                await notificationService.NotifyTicketAssignedAsync(result.Ticket.Id, newlyAssignedUserId, cancellationToken);
            }
        }
        return result.Outcome switch
        {
            TicketOperationOutcome.Success => Ok(result.Ticket),
            TicketOperationOutcome.NotFound => NotFound(),
            TicketOperationOutcome.InvalidAssignedUser => Invalid("invalid_assigned_user"),
            TicketOperationOutcome.AssignedUserOutsideDepartment => Invalid("assigned_user_outside_department"),
            _ => Problem(statusCode: 500),
        };
    }

    /// <summary>Story 13: transitions <c>Status</c> per the agreed lifecycle graph. Writes only an audit-log entry, never a <c>CustomerInteraction</c>.</summary>
    [HasPermission(Permissions.Tickets.Update)]
    [HttpPut("{id:guid}/status")]
    public async Task<ActionResult<TicketDetailDto>> UpdateStatus(Guid id, UpdateTicketStatusRequest request, CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        if (actorUserId is null)
        {
            return Unauthorized();
        }

        var before = await ticketsService.GetAsync(id, cancellationToken);

        var result = await ticketsService.UpdateStatusAsync(id, request.Status, actorUserId.Value, cancellationToken);
        if (result.Outcome == TicketOperationOutcome.Success)
        {
            await auditLogService.RecordAsync(
                action: "ticket.status.change",
                summary: $"Ticket '{result.Ticket!.Subject}' status changed to {result.Ticket.Status}",
                entityType: "Ticket",
                entityId: result.Ticket.Id.ToString(),
                metadata: new { previousStatus = before?.Status, status = result.Ticket.Status },
                ct: cancellationToken);
        }
        return result.Outcome switch
        {
            TicketOperationOutcome.Success => Ok(result.Ticket),
            TicketOperationOutcome.NotFound => NotFound(),
            TicketOperationOutcome.InvalidStatus => Invalid("invalid_status"),
            TicketOperationOutcome.InvalidStatusTransition => Invalid("invalid_status_transition"),
            _ => Problem(statusCode: 500),
        };
    }

    /// <summary>Story 13: manual escalation request with a required reason — held by Agent and Manager. Does not change <c>Status</c> or <c>AssignedUserId</c>, and writes no <c>CustomerInteraction</c>.</summary>
    [HasPermission(Permissions.Tickets.Escalate)]
    [HttpPost("{id:guid}/escalation")]
    public async Task<ActionResult<TicketDetailDto>> Escalate(Guid id, EscalateTicketRequest request, CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        if (actorUserId is null)
        {
            return Unauthorized();
        }

        var result = await ticketsService.EscalateAsync(id, request.Reason, actorUserId.Value, cancellationToken);
        if (result.Outcome == TicketOperationOutcome.Success)
        {
            await auditLogService.RecordAsync(
                action: "ticket.escalate",
                summary: $"Ticket '{result.Ticket!.Subject}' escalated",
                entityType: "Ticket",
                entityId: result.Ticket.Id.ToString(),
                metadata: new { reason = result.Ticket.EscalationReason },
                ct: cancellationToken);
        }
        return result.Outcome switch
        {
            TicketOperationOutcome.Success => Ok(result.Ticket),
            TicketOperationOutcome.NotFound => NotFound(),
            TicketOperationOutcome.InvalidEscalationReason => Invalid("invalid_reason"),
            TicketOperationOutcome.AlreadyEscalated => Invalid("already_escalated"),
            _ => Problem(statusCode: 500),
        };
    }

    /// <summary>Story 13: de-escalate / resolve an escalation — Manager-only (<see cref="Permissions.Tickets.EscalationManage"/>), distinct from the Agent-eligible request permission above. Does not change <c>Status</c> or <c>AssignedUserId</c>, and writes no <c>CustomerInteraction</c>.</summary>
    [HasPermission(Permissions.Tickets.EscalationManage)]
    [HttpDelete("{id:guid}/escalation")]
    public async Task<ActionResult<TicketDetailDto>> DeEscalate(Guid id, CancellationToken cancellationToken)
    {
        var result = await ticketsService.DeEscalateAsync(id, cancellationToken);
        if (result.Outcome == TicketOperationOutcome.Success)
        {
            await auditLogService.RecordAsync(
                action: "ticket.deescalate",
                summary: $"Ticket '{result.Ticket!.Subject}' de-escalated",
                entityType: "Ticket",
                entityId: result.Ticket.Id.ToString(),
                ct: cancellationToken);
        }
        return result.Outcome switch
        {
            TicketOperationOutcome.Success => Ok(result.Ticket),
            TicketOperationOutcome.NotFound => NotFound(),
            TicketOperationOutcome.NotEscalated => Invalid("not_escalated"),
            _ => Problem(statusCode: 500),
        };
    }

    /// <summary>Story 19: send an outbound email reply on an email-sourced ticket. 400 if the ticket isn't email-sourced or its customer has no email on file; 502 (and no persisted interaction) if <see cref="ITicketEmailReplyService"/>'s <see cref="IEmailSender"/> reports failure.</summary>
    [HasPermission(Permissions.Tickets.EmailReply)]
    [HttpPost("{id:guid}/email-replies")]
    public async Task<ActionResult<TicketDetailDto>> SendEmailReply(Guid id, SendTicketEmailReplyRequest request, CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        if (actorUserId is null)
        {
            return Unauthorized();
        }

        var result = await emailReplyService.SendReplyAsync(id, request.Body, actorUserId.Value, cancellationToken);
        if (result.Outcome == TicketEmailReplyOutcome.Success)
        {
            await auditLogService.RecordAsync(
                action: "ticket.email.reply.sent",
                summary: $"Email reply sent on ticket '{result.Ticket!.Subject}'",
                entityType: "Ticket",
                entityId: result.Ticket.Id.ToString(),
                ct: cancellationToken);
        }

        return result.Outcome switch
        {
            TicketEmailReplyOutcome.Success => Ok(result.Ticket),
            TicketEmailReplyOutcome.TicketNotFound => NotFound(),
            TicketEmailReplyOutcome.NotEmailChannel => Invalid("not_email_channel"),
            TicketEmailReplyOutcome.CustomerHasNoEmail => Invalid("customer_has_no_email"),
            TicketEmailReplyOutcome.InvalidBody => Invalid("invalid_body"),
            TicketEmailReplyOutcome.SendFailed => StatusCode(StatusCodes.Status502BadGateway, new { error = "email_send_failed" }),
            _ => Problem(statusCode: 500),
        };
    }

    /// <summary>Story 20: send an outbound WhatsApp/SMS reply on a channel-sourced ticket. 400 if the ticket's channel isn't sendable or there is no recipient phone number; 502 (and no persisted interaction) if <see cref="ITicketChannelReplyService"/>'s <see cref="IChannelMessageDispatcher"/> reports failure.</summary>
    [HasPermission(Permissions.Tickets.ChannelReply)]
    [HttpPost("{id:guid}/channel-replies")]
    public async Task<ActionResult<TicketDetailDto>> SendChannelReply(Guid id, SendTicketChannelReplyRequest request, CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        if (actorUserId is null)
        {
            return Unauthorized();
        }

        var result = await channelReplyService.SendReplyAsync(id, request.Body, actorUserId.Value, cancellationToken);
        if (result.Outcome == TicketChannelReplyOutcome.Success)
        {
            await auditLogService.RecordAsync(
                action: "ticket.channel.reply.sent",
                summary: $"Channel reply sent on ticket '{result.Ticket!.Subject}'",
                entityType: "Ticket",
                entityId: result.Ticket.Id.ToString(),
                ct: cancellationToken);
        }

        return result.Outcome switch
        {
            TicketChannelReplyOutcome.Success => Ok(result.Ticket),
            TicketChannelReplyOutcome.TicketNotFound => NotFound(),
            TicketChannelReplyOutcome.NotSendableChannel => Invalid("not_sendable_channel"),
            TicketChannelReplyOutcome.NoRecipient => Invalid("no_recipient"),
            TicketChannelReplyOutcome.InvalidBody => Invalid("invalid_body"),
            TicketChannelReplyOutcome.SendFailed => StatusCode(StatusCodes.Status502BadGateway, new { error = "provider_failed" }),
            _ => Problem(statusCode: 500),
        };
    }

    private BadRequestObjectResult Invalid(string error) => BadRequest(new { error });
}
