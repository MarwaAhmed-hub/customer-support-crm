using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CustomerSupportCrm.Api.LiveChat;

/// <summary>
/// Anonymous, internet-facing entry point for the live chat widget — see <see cref="LiveChatService"/>
/// for the business rules. The CRM itself is the transport (there is no external provider the way
/// Email/WhatsApp/SMS have one), so unlike those channels there is no "ingest" abstraction to swap
/// out later — a message posted here <i>is</i> the delivery. Starting a session is rate-limited via the
/// same <c>"public-channel"</c> policy as every other anonymous channel entry point (Web Form, Email,
/// WhatsApp, SMS); sending a follow-up message or polling is not — both require a session token already
/// obtained from a rate-limited start call, so the abuse surface is naturally bounded per session.
/// </summary>
[ApiController]
[Route("api/public/live-chat")]
[AllowAnonymous]
public class PublicLiveChatController(ILiveChatService liveChatService) : ControllerBase
{
    [HttpPost("sessions")]
    [EnableRateLimiting("public-channel")]
    public async Task<ActionResult<StartLiveChatSessionResponse>> Start(StartLiveChatSessionRequest request, CancellationToken cancellationToken)
    {
        var result = await liveChatService.StartAsync(request, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpGet("sessions/{id:guid}/messages")]
    public async Task<ActionResult<LiveChatSessionPublicDto>> GetMessages(Guid id, [FromQuery] string sessionToken, CancellationToken cancellationToken)
    {
        var result = await liveChatService.GetPublicSessionAsync(id, sessionToken, cancellationToken);
        return result.Outcome switch
        {
            LiveChatOperationOutcome.Success => Ok(result.Session),
            LiveChatOperationOutcome.SessionNotFound => NotFound(),
            LiveChatOperationOutcome.InvalidSessionToken => Forbid(),
            _ => Problem(statusCode: 500),
        };
    }

    [HttpPost("sessions/{id:guid}/messages")]
    public async Task<ActionResult<LiveChatMessageDto>> SendMessage(Guid id, SendCustomerLiveChatMessageRequest request, CancellationToken cancellationToken)
    {
        var result = await liveChatService.AppendCustomerMessageAsync(id, request.SessionToken, request.Body, cancellationToken);
        return result.Outcome switch
        {
            LiveChatOperationOutcome.Success => StatusCode(StatusCodes.Status201Created, result.Message),
            LiveChatOperationOutcome.SessionNotFound => NotFound(),
            LiveChatOperationOutcome.InvalidSessionToken => Forbid(),
            LiveChatOperationOutcome.ConversationClosed => Conflict(new { error = "conversation_closed" }),
            LiveChatOperationOutcome.InvalidBody => BadRequest(new { error = "invalid_body" }),
            _ => Problem(statusCode: 500),
        };
    }
}
