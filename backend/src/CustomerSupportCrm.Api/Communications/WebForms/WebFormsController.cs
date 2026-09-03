using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CustomerSupportCrm.Api.Communications.WebForms;

/// <summary>
/// Anonymous, internet-facing entry point for a customer-side web form — see
/// <see cref="WebFormSubmissionService"/> for the business rules. Rate-limited via the
/// <c>"public-channel"</c> policy registered in <c>Program.cs</c> (shared with every other anonymous
/// channel entry point — Live Chat, Email, WhatsApp, SMS); every other endpoint in this application
/// requires authentication (see the <c>SetFallbackPolicy</c> comment there), so
/// <see cref="AllowAnonymousAttribute"/> here is a deliberate, narrow opt-out.
/// </summary>
[ApiController]
[Route("api/public/web-forms")]
[AllowAnonymous]
[EnableRateLimiting("public-channel")]
public class WebFormsController(IWebFormSubmissionService submissionService) : ControllerBase
{
    [HttpPost("tickets")]
    public async Task<IActionResult> SubmitTicket(WebFormSubmissionRequest request, CancellationToken cancellationToken)
    {
        var result = await submissionService.SubmitAsync(request, cancellationToken);
        return result.Outcome switch
        {
            WebFormSubmissionOutcome.Success =>
                StatusCode(StatusCodes.Status201Created, new WebFormSubmissionResponse(result.TicketId!.Value, result.CustomerId!.Value)),
            // 202, no body: a bot that filled the honeypot sees the same shape as a genuine submission,
            // so it can't tell it was silently dropped.
            WebFormSubmissionOutcome.HoneypotTriggered => Accepted(),
            WebFormSubmissionOutcome.InvalidEmail => BadRequest(new { error = "invalid_email" }),
            _ => Problem(statusCode: 500),
        };
    }
}
