using CustomerSupportCrm.Api.Roles;
using CustomerSupportCrm.Domain.Roles;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupportCrm.Api.Users;

public sealed record UserRoleDto(Guid Id, string Name);

public enum UserRoleOperationOutcome
{
    Success,
    UserNotFound,
    RoleNotFound,

    /// <summary>Would remove the Administrator role from the last user who has it.</summary>
    LastAdministrator,
}

/// <summary>
/// Assigns and removes roles for a user. Kept separate from <see cref="Roles.RolesService"/> because
/// it owns a different invariant — at least one user must always hold the Administrator role — rather
/// than the role-catalogue rules <c>RolesService</c> owns.
/// </summary>
public interface IUserRolesService
{
    Task<IReadOnlyList<UserRoleDto>?> GetRolesAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<UserRoleOperationOutcome> AssignAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default);

    Task<UserRoleOperationOutcome> RemoveAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default);
}

public sealed class UserRolesService(CrmDbContext db) : IUserRolesService
{
    /// <summary>Null return means the user itself was not found (distinct from "found, zero roles").</summary>
    public async Task<IReadOnlyList<UserRoleDto>?> GetRolesAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        if (!await db.Users.AnyAsync(u => u.Id == userId, cancellationToken))
        {
            return null;
        }

        return await db.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .OrderBy(ur => ur.Role.Name)
            .Select(ur => new UserRoleDto(ur.RoleId, ur.Role.Name))
            .ToListAsync(cancellationToken);
    }

    public async Task<UserRoleOperationOutcome> AssignAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default)
    {
        if (!await db.Users.AnyAsync(u => u.Id == userId, cancellationToken))
        {
            return UserRoleOperationOutcome.UserNotFound;
        }

        if (!await db.Roles.AnyAsync(r => r.Id == roleId, cancellationToken))
        {
            return UserRoleOperationOutcome.RoleNotFound;
        }

        // Idempotent: assigning a role the user already has is a no-op success, not a conflict — the
        // caller asked for an end state ("this user has this role"), not a strict insert.
        if (await db.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == roleId, cancellationToken))
        {
            return UserRoleOperationOutcome.Success;
        }

        db.UserRoles.Add(new UserRole { UserId = userId, RoleId = roleId });
        await db.SaveChangesAsync(cancellationToken);

        return UserRoleOperationOutcome.Success;
    }

    public async Task<UserRoleOperationOutcome> RemoveAsync(Guid userId, Guid roleId, CancellationToken cancellationToken = default)
    {
        var link = await db.UserRoles.SingleOrDefaultAsync(ur => ur.UserId == userId && ur.RoleId == roleId, cancellationToken);
        if (link is null)
        {
            // Removing a role the user never had is treated the same as removing one that's already
            // gone: a no-op success, matching AssignAsync's idempotency.
            var userExists = await db.Users.AnyAsync(u => u.Id == userId, cancellationToken);
            if (!userExists)
            {
                return UserRoleOperationOutcome.UserNotFound;
            }

            var roleExists = await db.Roles.AnyAsync(r => r.Id == roleId, cancellationToken);
            return roleExists ? UserRoleOperationOutcome.Success : UserRoleOperationOutcome.RoleNotFound;
        }

        var role = await db.Roles.SingleAsync(r => r.Id == roleId, cancellationToken);
        if (role.NormalizedName == RolesService.AdministratorNormalizedName)
        {
            var administratorCount = await db.UserRoles.CountAsync(ur => ur.RoleId == roleId, cancellationToken);
            if (administratorCount <= 1)
            {
                return UserRoleOperationOutcome.LastAdministrator;
            }
        }

        db.UserRoles.Remove(link);
        await db.SaveChangesAsync(cancellationToken);

        return UserRoleOperationOutcome.Success;
    }
}
