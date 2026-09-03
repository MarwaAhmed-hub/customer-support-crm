using CustomerSupportCrm.Api.Audit;
using CustomerSupportCrm.Api.Authorization;
using CustomerSupportCrm.Domain.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupportCrm.Api.Branches;

/// <summary>List/create/update branches. No delete endpoint — deactivate via <see cref="Update"/> instead. See <see cref="BranchesService"/> for the business rules.</summary>
[ApiController]
[Route("api/branches")]
[Authorize]
public class BranchesController(IBranchesService branchesService, IAuditLogService auditLogService) : ControllerBase
{
    [HasPermission(Permissions.Branches.View)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<BranchDto>>> List(
        [FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        Ok(await branchesService.ListAsync(includeInactive, cancellationToken));

    [HasPermission(Permissions.Branches.View)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<BranchDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var branch = await branchesService.GetAsync(id, cancellationToken);
        return branch is null ? NotFound() : Ok(branch);
    }

    [HasPermission(Permissions.Branches.Create)]
    [HttpPost]
    public async Task<ActionResult<BranchDto>> Create(CreateBranchRequest request, CancellationToken cancellationToken)
    {
        var result = await branchesService.CreateAsync(request, cancellationToken);
        if (result.Outcome == BranchOperationOutcome.Success)
        {
            await auditLogService.RecordAsync(
                action: "create",
                summary: $"Branch '{result.Branch!.Name}' created",
                entityType: "Branch",
                entityId: result.Branch.Id.ToString(),
                ct: cancellationToken);
        }
        return result.Outcome switch
        {
            BranchOperationOutcome.Success => CreatedAtAction(nameof(Get), new { id = result.Branch!.Id }, result.Branch),
            BranchOperationOutcome.InvalidName => InvalidName(),
            BranchOperationOutcome.DuplicateName => DuplicateName(),
            BranchOperationOutcome.DuplicateCode => DuplicateCode(),
            _ => Problem(statusCode: 500),
        };
    }

    [HasPermission(Permissions.Branches.Update)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<BranchDto>> Update(Guid id, UpdateBranchRequest request, CancellationToken cancellationToken)
    {
        var result = await branchesService.UpdateAsync(id, request, cancellationToken);
        if (result.Outcome == BranchOperationOutcome.Success)
        {
            await auditLogService.RecordAsync(
                action: "update",
                summary: $"Branch '{result.Branch!.Name}' updated",
                entityType: "Branch",
                entityId: result.Branch.Id.ToString(),
                ct: cancellationToken);
        }
        return result.Outcome switch
        {
            BranchOperationOutcome.Success => Ok(result.Branch),
            BranchOperationOutcome.NotFound => NotFound(),
            BranchOperationOutcome.InvalidName => InvalidName(),
            BranchOperationOutcome.DuplicateName => DuplicateName(),
            BranchOperationOutcome.DuplicateCode => DuplicateCode(),
            _ => Problem(statusCode: 500),
        };
    }

    private BadRequestObjectResult InvalidName() => BadRequest(new { error = "invalid_name" });

    private ObjectResult DuplicateName() => Conflict(new { error = "duplicate_branch_name" });

    private ObjectResult DuplicateCode() => Conflict(new { error = "duplicate_branch_code" });
}
