using CustomerSupportCrm.Domain.Roles;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupportCrm.Api.Roles;

public enum RoleOperationOutcome
{
    Success,
    NotFound,
    DuplicateName,

    /// <summary>Attempted to rename or edit the permission set of the seeded Administrator role.</summary>
    AdministratorProtected,

    UnknownPermissionCodes,

    /// <summary>A submitted code is a real catalogue entry but is outside this system role's Eligible Permissions Matrix row.</summary>
    PermissionsNotEligibleForRole,
}

public sealed record RoleResult(RoleOperationOutcome Outcome, RoleDto? Role = null, IReadOnlyList<string>? UnknownCodes = null)
{
    public static RoleResult Success(RoleDto role) => new(RoleOperationOutcome.Success, role);
    public static readonly RoleResult NotFound = new(RoleOperationOutcome.NotFound);
    public static readonly RoleResult DuplicateName = new(RoleOperationOutcome.DuplicateName);
    public static readonly RoleResult AdministratorProtected = new(RoleOperationOutcome.AdministratorProtected);
    public static RoleResult UnknownPermissions(IReadOnlyList<string> codes) => new(RoleOperationOutcome.UnknownPermissionCodes, UnknownCodes: codes);
    public static RoleResult NotEligibleForRole(IReadOnlyList<string> codes) => new(RoleOperationOutcome.PermissionsNotEligibleForRole, UnknownCodes: codes);
}

/// <summary>
/// Business rules for roles that both <see cref="RolesController"/> and the seeder-adjacent code
/// share: duplicate-name rejection, protecting the seeded Administrator role, and validating
/// permission codes against <see cref="IPermissionCatalog"/> before they ever reach the database.
/// </summary>
public interface IRolesService
{
    Task<IReadOnlyList<RoleDto>> ListAsync(CancellationToken cancellationToken = default);

    Task<RoleDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<RoleResult> CreateAsync(CreateRoleRequest request, CancellationToken cancellationToken = default);

    Task<RoleResult> UpdateAsync(Guid id, UpdateRoleRequest request, CancellationToken cancellationToken = default);

    Task<RoleResult> ReplacePermissionsAsync(Guid id, IReadOnlyList<string> permissionCodes, CancellationToken cancellationToken = default);

    /// <summary>
    /// The catalogue subset this role is eligible to hold (the Eligible Permissions Matrix row for a
    /// system role, or the full catalogue for Administrator/custom roles), grouped by category — what
    /// <c>RolePermissionsPage</c> renders instead of the raw <c>/api/permissions</c> catalogue.
    /// Returns <c>null</c> when the role does not exist.
    /// </summary>
    Task<IReadOnlyList<PermissionCategoryDto>?> GetEligiblePermissionsAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed class RolesService(CrmDbContext db, IPermissionCatalog catalog) : IRolesService
{
    /// <summary>Must match the <c>NormalizedName</c> the seeder gives the Administrator role.</summary>
    public const string AdministratorNormalizedName = "ADMINISTRATOR";

    public async Task<IReadOnlyList<RoleDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        var roles = await LoadWithPermissions().ToListAsync(cancellationToken);
        return roles.Select(ToDto).ToList();
    }

