namespace CustomerSupportCrm.Domain.Roles;

/// <summary>
/// A named collection of permissions. A user's effective permissions are the union of permissions
/// across every role assigned to them (<see cref="UserRole"/>).
/// </summary>
/// <remarks>
/// Deliberately a plain, settable POCO — matching <see cref="Users.User"/>'s style. Business rules
/// (duplicate-name rejection, protecting the seeded "Administrator" role from rename, unknown
/// permission codes) live in <c>CustomerSupportCrm.Api.Roles.RolesService</c>, not here.
/// </remarks>
public class Role
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = default!;

    /// <summary>Upper-invariant form of <see cref="Name"/>, used for the case-insensitive unique index.</summary>
    public string NormalizedName { get; set; } = default!;

    public string? Description { get; set; }

    /// <summary>True for the four seeded roles (Administrator, Manager, Agent, Customer).</summary>
    public bool IsSystem { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();

    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
