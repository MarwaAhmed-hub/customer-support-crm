using CustomerSupportCrm.Domain.Users;

namespace CustomerSupportCrm.Domain.Roles;

/// <summary>Join row assigning one <see cref="Role"/> to one <see cref="User"/>. Composite key (UserId, RoleId).</summary>
public class UserRole
{
    public Guid UserId { get; set; }

    public Guid RoleId { get; set; }

    public DateTimeOffset AssignedAt { get; set; } = DateTimeOffset.UtcNow;

    public User User { get; set; } = default!;

    public Role Role { get; set; } = default!;
}
