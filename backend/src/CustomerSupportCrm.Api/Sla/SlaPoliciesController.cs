using CustomerSupportCrm.Api.Audit;
using CustomerSupportCrm.Api.Authorization;
using CustomerSupportCrm.Domain.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupportCrm.Api.Sla;

/// <summary>
/// Story 22: minimal admin surface for the SLA policies <see cref="ISlaService"/> applies at ticket
/// creation — list + update only, gated on the same <c>system</c> permissions as System Settings
/// (mirrors <c>SystemSettingsController</c>). No create/delete: <c>DbSeeder</c>'s seeded "Default SLA"
/// row is the source of truth for the policy set in this story.
/// </summary>
[ApiController]
[Route("api/sla/policies")]
[Authorize]
public class SlaPoliciesController(ISlaPoliciesService policiesService, IAuditLogService auditLogService) : ControllerBase
{
    [HasPermission(Permissions.SystemConfig.View)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<SlaPolicyDto>>> List(CancellationToken cancellationToken) =>
        Ok(await policiesService.ListActiveAsync(cancellationToken));

    [HasPermission(Permissions.SystemConfig.Update)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SlaPolicyDto>> Update(Guid id, UpdateSlaPolicyRequest request, CancellationToken cancellationToken)
    {
        var result = await policiesService.UpdateAsync(id, request, cancellationToken);
        if (result.Outcome == SlaPolicyOperationOutcome.Success)
        {
            await auditLogService.RecordAsync(
                action: "update",
                summary: $"SLA policy '{result.Policy!.Name}' updated",
                entityType: "SlaPolicy",
                entityId: result.Policy.Id.ToString(),
                ct: cancellationToken);
        }

        return result.Outcome switch
        {
            SlaPolicyOperationOutcome.Success => Ok(result.Policy),
            SlaPolicyOperationOutcome.NotFound => NotFound(),
            SlaPolicyOperationOutcome.InvalidFirstResponseMinutes => BadRequest(new { error = "invalid_first_response_minutes" }),
            SlaPolicyOperationOutcome.InvalidResolutionMinutes => BadRequest(new { error = "invalid_resolution_minutes" }),
            SlaPolicyOperationOutcome.DuplicateActivePolicy => Conflict(new { error = "duplicate_active_policy" }),
            _ => Problem(statusCode: 500),
        };
    }
}
