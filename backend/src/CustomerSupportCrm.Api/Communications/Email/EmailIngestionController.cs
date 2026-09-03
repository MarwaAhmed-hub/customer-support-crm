using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CustomerSupportCrm.Api.Communications.Email;

/// <summary>
/// Manual/dev replay of an inbound email — this story ships no real mailbox polling (see
/// <see cref="NullEmailSender"/>'s remarks), so this is how the ingest → find-or-create-customer →
/// find-or-create-ticket → interaction flow is exercised end to end.
/// </summary>
/// <remarks>
/// Correction: originally gated on the same admin-only permission as System Settings, on the theory
/// that a real mail-server-to-CRM webhook would authenticate with a shared secret rather than a staff
/// login. But every one of these ingest endpoints represents a <em>customer</em> submitting something —
/// never a staff member signing in — so it is anonymous, the same as the public Web Form and Live Chat
/// widget, and rate-limited the same way for the same reason (an anonymous write is otherwise
/// unbounded). The created ticket is attributed to the seeded system account, matching those.
/// </remarks>
[ApiController]
[Route("api/public/email/ingest")]
[AllowAnonymous]
public class EmailIngestionController(IEmailIngestionService ingestionService) : ControllerBase
{
    [HttpPost]
    [EnableRateLimiting("public-channel")]
    public async Task<ActionResult<EmailIngestionResponse>> Ingest(IncomingEmailRequest request, CancellationToken cancellationToken)
    {
        var result = await ingestionService.IngestAsync(request, cancellationToken);
        return result.Outcome switch
        {
            EmailIngestionOutcome.InvalidSender => BadRequest(new { error = "invalid_sender" }),
            _ => Ok(new EmailIngestionResponse(result.TicketId!.Value, result.CustomerId!.Value, result.Outcome == EmailIngestionOutcome.AlreadyProcessed)),
        };
    }
}
