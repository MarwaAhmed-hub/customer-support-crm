using CustomerSupportCrm.Api.Audit;
using CustomerSupportCrm.Api.Authorization;
using CustomerSupportCrm.Api.Roles;
using CustomerSupportCrm.Domain.Roles;
using CustomerSupportCrm.Domain.Users;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupportCrm.Api.Users;

/// <summary>
/// CRUD for CRM user accounts, plus role assignment: list/search, detail, create, update,
/// activate/deactivate, and view/assign/remove the user's roles.
/// </summary>
/// <remarks>
/// Story 03: authorization moved from a single hard-coded "Admin" policy check to per-action
/// permission policies (<see cref="HasPermissionAttribute"/>) — see the TODO(roles-permissions)
/// comment this replaces in <see cref="Domain.Users.User.IsAdmin"/>.
///
/// This controller talks to <see cref="CrmDbContext"/> directly for the CRUD it already owned
/// (mirroring <see cref="Auth.AuthController"/>'s style — there is no service-layer abstraction for
/// plain user CRUD), but delegates role assignment to <see cref="IUserRolesService"/>, which owns
/// the last-administrator safety invariant.
/// </remarks>
[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController(
    CrmDbContext db,
    IPasswordHasher<User> passwordHasher,
    IUserRolesService userRolesService,
    IAuditLogService auditLogService) : ControllerBase
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    [HasPermission(Permissions.Users.View)]
    [HttpGet]
    public async Task<ActionResult<PagedResult<UserListItemDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = DefaultPageSize,
        [FromQuery] string? search = null,
        [FromQuery] Guid? departmentId = null,
        [FromQuery] Guid? branchId = null,
        CancellationToken cancellationToken = default)
    {
        // Out-of-range paging is clamped rather than rejected: it is a display concern, not a
        // client error.
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

        var query = db.Users.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            // Contains() parameterises the search term, so SQL LIKE wildcards typed by the caller
            // (%, _) are matched literally rather than interpreted.
            var term = search.Trim();
            query = query.Where(u => u.Email.Contains(term) || u.DisplayName.Contains(term));
        }

        if (departmentId is not null)
        {
            query = query.Where(u => u.DepartmentId == departmentId);
        }

        if (branchId is not null)
        {
            query = query.Where(u => u.BranchId == branchId);
        }

        var total = await query.CountAsync(cancellationToken);

        var rows = await query
            .OrderBy(u => u.Email)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(u => new { u.Id, u.Email, u.DisplayName, u.IsActive, u.DepartmentId, u.BranchId })
            .ToListAsync(cancellationToken);

        // No navigation property from User to Department/Branch (see the remarks on
        // User.DepartmentId/BranchId), so names are denormalised here with two small batch lookups
        // instead of a per-row query.
        var (departmentNames, branchNames) = await LoadNamesAsync(
            rows.Select(r => r.DepartmentId), rows.Select(r => r.BranchId), cancellationToken);

        var items = rows
            .Select(r => new UserListItemDto(
                r.Id, r.Email, r.DisplayName, r.IsActive,
                r.DepartmentId, NameOrNull(departmentNames, r.DepartmentId),
                r.BranchId, NameOrNull(branchNames, r.BranchId)))
            .ToList();

        return Ok(new PagedResult<UserListItemDto>(items, page, pageSize, total));
    }

    [HasPermission(Permissions.Users.View)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<UserDetailDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        return Ok(await ToDetailDtoAsync(user, cancellationToken));
    }

    [HasPermission(Permissions.Users.Create)]
    [HttpPost]
    public async Task<ActionResult<UserDetailDto>> Create(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var email = EmailNormalizer.Normalize(request.Email);

        if (await db.Users.AnyAsync(u => u.Email == email, cancellationToken))
        {
            return DuplicateEmail();
        }

        var departmentError = await ValidateDepartmentAsync(request.DepartmentId, cancellationToken);
        if (departmentError is not null)
        {
            return departmentError;
        }

        var branchError = await ValidateBranchAsync(request.BranchId, cancellationToken);
        if (branchError is not null)
        {
            return branchError;
        }

        var user = new User
        {
            Email = email,
            DisplayName = request.DisplayName.Trim(),
            IsActive = true,
            DepartmentId = request.DepartmentId,
            BranchId = request.BranchId,
        };
        // Reuses the same hasher and PasswordHash column as the login path (AuthController); an
        // admin-supplied temporary password with no forced-change flag — self-service reset is out
        // of scope for this story.
        user.PasswordHash = passwordHasher.HashPassword(user, request.Password);

        db.Users.Add(user);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueEmailViolation(ex))
        {
            // Defense in depth: a concurrent create with the same normalized email raced past the
            // check above and lost to the unique index on Users.Email.
            return DuplicateEmail();
        }

        await auditLogService.RecordAsync(
            action: "create",
            summary: $"User {user.Email} created",
            entityType: "User",
            entityId: user.Id.ToString(),
            ct: cancellationToken);

        var dto = await ToDetailDtoAsync(user, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = user.Id }, dto);
    }

    [HasPermission(Permissions.Users.Update)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<UserDetailDto>> Update(Guid id, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        var email = EmailNormalizer.Normalize(request.Email);

        if (await db.Users.AnyAsync(u => u.Id != id && u.Email == email, cancellationToken))
        {
            return DuplicateEmail();
        }

        // Re-validate only a department/branch the caller is actually *changing*. A user's existing
        // assignment can outlive the department/branch being deactivated (see
        // DepartmentsService/BranchesService — deactivation never touches Users), and an edit to,
        // say, just the display name must not be rejected as a side effect of that.
        if (request.DepartmentId != user.DepartmentId)
        {
            var departmentError = await ValidateDepartmentAsync(request.DepartmentId, cancellationToken);
            if (departmentError is not null)
            {
                return departmentError;
            }
        }

        if (request.BranchId != user.BranchId)
        {
            var branchError = await ValidateBranchAsync(request.BranchId, cancellationToken);
            if (branchError is not null)
            {
                return branchError;
            }
        }

        user.Email = email;
        user.DisplayName = request.DisplayName.Trim();
        user.DepartmentId = request.DepartmentId;
        user.BranchId = request.BranchId;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueEmailViolation(ex))
        {
            return DuplicateEmail();
        }

        await auditLogService.RecordAsync(
            action: "update",
            summary: $"User {user.Email} updated",
            entityType: "User",
            entityId: user.Id.ToString(),
            ct: cancellationToken);

        return Ok(await ToDetailDtoAsync(user, cancellationToken));
    }

    [HasPermission(Permissions.Users.Update)]
    [HttpPost("{id:guid}/activate")]
    public Task<ActionResult<UserDetailDto>> Activate(Guid id, CancellationToken cancellationToken) =>
        SetActiveAsync(id, isActive: true, cancellationToken);

    [HasPermission(Permissions.Users.Update)]
    [HttpPost("{id:guid}/deactivate")]
    public Task<ActionResult<UserDetailDto>> Deactivate(Guid id, CancellationToken cancellationToken) =>
        SetActiveAsync(id, isActive: false, cancellationToken);

    [HasPermission(Permissions.Users.View)]
    [HttpGet("{id:guid}/roles")]
    public async Task<ActionResult<IReadOnlyList<UserRoleDto>>> GetRoles(Guid id, CancellationToken cancellationToken)
    {
        var roles = await userRolesService.GetRolesAsync(id, cancellationToken);
        return roles is null ? NotFound() : Ok(roles);
    }

    [HasPermission(Permissions.PermissionsMgmt.Assign)]
    [HttpPost("{id:guid}/roles")]
    public async Task<IActionResult> AssignRole(Guid id, AssignRoleRequest request, CancellationToken cancellationToken)
    {
        var outcome = await userRolesService.AssignAsync(id, request.RoleId, cancellationToken);
        if (outcome == UserRoleOperationOutcome.Success)
        {
            // Get user and role info for audit log
            var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, cancellationToken);
            var role = await db.Roles.AsNoTracking().SingleOrDefaultAsync(r => r.Id == request.RoleId, cancellationToken);

            if (user is not null && role is not null)
            {
                await auditLogService.RecordAsync(
                    action: "user.role.assign",
                    summary: $"Role '{role.Name}' assigned to user {user.Email}",
                    entityType: "User",
                    entityId: user.Id.ToString(),
                    metadata: new { roleId = request.RoleId, roleName = role.Name },
                    ct: cancellationToken);
            }
        }
        return outcome switch
        {
            UserRoleOperationOutcome.Success => Ok(await userRolesService.GetRolesAsync(id, cancellationToken)),
            UserRoleOperationOutcome.UserNotFound or UserRoleOperationOutcome.RoleNotFound => NotFound(),
            _ => Problem(statusCode: 500),
        };
    }

    [HasPermission(Permissions.PermissionsMgmt.Assign)]
    [HttpDelete("{id:guid}/roles/{roleId:guid}")]
    public async Task<IActionResult> RemoveRole(Guid id, Guid roleId, CancellationToken cancellationToken)
    {
        var outcome = await userRolesService.RemoveAsync(id, roleId, cancellationToken);
        if (outcome == UserRoleOperationOutcome.Success)
        {
            // Get user and role info for audit log
            var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(u => u.Id == id, cancellationToken);
            var role = await db.Roles.AsNoTracking().SingleOrDefaultAsync(r => r.Id == roleId, cancellationToken);

            if (user is not null && role is not null)
            {
                await auditLogService.RecordAsync(
                    action: "user.role.remove",
                    summary: $"Role '{role.Name}' removed from user {user.Email}",
                    entityType: "User",
                    entityId: user.Id.ToString(),
                    metadata: new { roleId = roleId, roleName = role.Name },
                    ct: cancellationToken);
            }
        }
        return outcome switch
        {
            UserRoleOperationOutcome.Success => Ok(await userRolesService.GetRolesAsync(id, cancellationToken)),
            UserRoleOperationOutcome.UserNotFound or UserRoleOperationOutcome.RoleNotFound => NotFound(),
            UserRoleOperationOutcome.LastAdministrator =>
                BadRequest(new { error = "last_administrator" }),
            _ => Problem(statusCode: 500),
        };
    }

    /// <summary>
    /// Deactivating any account — including the caller's own — is allowed here and succeeds
    /// immediately. It does not force an ongoing session to end mid-request: the still-valid JWT
    /// keeps working until <see cref="Auth.AuthController.Me"/> or the next login is checked, both
    /// of which already reject <c>IsActive == false</c> (Story 01).
    /// </summary>
    private async Task<ActionResult<UserDetailDto>> SetActiveAsync(Guid id, bool isActive, CancellationToken cancellationToken)
    {
        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        user.IsActive = isActive;
        await db.SaveChangesAsync(cancellationToken);

        await auditLogService.RecordAsync(
            action: isActive ? "activate" : "deactivate",
            summary: $"User {user.Email} {(isActive ? "activated" : "deactivated")}",
            entityType: "User",
            entityId: user.Id.ToString(),
            ct: cancellationToken);

        return Ok(await ToDetailDtoAsync(user, cancellationToken));
    }

    private async Task<UserDetailDto> ToDetailDtoAsync(User user, CancellationToken cancellationToken)
    {
        var roles = await userRolesService.GetRolesAsync(user.Id, cancellationToken) ?? [];

        string? departmentName = null;
        if (user.DepartmentId is { } departmentId)
        {
            departmentName = await db.Departments.AsNoTracking()
                .Where(d => d.Id == departmentId)
                .Select(d => d.Name)
                .SingleOrDefaultAsync(cancellationToken);
        }

        string? branchName = null;
        if (user.BranchId is { } branchId)
        {
            branchName = await db.Branches.AsNoTracking()
                .Where(b => b.Id == branchId)
                .Select(b => b.Name)
                .SingleOrDefaultAsync(cancellationToken);
        }

        return new UserDetailDto(
            user.Id, user.Email, user.DisplayName, user.IsActive, user.CreatedAt, roles,
            user.DepartmentId, departmentName, user.BranchId, branchName);
    }

    /// <summary>
    /// A provided <c>DepartmentId</c> must reference an existing, <b>active</b> department — an admin
    /// cannot newly assign a user to one that has been deactivated, nor to an id that doesn't exist.
    /// <c>null</c> is always allowed (unassigned). Returns <c>null</c> when valid, otherwise the
    /// <c>400</c> response to return directly. Callers only invoke this for an id that is actually
    /// changing — see the comment in <see cref="Update"/>.
    /// </summary>
    private async Task<BadRequestObjectResult?> ValidateDepartmentAsync(Guid? departmentId, CancellationToken cancellationToken) =>
        departmentId is { } id && !await db.Departments.AnyAsync(d => d.Id == id && d.IsActive, cancellationToken)
            ? BadRequest(new { error = "invalid_department" })
            : null;

    /// <summary>Branch equivalent of <see cref="ValidateDepartmentAsync"/>.</summary>
    private async Task<BadRequestObjectResult?> ValidateBranchAsync(Guid? branchId, CancellationToken cancellationToken) =>
        branchId is { } id && !await db.Branches.AnyAsync(b => b.Id == id && b.IsActive, cancellationToken)
            ? BadRequest(new { error = "invalid_branch" })
            : null;

    /// <summary>Batch name lookup for <see cref="List"/> — one query per entity instead of one per row.</summary>
    private async Task<(Dictionary<Guid, string> Departments, Dictionary<Guid, string> Branches)> LoadNamesAsync(
        IEnumerable<Guid?> departmentIds, IEnumerable<Guid?> branchIds, CancellationToken cancellationToken)
    {
        var distinctDepartmentIds = departmentIds.Where(id => id is not null).Select(id => id!.Value).Distinct().ToList();
        var distinctBranchIds = branchIds.Where(id => id is not null).Select(id => id!.Value).Distinct().ToList();

        var departments = distinctDepartmentIds.Count == 0
            ? []
            : await db.Departments.AsNoTracking()
                .Where(d => distinctDepartmentIds.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, d => d.Name, cancellationToken);

        var branches = distinctBranchIds.Count == 0
            ? []
            : await db.Branches.AsNoTracking()
                .Where(b => distinctBranchIds.Contains(b.Id))
                .ToDictionaryAsync(b => b.Id, b => b.Name, cancellationToken);

        return (departments, branches);
    }

    private static string? NameOrNull(Dictionary<Guid, string> names, Guid? id) =>
        id is { } value && names.TryGetValue(value, out var name) ? name : null;

    private ObjectResult DuplicateEmail() => Conflict(new { error = "duplicate_email" });

    private static bool IsUniqueEmailViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
}
