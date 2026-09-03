namespace CustomerSupportCrm.Domain.Tickets;

/// <summary>
/// Story 23: one row per department (the department id is the primary key — see
/// <c>CrmDbContext</c>), tracking the last agent id <c>TicketAssignmentService</c> chose as its
/// round-robin tie-break within that department. Upserted in the same <c>SaveChangesAsync</c> as the
/// assignment it records, so it never drifts out of sync with the tickets it influenced.
/// </summary>
public class AssignmentRoundRobinCursor
{
    public Guid DepartmentId { get; set; }

    public Guid LastAssignedUserId { get; set; }

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
