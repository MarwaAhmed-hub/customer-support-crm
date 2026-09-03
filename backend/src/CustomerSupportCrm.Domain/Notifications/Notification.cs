using CustomerSupportCrm.Domain.Tickets;

namespace CustomerSupportCrm.Domain.Notifications;

/// <summary>Story 25: which business event produced this notification.</summary>
public enum NotificationEventType
{
    TicketAssigned = 1,
    SlaWarning = 2,
    SlaBreached = 3,
}

/// <summary>
/// Story 25: an internal, in-app notification for a signed-in staff member (Agent/Manager/
/// Administrator) — never a customer in this pass; see <see cref="NotificationService"/>'s remarks
/// for why customer-facing dispatch is deliberately deferred. Created synchronously by
/// <c>TicketsController</c> (on assignment) and <c>SlaEscalationBackgroundService</c>/
/// <c>SlaEscalationsController</c> (on a new <c>TicketEscalation</c> row) — see
/// <see cref="Api.Notifications.INotificationService"/>. No update path beyond marking read; never
/// mutates the <see cref="Tickets.Ticket"/> it's about.
/// </summary>
public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public NotificationEventType EventType { get; set; }

    /// <summary>Only set for <see cref="NotificationEventType.SlaWarning"/>/<see cref="NotificationEventType.SlaBreached"/> — null for <see cref="NotificationEventType.TicketAssigned"/>.</summary>
    public SlaType? SlaType { get; set; }

    public Guid TicketId { get; set; }

    public Ticket? Ticket { get; set; }

    /// <summary>The staff member this notification is for. Always required — a caller with no resolvable recipient (e.g. no active Administrator) never creates a row at all; see <c>NotificationService</c>.</summary>
    public Guid RecipientUserId { get; set; }

    public string Subject { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>Null until the recipient marks it read — see <c>INotificationService.MarkReadAsync</c>.</summary>
    public DateTime? ReadAtUtc { get; set; }

    /// <summary>
    /// Deterministic uniqueness key, enforced by a unique index (see <c>CrmDbContext</c>) — the actual
    /// idempotency backstop, same role <c>TicketEscalation</c>'s own unique index plays for Story 24.
    /// </summary>
    public string DedupeKey { get; set; } = string.Empty;
}
