namespace CustomerSupportCrm.Domain.Branches;

/// <summary>
/// A physical/organisational branch a user can be assigned to (e.g. "Cairo", "Dubai"). Independent of
/// <see cref="Departments.Department"/> — the two are separate axes with no parent/child relationship.
/// </summary>
/// <remarks>Same shape and rationale as <see cref="Departments.Department"/> — see its remarks.</remarks>
public class Branch
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = default!;

    /// <summary>Upper-invariant form of <see cref="Name"/>, used for the case-insensitive unique index — same pattern as <see cref="Roles.Role.NormalizedName"/>.</summary>
    public string NormalizedName { get; set; } = default!;

    /// <summary>Optional short code (e.g. "CAI"), unique when present. Case-sensitive — the story does not ask for case-insensitive matching here.</summary>
    public string? Code { get; set; }

    /// <summary>
    /// Only <c>true</c> branches appear in user-facing pickers (<c>UserFormPage</c>'s dropdown).
    /// Setting this to <c>false</c> is the story's only "delete" — existing users keep their
    /// <see cref="Users.User.BranchId"/>; there is no hard-delete endpoint.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
