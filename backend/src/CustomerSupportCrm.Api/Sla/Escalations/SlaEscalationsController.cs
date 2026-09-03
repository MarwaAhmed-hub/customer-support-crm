using CustomerSupportCrm.Api.Authorization;
using CustomerSupportCrm.Api.Notifications;
using CustomerSupportCrm.Domain.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupportCrm.Api.Sla.Escalations;

/// <summary>Read-only escalation history for a ticket (Story 24), plus a manual evaluate trigger for QA and for Story 25 to call on demand. No POST beyond that trigger, no PUT/DELETE — rows are written only by <see cref="ISlaEscalationService"/> itself.</summary>
[ApiController]
[Route("api/tickets/{ticketId:guid}/escalations")]
[Authorize]
public class SlaEscalationsController(ISlaEscalationService escalationService, INotificationService notificationService) : ControllerBase
{
    [HasPermission(Permissions.Sla.EscalationsView)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TicketEscalationDto>>> List(Guid ticketId, CancellationToken cancellationToken) =>
        Ok(await escalationService.ListForTicketAsync(ticketId, cancellationToken));

    /// <summary>Manual trigger — evaluates this one ticket immediately rather than waiting for the background sweep. Idempotent, same as every other call path into the evaluator. Also fires a notification per newly-created row (Story 25), same as the background sweep does.</summary>
    [HasPermission(Permissions.Sla.EscalationsView)]
    [HttpPost("evaluate")]
    public async Task<ActionResult<IReadOnlyList<TicketEscalationDto>>> Evaluate(Guid ticketId, CancellationToken cancellationToken)
    {
        var created = await escalationService.EvaluateAsync(ticketId, now: null, cancellationToken);
        foreach (var escalation in created)
        {
            await notificationService.NotifySlaMilestoneAsync(escalation, cancellationToken);
        }
        return Ok(created);
    }
}