    public async Task<RoleDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var role = await LoadWithPermissions()
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        return role is null ? null : ToDto(role);
    }

    public async Task<RoleResult> CreateAsync(CreateRoleRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        var normalized = name.ToUpperInvariant();

        if (await db.Roles.AnyAsync(r => r.NormalizedName == normalized, cancellationToken))
        {
            return RoleResult.DuplicateName;
        }

        var role = new Role
        {
            Name = name,
            NormalizedName = normalized,
            Description = NormalizeDescription(request.Description),
            IsSystem = false,
        };

        db.Roles.Add(role);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueNameViolation(ex))
        {
            // A concurrent create with the same normalized name raced past the check above and lost
            // to the unique index — same defense-in-depth pattern as UsersController.
            return RoleResult.DuplicateName;
        }

        return RoleResult.Success(new RoleDto(role.Id, role.Name, role.Description, role.IsSystem, []));
    }

    public async Task<RoleResult> UpdateAsync(Guid id, UpdateRoleRequest request, CancellationToken cancellationToken = default)
    {
        var role = await db.Roles.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (role is null)
        {
            return RoleResult.NotFound;
        }

        var name = request.Name.Trim();
        var normalized = name.ToUpperInvariant();

        if (role.NormalizedName == AdministratorNormalizedName && normalized != AdministratorNormalizedName)
        {
            return RoleResult.AdministratorProtected;
        }

        if (normalized != role.NormalizedName &&
            await db.Roles.AnyAsync(r => r.Id != id && r.NormalizedName == normalized, cancellationToken))
        {
            return RoleResult.DuplicateName;
        }

        role.Name = name;
        role.NormalizedName = normalized;
        role.Description = NormalizeDescription(request.Description);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueNameViolation(ex))
        {
            return RoleResult.DuplicateName;
        }

        var permissionCodes = await db.RolePermissions
            .Where(rp => rp.RoleId == role.Id)
            .Select(rp => rp.Permission.Code)
            .OrderBy(code => code)
            .ToListAsync(cancellationToken);

        return RoleResult.Success(new RoleDto(role.Id, role.Name, role.Description, role.IsSystem, permissionCodes));
    }

    public async Task<RoleResult> ReplacePermissionsAsync(Guid id, IReadOnlyList<string> permissionCodes, CancellationToken cancellationToken = default)
    {
        var role = await db.Roles
            .Include(r => r.RolePermissions)
            .SingleOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (role is null)
        {
            return RoleResult.NotFound;
        }

        if (role.NormalizedName == AdministratorNormalizedName)
        {
            // Administrator always has every permission — see DbSeeder, which re-syncs this on every
            // startup. Silently accepting a partial set here would create a window where the seeded
            // administrator loses access until the next restart.
            return RoleResult.AdministratorProtected;
        }

        var requested = permissionCodes.Distinct(StringComparer.Ordinal).ToList();
        var unknown = requested.Where(code => !catalog.IsValidCode(code)).ToList();
        if (unknown.Count > 0)
        {
            return RoleResult.UnknownPermissions(unknown);
        }

        // Defense in depth: the Roles UI only ever renders/submits a system role's eligible subset
        // (see GetEligiblePermissionsAsync below), but a caller hitting the API directly could submit
        // a real catalogue code outside it — e.g. "roles.view" for the Customer role. Reject those so
        // the Eligible Permissions Matrix is an enforced rule, not just a UI hide.
        if (role.IsSystem)
        {
            var eligibleCodes = catalog.EligibleFor(role).Select(p => p.Code).ToHashSet(StringComparer.Ordinal);
            var ineligible = requested.Where(code => !eligibleCodes.Contains(code)).ToList();
            if (ineligible.Count > 0)
            {
                return RoleResult.NotEligibleForRole(ineligible);
            }
        }

        var permissionIds = await db.Permissions
            .Where(p => requested.Contains(p.Code))
            .Select(p => p.Id)
            .ToListAsync(cancellationToken);

        // Clear-and-reinsert inside one SaveChangesAsync (one implicit transaction) so a concurrent
        // reader never observes a partially-replaced set.
        db.RolePermissions.RemoveRange(role.RolePermissions);
        foreach (var permissionId in permissionIds)
        {
            db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permissionId });
        }

        await db.SaveChangesAsync(cancellationToken);

        return RoleResult.Success(new RoleDto(role.Id, role.Name, role.Description, role.IsSystem, requested.OrderBy(c => c).ToList()));
    }

    public async Task<IReadOnlyList<PermissionCategoryDto>?> GetEligiblePermissionsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var role = await db.Roles.AsNoTracking().SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (role is null)
        {
            return null;
        }

        return catalog.EligibleFor(role)
            .GroupBy(p => p.Category)
            .OrderBy(g => g.Key)
            .Select(g => new PermissionCategoryDto(g.Key, g.OrderBy(p => p.Code).ToList()))
            .ToList();
    }

    private IQueryable<Role> LoadWithPermissions() =>
        db.Roles.AsNoTracking().Include(r => r.RolePermissions).ThenInclude(rp => rp.Permission);

    private static RoleDto ToDto(Role role) => new(
        role.Id,
        role.Name,
        role.Description,
        role.IsSystem,
        role.RolePermissions.Select(rp => rp.Permission.Code).OrderBy(c => c).ToList());

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    // Same pattern as UsersController.IsUniqueEmailViolation: a synchronous check on the SQL error
    // number (2601/2627), not a second DB round-trip — `catch ... when (await ...)` is not legal C#.
    private static bool IsUniqueNameViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
}
