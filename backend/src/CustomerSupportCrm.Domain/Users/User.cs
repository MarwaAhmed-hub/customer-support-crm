using CustomerSupportCrm.Domain.Roles;

namespace CustomerSupportCrm.Domain.Users;

/// <summary>
/// A CRM user account.
/// </summary>
/// <remarks>
/// Story 02 (users-management) extends this entity additively. To keep that possible, this class
/// deliberately stays a plain, public, parameterless-constructible class with settable properties:
/// no <c>sealed</c>, no <c>record</c>, no private setters, no factory-only construction, and no
/// business rules. Validation lives in the API layer; persistence configuration lives in
/// <c>CrmDbContext.OnModelCreating</c>, not in attributes here.
///
/// Fields that later stories will add (UpdatedAt, LastLoginAt, soft-delete flags) are intentionally
/// absent — each is a one-column additive migration in the story that needs it. <see cref="UserRoles"/>
/// (Story 03) is the first navigation property, added the same way. <see cref="DepartmentId"/> /
/// <see cref="BranchId"/> (Story 04) deliberately have no navigation property back to the
/// Department/Branch entity — nothing here needs to traverse from a loaded <c>User</c> to its
/// department/branch row; a caller that needs the name looks it up directly (see
/// <c>CustomerSupportCrm.Api.Users.UsersController</c>).
/// </remarks>
public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Stored lower-cased and trimmed via <see cref="EmailNormalizer"/>. Unique.</summary>
    public string Email { get; set; } = default!;

    public string DisplayName { get; set; } = default!;

    public string PasswordHash { get; set; } = default!;

    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Legacy placeholder flag from Story 02, kept for backward compatibility only. New
    /// authorization code must not branch on it — see <see cref="UserRoles"/> and
    /// <c>CustomerSupportCrm.Api.Auth.ClaimsPrincipalExtensions.HasPermission</c> instead.
    /// </summary>
    /// <remarks>
    /// Story 03 treats this as "member of the seeded Administrator role" for seeding purposes only
    /// (see <c>DbSeeder</c>) and keeps emitting the same "role": "Admin" JWT claim it always has, so
    /// already-issued tokens and the <c>[Authorize(Policy = "Admin")]</c> checks predating this
    /// story keep working unchanged. It is not dropped in this story; a follow-up story can remove
    /// it once every environment has migrated to roles/permissions.
    /// </remarks>
    public bool IsAdmin { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>The roles assigned to this user. Effective permissions are the union across all of them.</summary>
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();

    /// <summary>Optional; <c>null</c> means unassigned. Must reference an existing, active department — enforced in <c>UsersController</c>, not here.</summary>
    public Guid? DepartmentId { get; set; }

    /// <summary>Optional; <c>null</c> means unassigned. Must reference an existing, active branch — enforced in <c>UsersController</c>, not here.</summary>
    public Guid? BranchId { get; set; }

    /// <summary>
    /// Story 23: optional cap on this agent's concurrent active (non-Resolved, non-Closed) ticket
    /// count — <c>null</c> means unlimited. Consulted only by
    /// <c>CustomerSupportCrm.Api.Tickets.Assignment.TicketAssignmentService</c>'s automatic-assignment
    /// eligibility check; manual assignment (Story 12) never enforces it.
    /// </summary>
    public int? MaxActiveTickets { get; set; }
}
