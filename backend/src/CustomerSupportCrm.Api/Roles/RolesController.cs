using CustomerSupportCrm.Api.Audit;
using CustomerSupportCrm.Api.Authorization;
using CustomerSupportCrm.Domain.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupportCrm.Api.Roles;

/// <summary>List/create/update roles and replace a role's permission set. See <see cref="RolesService"/> for the business rules.</summary>
[ApiController]
[Route("api/roles")]
[Authorize]
public class RolesController(IRolesService rolesService, IAuditLogService auditLogService) : ControllerBase
{
    [HasPermission(Permissions.Roles.View)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RoleDto>>> List(CancellationToken cancellationToken) =>
        Ok(await rolesService.ListAsync(cancellationToken));

    [HasPermission(Permissions.Roles.View)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<RoleDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var role = await rolesService.GetAsync(id, cancellationToken);
        return role is null ? NotFound() : Ok(role);
    }

    [HasPermission(Permissions.Roles.View)]
    [HttpGet("{id:guid}/eligible-permissions")]
    public async Task<ActionResult<IReadOnlyList<PermissionCategoryDto>>> GetEligiblePermissions(Guid id, CancellationToken cancellationToken)
    {
        var result = await rolesService.GetEligiblePermissionsAsync(id, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }

    [HasPermission(Permissions.Roles.Create)]
    [HttpPost]
    public async Task<ActionResult<RoleDto>> Create(CreateRoleRequest request, CancellationToken cancellationToken)
    {
        var result = await rolesService.CreateAsync(request, cancellationToken);
        if (result.Outcome == RoleOperationOutcome.Success)
        {
            await auditLogService.RecordAsync(
                action: "create",
                summary: $"Role '{result.Role!.Name}' created",
                entityType: "Role",
                entityId: result.Role.Id.ToString(),
                ct: cancellationToken);
        }
        return result.Outcome switch
        {
            RoleOperationOutcome.Success => CreatedAtAction(nameof(Get), new { id = result.Role!.Id }, result.Role),
            RoleOperationOutcome.DuplicateName => DuplicateName(),
            _ => Problem(statusCode: 500),
        };
    }

    [HasPermission(Permissions.Roles.Update)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<RoleDto>> Update(Guid id, UpdateRoleRequest request, CancellationToken cancellationToken)
    {
        var result = await rolesService.UpdateAsync(id, request, cancellationToken);
        if (result.Outcome == RoleOperationOutcome.Success)
        {
            await auditLogService.RecordAsync(
                action: "update",
                summary: $"Role '{result.Role!.Name}' updated",
                entityType: "Role",
                entityId: result.Role.Id.ToString(),
                ct: cancellationToken);
        }
        return result.Outcome switch
        {
            RoleOperationOutcome.Success => Ok(result.Role),
            RoleOperationOutcome.NotFound => NotFound(),
            RoleOperationOutcome.DuplicateName => DuplicateName(),
            RoleOperationOutcome.AdministratorProtected => AdministratorProtected(),
            _ => Problem(statusCode: 500),
        };
    }

    [HasPermission(Permissions.PermissionsMgmt.Assign)]
    [HttpPut("{id:guid}/permissions")]
    public async Task<ActionResult<RoleDto>> ReplacePermissions(Guid id, ReplaceRolePermissionsRequest request, CancellationToken cancellationToken)
    {
        var result = await rolesService.ReplacePermissionsAsync(id, request.Permissions, cancellationToken);
        if (result.Outcome == RoleOperationOutcome.Success)
        {
            var permissionList = request.Permissions.Any() ? string.Join(", ", request.Permissions) : "(none)";
            await auditLogService.RecordAsync(
                action: "role.permissions.update",
                summary: $"Role '{result.Role!.Name}' permissions updated: {permissionList}",
                entityType: "Role",
                entityId: result.Role.Id.ToString(),
                metadata: new { permissions = request.Permissions },
                ct: cancellationToken);
        }
        return result.Outcome switch
        {
            RoleOperationOutcome.Success => Ok(result.Role),
            RoleOperationOutcome.NotFound => NotFound(),
            RoleOperationOutcome.AdministratorProtected => AdministratorProtected(),
            RoleOperationOutcome.UnknownPermissionCodes => BadRequest(new { error = "unknown_permission_codes", codes = result.UnknownCodes }),
            RoleOperationOutcome.PermissionsNotEligibleForRole => BadRequest(new { error = "permission_not_eligible_for_role", codes = result.UnknownCodes }),
            _ => Problem(statusCode: 500),
        };
    }

    private ObjectResult DuplicateName() => Conflict(new { error = "duplicate_role_name" });

    private ObjectResult AdministratorProtected() => Conflict(new { error = "administrator_role_protected" });
}
