using CustomerSupportCrm.Domain.Tickets;
using CustomerSupportCrm.Domain.Users;

namespace CustomerSupportCrm.Domain.Customers;

/// <summary>
/// A single, read-only interaction record attached to a <see cref="Customer"/> — e.g. a call log or
/// meeting note authored elsewhere in the system, or (Story 19) an inbound/outbound email or web-form
/// submission. <see cref="InteractionType"/> stays a free-form short code rather than growing into a
/// dedicated Channel/Direction enum pair — Story 19 uses the values "ticket" (unchanged, Story 11),
/// "email_inbound", "email_outbound", and "web_form".
/// </summary>
/// <remarks>A plain, settable POCO — matching <see cref="Branches.Branch"/>/<see cref="Departments.Department"/>'s style.</remarks>
public class CustomerInteraction
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CustomerId { get; set; }

    public Customer Customer { get; set; } = null!;

    /// <summary>Chronological anchor for the newest-first listing. UTC.</summary>
    public DateTime OccurredAt { get; set; }

    /// <summary>Free-form short code (e.g. "call", "meeting", "note-log", "ticket", "email_inbound", "email_outbound", "web_form") — not a channel implementation.</summary>
    public string InteractionType { get; set; } = string.Empty;

    /// <summary>Optional short summary shown in the list — for email, the subject line.</summary>
    public string? Summary { get; set; }

    /// <summary>Optional longer description — for email, the body text.</summary>
    public string? Details { get; set; }

    /// <summary>
    /// CRM user/agent associated with the interaction. Nullable — system-authored events may have
    /// none, and the FK is SetNull (see <c>CrmDbContext</c>), so a deleted user's past interactions
    /// keep their row with this reset to null rather than being removed.
    /// </summary>
    public Guid? UserId { get; set; }

    public User? User { get; set; }

    /// <summary>
    /// Story 19: the <see cref="Tickets.Ticket"/> this interaction belongs to — null for interactions
    /// that predate this story or aren't ticket-related (e.g. a manual call log). Every email/web-form
    /// interaction sets this.
    /// </summary>
    public Guid? TicketId { get; set; }

    public Ticket? Ticket { get; set; }

    /// <summary>Story 19: the email provider's message id — set on both inbound and outbound email interactions, used for idempotent re-ingestion and for threading a reply's <see cref="InReplyToMessageId"/> back to it. Null for non-email interactions.</summary>
    public string? ExternalMessageId { get; set; }

    /// <summary>Story 19: the inbound email's own "In-Reply-To" header value, when present — used to link a threaded customer reply back to the ticket created by the message it replies to.</summary>
    public string? InReplyToMessageId { get; set; }

    /// <summary>Story 19: the sender address for an inbound email.</summary>
    public string? FromAddress { get; set; }

    /// <summary>Story 19: the recipient address for an outbound email.</summary>
    public string? ToAddress { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }
}
