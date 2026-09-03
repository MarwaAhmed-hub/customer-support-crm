namespace CustomerSupportCrm.Domain.Tickets;

/// <summary>
/// Master data for a ticket's urgency level (e.g. "Low", "Urgent"). Independent of
/// <see cref="TicketCategory"/> — see its remarks for the shared rationale.
/// </summary>
public class TicketPriority
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = default!;

    /// <summary>Upper-invariant form of <see cref="Name"/>, used for the case-insensitive unique index — same pattern as <see cref="Departments.Department.NormalizedName"/>.</summary>
    public string NormalizedName { get; set; } = default!;

    /// <summary>Ascending sort key for rendering priorities in their natural order (e.g. Low=10, Medium=20, High=30, Urgent=40). Not required to be unique or contiguous — ties break by <see cref="Name"/>.</summary>
    public int SortOrder { get; set; }

    public string? Description { get; set; }

    /// <summary>Only <c>true</c> priorities appear in user-facing pickers on a ticket form (a later story). Setting this to <c>false</c> is the only "delete" — there is no hard-delete endpoint.</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
