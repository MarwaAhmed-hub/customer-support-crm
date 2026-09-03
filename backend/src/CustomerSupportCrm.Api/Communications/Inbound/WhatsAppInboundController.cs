using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace CustomerSupportCrm.Api.Communications.Inbound;

/// <summary>
/// Manual/dev replay of an inbound WhatsApp message — see <see cref="InboundMessageService"/>'s
/// remarks for the algorithm.
/// </summary>
/// <remarks>
/// Correction: originally Administrator-gated on the theory that a real WhatsApp webhook would
/// authenticate via provider signature verification rather than a staff login. But this represents a
/// <em>customer</em> sending a message — never a staff member — so it is anonymous, the same as the
/// public Web Form and Live Chat widget, and rate-limited the same way for the same reason.
/// </remarks>
[ApiController]
[Route("api/public/channels/whatsapp")]
[AllowAnonymous]
public class WhatsAppInboundController(IInboundMessageService inboundMessageService) : ControllerBase
{
    [HttpPost("inbound")]
    [EnableRateLimiting("public-channel")]
    public async Task<ActionResult<InboundMessageResponse>> Inbound(InboundMessageRequest request, CancellationToken cancellationToken)
    {
        var result = await inboundMessageService.IngestAsync("WhatsApp", request, cancellationToken);
        return Ok(new InboundMessageResponse(result.TicketId, result.CustomerId, result.Deduplicated));
    }
}
