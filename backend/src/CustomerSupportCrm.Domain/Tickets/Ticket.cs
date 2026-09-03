using CustomerSupportCrm.Domain.Customers;
using CustomerSupportCrm.Domain.Users;

namespace CustomerSupportCrm.Domain.Tickets;

/// <summary>
/// A support ticket raised for a <see cref="Customers.Customer"/>, classified by
/// <see cref="TicketCategory"/> and <see cref="TicketPriority"/>. History (Story 14) is deliberately
/// still absent — see Story 11's "Not in scope".
/// </summary>
/// <remarks>Timestamps use <see cref="DateTimeOffset"/>, matching <see cref="TicketCategory"/>/<see cref="TicketPriority"/> — its siblings in this same namespace.</remarks>
public class Ticket
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CustomerId { get; set; }

    public Customer? Customer { get; set; }

    public string Subject { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public Guid CategoryId { get; set; }

    public TicketCategory? Category { get; set; }

    public Guid PriorityId { get; set; }

    public TicketPriority? Priority { get; set; }

    /// <summary>Free-form short code — see <see cref="TicketStatuses"/>. Only <see cref="TicketStatuses.Open"/> is ever set by this story.</summary>
    public string Status { get; set; } = TicketStatuses.Open;

    public Guid CreatedByUserId { get; set; }

    public User? CreatedByUser { get; set; }

    /// <summary>
    /// The CRM user currently assigned to work this ticket. Nullable — a ticket starts unassigned,
    /// and can be explicitly unassigned again later (Story 12). No status transition, escalation, or
    /// automatic-assignment rule is tied to this field in this story.
    /// </summary>
    public Guid? AssignedUserId { get; set; }

    public User? AssignedUser { get; set; }

    /// <summary>Story 13: manual escalation only — no SLA timer or automatic-escalation rule sets this.</summary>
    public bool IsEscalated { get; set; }

    public DateTimeOffset? EscalatedAt { get; set; }

    public Guid? EscalatedByUserId { get; set; }

    public User? EscalatedByUser { get; set; }

    public string? EscalationReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;

    /// <summary>Story 19/20: "Email" | "WebForm" | "WhatsApp" | "Sms" | null (manual/internal creation — every ticket before Story 19, and every ticket created through the authenticated UI). Set once at creation and never changed afterward.</summary>
    public string? SourceChannel { get; set; }

    /// <summary>
    /// Story 20: the provider's stable conversation/thread id (e.g. a WhatsApp wa_id), stamped onto
    /// this ticket by <see cref="Communications.Inbound.InboundMessageService"/> when the inbound
    /// message carried one. Correction (see that class's remarks for the full history): a matching
    /// inbound message on the same channel/customer <em>does</em> reuse this ticket while it is still
    /// open, rather than always opening a new one — scoped to this exact conversation id, not to
    /// "any open ticket on this channel" (an earlier, broader version of that reuse rule merged
    /// unrelated conversations together and was reverted for it).
    /// </summary>
    public string? ExternalConversationId { get; set; }
}
