namespace CustomerSupportCrm.Domain.Departments;

/// <summary>
/// An organisational department a user can be assigned to (e.g. "Support", "Sales"). Independent of
/// <see cref="Branches.Branch"/> — the two are separate axes with no parent/child relationship.
/// </summary>
/// <remarks>
/// A plain, settable POCO — matching <see cref="Roles.Role"/>'s style, not <see cref="Users.User"/>'s
/// (which this type has no relation to beyond being referenced by <see cref="Users.User.DepartmentId"/>).
/// Business rules (duplicate-name rejection, duplicate-code rejection, the no-hard-delete rule) live in
/// <c>CustomerSupportCrm.Api.Departments.DepartmentsService</c>, not here.
/// </remarks>
public class Department
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = default!;

    /// <summary>Upper-invariant form of <see cref="Name"/>, used for the case-insensitive unique index — same pattern as <see cref="Roles.Role.NormalizedName"/>.</summary>
    public string NormalizedName { get; set; } = default!;

    /// <summary>Optional short code (e.g. "SUP"), unique when present. Case-sensitive — the story does not ask for case-insensitive matching here.</summary>
    public string? Code { get; set; }

    /// <summary>
    /// Only <c>true</c> departments appear in user-facing pickers (<c>UserFormPage</c>'s dropdown).
    /// Setting this to <c>false</c> is the story's only "delete" — existing users keep their
    /// <see cref="Users.User.DepartmentId"/>; there is no hard-delete endpoint.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
