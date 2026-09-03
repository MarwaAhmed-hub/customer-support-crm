using CustomerSupportCrm.Domain.Tickets;

namespace CustomerSupportCrm.Domain.Sla;

/// <summary>
/// Story 22: one row per <see cref="Ticket"/> (unique on <see cref="TicketId"/>), snapshotting the
/// <see cref="SlaPolicy"/> that applied when the ticket was created and tracking First Response and
/// Resolution independently. Deliberately a separate table rather than columns on <see cref="Ticket"/>
/// itself — SLA is a sibling concept to Story 13's manual <c>IsEscalated</c> fields, not a replacement
/// or extension of them, and keeping it isolated leaves room for Stories 24/25 to build on it without
/// touching the Ticket entity again.
/// </summary>
public class TicketSla
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TicketId { get; set; }

    public Ticket? Ticket { get; set; }

    /// <summary>The policy that started this SLA — kept even if the policy is later edited/deactivated, so an in-flight ticket's clock never moves once started.</summary>
    public Guid SlaPolicyId { get; set; }

    public SlaPolicy? SlaPolicy { get; set; }

    /// <summary>Always <c>Ticket.CreatedAt</c> — never <c>AssignedAt</c>, never <c>UpdatedAt</c>. Set once at <see cref="Api.Sla.ISlaService.StartForTicketAsync"/> and never changed afterward, including by assignment or category changes.</summary>
    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset FirstResponseDueAt { get; set; }

    public DateTimeOffset ResolutionDueAt { get; set; }

    public string FirstResponseStatus { get; set; } = SlaStatuses.Running;

    public string ResolutionStatus { get; set; } = SlaStatuses.Running;

    /// <summary>Set only when <see cref="FirstResponseStatus"/> transitions to <see cref="SlaStatuses.Met"/> — stays null on a Breached transition (the response never came before the terminal state was recorded).</summary>
    public DateTimeOffset? FirstResponseAt { get; set; }

    /// <summary>Set only when <see cref="ResolutionStatus"/> transitions to <see cref="SlaStatuses.Met"/> — same null-on-breach convention as <see cref="FirstResponseAt"/>.</summary>
    public DateTimeOffset? ResolvedAt { get; set; }

    public DateTimeOffset? FirstResponseBreachedAt { get; set; }

    public DateTimeOffset? ResolutionBreachedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
