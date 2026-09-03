using CustomerSupportCrm.Domain.Users;

namespace CustomerSupportCrm.Domain.Tickets;

/// <summary>
/// Story 18: an internal, staff-only discussion thread on a <see cref="Ticket"/> — Agents and Managers
/// talking to each other about how to handle the ticket, never shown to the customer. Deliberately
/// separate from <see cref="TicketHistory"/> (an immutable record of what changed on the ticket
/// itself) and from <see cref="Audit.AuditLog"/> (a system-wide security/ops trail). No edit/delete —
/// out of scope for this story.
/// </summary>
public class TicketCollaborationComment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid TicketId { get; set; }

    public Ticket? Ticket { get; set; }

    public string Body { get; set; } = string.Empty;

    public Guid AuthorUserId { get; set; }

    public User? AuthorUser { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
