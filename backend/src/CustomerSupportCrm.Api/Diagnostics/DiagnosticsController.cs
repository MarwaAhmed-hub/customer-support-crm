using CustomerSupportCrm.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupportCrm.Api.Diagnostics;

/// <summary>
/// Smoke-test endpoints that verify the authentication pipeline end to end.
/// </summary>
/// <remarks>
/// <b>These are not CRM business functionality.</b> They carry no business data, and their only
/// consumers are Story 01's manual verification steps and integration tests — no frontend feature
/// code calls them. Once real protected endpoints exist in Story 02 they may be removed or moved
/// behind a health-check gate. Do not extend them with version info, database status, or uptime;
/// a genuine health check is <c>Microsoft.Extensions.Diagnostics.HealthChecks</c>, and that is not
/// this story.
/// </remarks>
[ApiController]
[Route("api/diagnostics")]
public class DiagnosticsController : ControllerBase
{
    [AllowAnonymous]
    [HttpGet("ping")]
    public IActionResult Ping() => Ok(new { status = "ok" });

    [Authorize]
    [HttpGet("ping/secure")]
    public IActionResult PingSecure() => Ok(new { status = "ok", userId = User.GetUserId() });
}
