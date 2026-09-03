using CustomerSupportCrm.Api.Audit;
using CustomerSupportCrm.Api.Auth;
using CustomerSupportCrm.Api.Authorization;
using CustomerSupportCrm.Domain.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupportCrm.Api.Tickets.Collaboration;

/// <summary>Internal, staff-only discussion thread on a ticket — see <see cref="TicketCollaborationCommentsService"/> for the business rules. Never customer-facing; no edit/delete.</summary>
[ApiController]
[Route("api/tickets/{ticketId:guid}/collaboration-comments")]
[Authorize]
public class TicketCollaborationCommentsController(ITicketCollaborationCommentsService commentsService, IAuditLogService auditLogService) : ControllerBase
{
    [HasPermission(Permissions.Tickets.CollaborationView)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TicketCollaborationCommentDto>>> List(Guid ticketId, CancellationToken cancellationToken)
    {
        var comments = await commentsService.ListAsync(ticketId, cancellationToken);
        return comments is null ? NotFound() : Ok(comments);
    }

    [HasPermission(Permissions.Tickets.CollaborationCreate)]
    [HttpPost]
    public async Task<ActionResult<TicketCollaborationCommentDto>> Create(Guid ticketId, CreateTicketCollaborationCommentRequest request, CancellationToken cancellationToken)
    {
        // [Authorize] guarantees a valid subject claim, so GetUserId() is non-null in practice; the
        // 401 Unauthorized fallback is defensive, matching TicketsController.Create.
        var actorUserId = User.GetUserId();
        if (actorUserId is null)
        {
            return Unauthorized();
        }

        var result = await commentsService.CreateAsync(ticketId, actorUserId.Value, request, cancellationToken);
        if (result.Outcome == TicketCollaborationCommentOperationOutcome.Success)
        {
            await auditLogService.RecordAsync(
                action: "ticket.collaboration.comment.created",
                summary: $"Internal collaboration comment added to ticket {ticketId}",
                entityType: "TicketCollaborationComment",
                entityId: result.Comment!.Id.ToString(),
                ct: cancellationToken);
        }

        return result.Outcome switch
        {
            TicketCollaborationCommentOperationOutcome.Success => CreatedAtAction(nameof(List), new { ticketId }, result.Comment),
            TicketCollaborationCommentOperationOutcome.TicketNotFound => NotFound(),
            TicketCollaborationCommentOperationOutcome.InvalidBody => Invalid("invalid_body"),
            _ => Problem(statusCode: 500),
        };
    }

    private BadRequestObjectResult Invalid(string error) => BadRequest(new { error });
}
