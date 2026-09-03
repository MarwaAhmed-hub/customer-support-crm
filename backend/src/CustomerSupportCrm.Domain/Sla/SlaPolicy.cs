using CustomerSupportCrm.Domain.Tickets;

namespace CustomerSupportCrm.Domain.Sla;

/// <summary>
/// Story 22: how long a ticket has to get a First Response and a Resolution, by priority.
/// <see cref="PriorityId"/> is nullable — a null-priority policy is the fallback applied when no
/// priority-specific active policy exists, so a fresh install only ever needs one seeded row
/// (<c>DbSeeder</c>'s "Default SLA") to cover every ticket regardless of priority.
/// </summary>
public class SlaPolicy
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid? PriorityId { get; set; }

    public TicketPriority? Priority { get; set; }

    public string Name { get; set; } = default!;

    public int FirstResponseMinutes { get; set; }

    public int ResolutionMinutes { get; set; }

    /// <summary>Only <c>true</c> policies are eligible for <see cref="Api.Sla.ISlaService.StartForTicketAsync"/> to pick — same "deactivate, never hard-delete" convention as <see cref="TicketPriority.IsActive"/>. At most one active policy per <see cref="PriorityId"/> value (including one active default where it's null) — enforced by a filtered unique index, not application code.</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
