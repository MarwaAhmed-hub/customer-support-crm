using Microsoft.EntityFrameworkCore;

namespace CustomerSupportCrm.Infrastructure.Persistence;

/// <summary>Computes a user's effective permissions: the union of permissions across every role assigned to them.</summary>
public interface IUserPermissionsQuery
{
    Task<IReadOnlyList<string>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed class UserPermissionsQuery(CrmDbContext db) : IUserPermissionsQuery
{
    public async Task<IReadOnlyList<string>> GetForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var codes = await db.UserRoles
            .AsNoTracking()
            .Where(ur => ur.UserId == userId)
            .SelectMany(ur => ur.Role.RolePermissions)
            .Select(rp => rp.Permission.Code)
            .Distinct()
            .OrderBy(code => code)
            .ToListAsync(cancellationToken);

        return codes;
    }
}
