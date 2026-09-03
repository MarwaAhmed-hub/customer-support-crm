namespace CustomerSupportCrm.Domain.Roles;

/// <summary>
/// One row of the permission catalogue (<c>CustomerSupportCrm.Api.Authorization.Permissions</c> is
/// the source of truth for which codes exist; this table mirrors it so roles can reference permission
/// ids with a real foreign key instead of a bare string).
/// </summary>
public class Permission
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Stable identifier, e.g. "users.view". Never reused once shipped.</summary>
    public string Code { get; set; } = default!;

    /// <summary>Grouping used by the frontend's permission picker, e.g. "users".</summary>
    public string Category { get; set; } = default!;

    public string DisplayName { get; set; } = default!;

    public string? Description { get; set; }
}
