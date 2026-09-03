namespace CustomerSupportCrm.Domain.Tickets;

/// <summary>Story 24: which SLA clock (see <c>Domain.Sla.TicketSla</c>) an escalation is about — evaluated independently of the other.</summary>
public enum SlaType
{
    FirstResponse = 1,
    Resolution = 2,
}

/// <summary>Story 24: 80% of the SLA duration elapsed (Warning) or the due time itself reached (Breach).</summary>
public enum EscalationMilestone
{
    Warning = 1,
    Breach = 2,
}

/// <summary>Story 24: who an escalation is routed to — never <c>Customer</c>, see <c>SlaEscalationService</c>'s routing rules.</summary>
public enum EscalationTargetRole
{
    Agent = 1,
    Manager = 2,
    Administrator = 3,
}

/// <summary>
/// Story 24: an immutable record of one SLA warning/breach milestone crossed for one ticket. At most
/// one row exists per <c>(TicketId, SlaType, Milestone)</c> — enforced by a unique index in
/// <c>CrmDbContext</c> — so re-running the evaluator is always safe. Modeled on <see cref="TicketHistory"/>'s
/// append-only style. No update/delete path — see <see cref="Api.Sla.Escalations.ISlaEscalationService"/>.
/// </summary>
public class TicketEscalation
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TicketId { get; set; }

    public Ticket? Ticket { get; set; }

    public SlaType SlaType { get; set; }

    public EscalationMilestone Milestone { get; set; }

    /// <summary>The user this escalation is routed to. Null when no eligible user could be resolved at all (e.g. no active Administrator exists) — see <see cref="Notes"/>.</summary>
    public Guid? TargetUserId { get; set; }

    public EscalationTargetRole TargetRole { get; set; }

    /// <summary>UTC instant the 80%/100% threshold was actually reached (not when this row was created — the evaluator may run minutes later).</summary>
    public DateTime ThresholdAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>True when the ticket had no <see cref="Ticket.AssignedUserId"/> at the moment this specific milestone was evaluated — routing is decided per-milestone, not once for the ticket's lifetime (see the evaluator's remarks on assignment changing between Warning and Breach).</summary>
    public bool WasUnassigned { get; set; }

    /// <summary>Explains an unusual routing outcome (e.g. "no manager resolved; fell back to administrator"). Null on the ordinary path.</summary>
    public string? Notes { get; set; }
}
