using CustomerSupportCrm.Api.Sla.Escalations;
using CustomerSupportCrm.Domain.Notifications;
using CustomerSupportCrm.Domain.Tickets;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CustomerSupportCrm.Api.Notifications;

/// <summary>
/// Deliberately synchronous rather than an async event-bus + background worker: every trigger point
/// (a controller action succeeding, or a sweep just having created a <c>TicketEscalation</c> row) is
/// already the exact moment the recipient is known with certainty, so there is nothing to decouple —
/// adding a <c>Channel&lt;T&gt;</c> + hosted worker in between would only add latency and a second
/// place for the write to fail. "Notification failure must not roll back the ticket operation" is
/// satisfied the same way <c>AuditLogService.RecordAsync</c> already satisfies it for audit logging:
/// every public method here catches its own exceptions and never throws back to the caller.
///
/// Customer-facing notifications (routed through the existing WhatsApp/SMS/email dispatchers) are
/// deliberately out of scope for this pass — that would mean automatically sending real outbound
/// messages to real customers on a live channel, which is a materially different, higher-stakes
/// decision than persisting an internal row, and was confirmed out of scope rather than assumed.
/// </summary>
public sealed class NotificationService(CrmDbContext db, ILogger<NotificationService> logger) : INotificationService
{
    public async Task NotifyTicketAssignedAsync(Guid ticketId, Guid assignedUserId, CancellationToken cancellationToken = default)
    {
        try
        {
            var subject = await db.Tickets.AsNoTracking().Where(t => t.Id == ticketId).Select(t => t.Subject).SingleOrDefaultAsync(cancellationToken);
            if (subject is null)
            {
                return;
            }

            // Unlike SlaMilestone below (driven by a periodic sweep that could plausibly re-process
            // the same TicketEscalation row), this is triggered synchronously exactly once per
            // successful assignment change from the controller — there is no retry/replay path that
            // could call this twice for the "same" assignment, so the key only needs to be unique by
            // construction, not meaningfully deduplicating across repeated calls.
            var dedupeKey = $"ticket-assigned:{ticketId}:{assignedUserId}:{DateTime.UtcNow.Ticks}";

            await InsertAsync(new Notification
            {
                EventType = NotificationEventType.TicketAssigned,
                TicketId = ticketId,
                RecipientUserId = assignedUserId,
                Subject = "Ticket assigned to you",
                Body = $"You've been assigned ticket \"{Truncate(subject, 150)}\".",
                DedupeKey = dedupeKey,
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create TicketAssigned notification for ticket {TicketId} / user {UserId}.", ticketId, assignedUserId);
        }
    }

    public async Task NotifySlaMilestoneAsync(TicketEscalationDto escalation, CancellationToken cancellationToken = default)
    {
        if (escalation.TargetUserId is null)
        {
            // Already logged as an error by SlaEscalationService itself when it couldn't resolve
            // anyone — nothing more to do here, and definitely nothing to notify.
            return;
        }

        try
        {
            var subject = await db.Tickets.AsNoTracking().Where(t => t.Id == escalation.TicketId).Select(t => t.Subject).SingleOrDefaultAsync(cancellationToken);
            if (subject is null)
            {
                return;
            }

            var eventType = escalation.Milestone == EscalationMilestone.Warning ? NotificationEventType.SlaWarning : NotificationEventType.SlaBreached;
            var slaLabel = escalation.SlaType == SlaType.FirstResponse ? "First Response" : "Resolution";
            var milestoneLabel = escalation.Milestone == EscalationMilestone.Warning ? "80% elapsed" : "breached";

            // Keyed by the escalation's own id — a TicketEscalation row is created at most once ever
            // (Story 24's own unique index), so this is a true 1:1 dedupe key, not just
            // unique-by-construction like TicketAssigned's above.
            var dedupeKey = $"escalation:{escalation.Id}";

            await InsertAsync(new Notification
            {
                EventType = eventType,
                SlaType = escalation.SlaType,
                TicketId = escalation.TicketId,
                RecipientUserId = escalation.TargetUserId.Value,
                Subject = $"{slaLabel} SLA {milestoneLabel}",
                Body = $"{slaLabel} SLA for ticket \"{Truncate(subject, 150)}\" has {milestoneLabel}.",
                DedupeKey = dedupeKey,
            }, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to create {EventType} notification for escalation {EscalationId} (ticket {TicketId}).",
                escalation.Milestone, escalation.Id, escalation.TicketId);
        }
    }

    public async Task<NotificationListResponse> ListForUserAsync(Guid userId, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.Notifications.AsNoTracking().Where(n => n.RecipientUserId == userId);
        if (unreadOnly)
        {
            query = query.Where(n => n.ReadAtUtc == null);
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(n => n.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationDto(n.Id, n.EventType, n.SlaType, n.TicketId, n.Subject, n.Body, n.CreatedAtUtc, n.ReadAtUtc))
            .ToListAsync(cancellationToken);

        return new NotificationListResponse(items, total, page, pageSize);
    }

    public async Task<bool> MarkReadAsync(Guid notificationId, Guid userId, CancellationToken cancellationToken = default)
    {
        var notification = await db.Notifications.SingleOrDefaultAsync(n => n.Id == notificationId, cancellationToken);
        if (notification is null || notification.RecipientUserId != userId)
        {
            return false;
        }

        if (notification.ReadAtUtc is null)
        {
            notification.ReadAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return true;
    }

    public async Task<int> MarkAllReadAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var unread = await db.Notifications.Where(n => n.RecipientUserId == userId && n.ReadAtUtc == null).ToListAsync(cancellationToken);
        if (unread.Count == 0)
        {
            return 0;
        }

        var now = DateTime.UtcNow;
        foreach (var notification in unread)
        {
            notification.ReadAtUtc = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        return unread.Count;
    }

    private async Task InsertAsync(Notification notification, CancellationToken cancellationToken)
    {
        // The ordinary (non-concurrent) idempotency path — same "check first" pattern
        // SlaEscalationService.TryCreateAsync uses, and for the same reason: the EF InMemory provider
        // used in tests does not enforce the unique index at all, so this check is what actually makes
        // repeated calls idempotent there. The catch below is the production (SQL Server) backstop for
        // a genuine race between two concurrent callers.
        if (await db.Notifications.AnyAsync(n => n.DedupeKey == notification.DedupeKey, cancellationToken))
        {
            return;
        }

        db.Notifications.Add(notification);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Same defense-in-depth as TicketCategoriesService/SlaEscalationService: lost a race
            // against a concurrent writer for this exact DedupeKey — treat as already-created.
            db.Entry(notification).State = EntityState.Detached;
        }
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "…";

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
}
