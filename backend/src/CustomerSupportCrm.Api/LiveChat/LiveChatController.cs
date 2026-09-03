using CustomerSupportCrm.Api.Audit;
using CustomerSupportCrm.Api.Auth;
using CustomerSupportCrm.Api.Authorization;
using CustomerSupportCrm.Domain.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupportCrm.Api.LiveChat;

/// <summary>Agent workspace for live chat — see <see cref="LiveChatService"/> for the business rules. Closing/reopening a chat is not a separate action here: it is the linked ticket's existing status transition (Story 13), used exactly the same way any other ticket is closed.</summary>
[ApiController]
[Route("api/live-chat")]
[Authorize]
public class LiveChatController(ILiveChatService liveChatService, IAuditLogService auditLogService) : ControllerBase
{
    [HasPermission(Permissions.LiveChat.View)]
    [HttpGet("conversations")]
    public async Task<ActionResult<IReadOnlyList<LiveChatSessionListItemDto>>> List([FromQuery] string? status, CancellationToken cancellationToken)
    {
        var (error, scopeToUserId) = ResolveScope();
        if (error is not null)
        {
            return error;
        }

        return Ok(await liveChatService.ListForAgentAsync(status, scopeToUserId, cancellationToken));
    }

    [HasPermission(Permissions.LiveChat.View)]
    [HttpGet("conversations/{id:guid}")]
    public async Task<ActionResult<LiveChatSessionDetailDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var (error, scopeToUserId) = ResolveScope();
        if (error is not null)
        {
            return error;
        }

        var session = await liveChatService.GetForAgentAsync(id, scopeToUserId, cancellationToken);
        return session is null ? NotFound() : Ok(session);
    }

    /// <summary>
    /// Whoever can assign tickets (<c>tickets.assign</c> — Manager/Admin) already needs full visibility
    /// across the team's queue to do that job, so they see every conversation; a caller with only
    /// <c>livechat.view</c> (a plain Agent) sees just the ones assigned to them. Reuses the existing
    /// assignment permission as the signal instead of checking a role name or adding a dedicated
    /// "see everyone's chats" permission.
    /// </summary>
    private (ActionResult? Error, Guid? ScopeToUserId) ResolveScope()
    {
        if (User.HasPermission(Permissions.Tickets.Assign))
        {
            return (null, null);
        }

        var actorUserId = User.GetUserId();
        return actorUserId is null ? (Unauthorized(), null) : (null, actorUserId);
    }

    [HasPermission(Permissions.LiveChat.Send)]
    [HttpPost("conversations/{id:guid}/messages")]
    public async Task<ActionResult<LiveChatMessageDto>> SendMessage(Guid id, SendAgentLiveChatMessageRequest request, CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        if (actorUserId is null)
        {
            return Unauthorized();
        }

        var result = await liveChatService.AppendAgentMessageAsync(id, actorUserId.Value, request.Body, cancellationToken);
        if (result.Outcome == LiveChatOperationOutcome.Success)
        {
            await auditLogService.RecordAsync(
                action: "livechat.message.sent",
                summary: "Live chat reply sent",
                entityType: "LiveChatSession",
                entityId: id.ToString(),
                ct: cancellationToken);
        }

        return result.Outcome switch
        {
            LiveChatOperationOutcome.Success => StatusCode(StatusCodes.Status201Created, result.Message),
            LiveChatOperationOutcome.SessionNotFound => NotFound(),
            LiveChatOperationOutcome.ConversationClosed => Conflict(new { error = "conversation_closed" }),
            LiveChatOperationOutcome.InvalidBody => BadRequest(new { error = "invalid_body" }),
            _ => Problem(statusCode: 500),
        };
    }
}
