using CustomerSupportCrm.Domain.Users;

namespace CustomerSupportCrm.Domain.Tickets;

/// <summary>
/// One immutable line in a ticket's business-lifecycle timeline (Story 14) — creation, field edits,
/// assignment/reassignment, and status transitions from Stories 11-13. Deliberately separate from
/// <see cref="Audit.AuditLog"/> (a system-wide security/ops trail) and from
/// <see cref="Customers.CustomerInteraction"/> (Story 08's customer-facing activity feed, still
/// written only once per ticket by Story 11's create flow) — this table exists purely to answer
/// "what happened to this specific ticket, in order." Manual escalation (Story 13) is intentionally
/// not recorded here — it already has its own audit-log entries and its own UI state.
/// </summary>
public class TicketHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TicketId { get; set; }

    public Ticket? Ticket { get; set; }

    /// <summary>"Created" | "Updated" | "Assigned" | "Reassigned" | "StatusChanged" | "CategoryChanged" | "PriorityChanged".</summary>
    public string EventType { get; set; } = string.Empty;

    /// <summary>The <see cref="Ticket"/> field this entry describes, e.g. "Subject", "Status", "AssignedUserId" — null for "Created".</summary>
    public string? Field { get; set; }

    /// <summary>Human-readable display value, not a raw foreign-key id — null for "Created".</summary>
    public string? PreviousValue { get; set; }

    public string? NewValue { get; set; }

    /// <summary>One-line summary rendered as the timeline entry's title, e.g. "Ticket assigned to Jane Doe".</summary>
    public string Summary { get; set; } = string.Empty;

    /// <summary>Nullable so a system-authored entry (none exist yet, but the shape stays honest) never needs a fake actor.</summary>
    public Guid? PerformedByUserId { get; set; }

    public User? PerformedByUser { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
