namespace CustomerSupportCrm.Domain.Roles;

/// <summary>Join row granting one <see cref="Permission"/> to one <see cref="Role"/>. Composite key (RoleId, PermissionId).</summary>
public class RolePermission
{
    public Guid RoleId { get; set; }

    public Guid PermissionId { get; set; }

    public Role Role { get; set; } = default!;

    public Permission Permission { get; set; } = default!;
}
