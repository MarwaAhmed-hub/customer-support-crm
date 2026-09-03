using CustomerSupportCrm.Api.Authorization;
using CustomerSupportCrm.Domain.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupportCrm.Api.Audit;

[ApiController]
[Route("api/audit-logs")]
[Authorize]
public class AuditLogsController(IAuditLogService auditLogService) : ControllerBase
{
    [HasPermission(Permissions.AuditLogs.View)]
    [HttpGet]
    public async Task<ActionResult<AuditLogPageDto>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? action = null,
        [FromQuery] string? entityType = null,
        [FromQuery] Guid? actorUserId = null,
        [FromQuery] DateTime? fromUtc = null,
        [FromQuery] DateTime? toUtc = null,
        CancellationToken cancellationToken = default)
    {
        var query = new AuditLogQuery(
            Page: page,
            PageSize: pageSize,
            Action: action,
            EntityType: entityType,
            ActorUserId: actorUserId,
            FromUtc: fromUtc,
            ToUtc: toUtc);

        var result = await auditLogService.QueryAsync(query, cancellationToken);
        return Ok(result);
    }
}
