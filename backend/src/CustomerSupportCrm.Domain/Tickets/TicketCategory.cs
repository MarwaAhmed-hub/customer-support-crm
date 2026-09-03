namespace CustomerSupportCrm.Domain.Tickets;

/// <summary>
/// Master data for classifying tickets (e.g. "Technical Support", "Billing"). Independent of
/// <see cref="TicketPriority"/> — the two are separate axes with no parent/child relationship, and
/// independent of the <c>Ticket</c> entity itself (a later story).
/// </summary>
/// <remarks>Same shape and rationale as <see cref="Departments.Department"/> — see its remarks.</remarks>
public class TicketCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = default!;

    /// <summary>Upper-invariant form of <see cref="Name"/>, used for the case-insensitive unique index — same pattern as <see cref="Departments.Department.NormalizedName"/>.</summary>
    public string NormalizedName { get; set; } = default!;

    public string? Description { get; set; }

    /// <summary>Only <c>true</c> categories appear in user-facing pickers on a ticket form (a later story). Setting this to <c>false</c> is the only "delete" — there is no hard-delete endpoint.</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// Optional; <c>null</c> means no department is associated with this category. Drives the ticket
    /// detail page's assignee picker — when a ticket's category has a department, only active users
    /// in that department are offered as assignees, strictly (no cross-department fallback, even if
    /// that leaves the picker empty); a category with no department imposes no restriction at all —
    /// every active user is eligible. The same department scoping is enforced server-side (not just as
    /// a picker filter) by <c>TicketsService.UpdateAssignmentAsync</c>, and drives automatic assignment
    /// (Story 23 — see <c>CustomerSupportCrm.Api.Tickets.Assignment.TicketAssignmentService</c>). Must
    /// reference an existing, active department — enforced in <c>TicketCategoriesService</c>, not
    /// here. No navigation property back to <see cref="Departments.Department"/> — same "look the name
    /// up directly" convention as <see cref="Users.User.DepartmentId"/>; see its remarks.
    /// </summary>
    public Guid? DepartmentId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
