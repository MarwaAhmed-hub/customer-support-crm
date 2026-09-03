using CustomerSupportCrm.Api.Sla.Escalations;

namespace CustomerSupportCrm.Api.Notifications;

/// <summary>
/// Story 25: internal, in-app notifications for a signed-in staff member — reacts to the two business
/// events Stories 23/24 already produce (a ticket's <c>AssignedUserId</c> changing, and a new
/// <c>TicketEscalation</c> row) without ever mutating the ticket itself. See
/// <see cref="NotificationService"/>'s remarks for why this is a synchronous, self-isolating call
/// rather than an async event bus, and why customer-facing dispatch is deliberately out of scope for
/// this pass.
/// </summary>
public interface INotificationService
{
    /// <summary>No-ops (creates nothing) if <paramref name="assignedUserId"/> is the ticket's only ever assignee change within this exact call — there is no "unassigned" case here since the caller only invokes this when a ticket newly has a non-null assignee. Never throws.</summary>
    Task NotifyTicketAssignedAsync(Guid ticketId, Guid assignedUserId, CancellationToken cancellationToken = default);

    /// <summary>No-ops if <paramref name="escalation"/>.TargetUserId is null (no eligible recipient could be resolved — already logged as an error by <c>SlaEscalationService</c> itself). Never throws.</summary>
    Task NotifySlaMilestoneAsync(TicketEscalationDto escalation, CancellationToken cancellationToken = default);

    /// <summary>The caller's own notifications, newest first.</summary>
    Task<NotificationListResponse> ListForUserAsync(Guid userId, bool unreadOnly, int page, int pageSize, CancellationToken cancellationToken = default);

    /// <summary>False if the notification doesn't exist or belongs to a different user — the controller turns that into a 404, never revealing which case it was.</summary>
    Task<bool> MarkReadAsync(Guid notificationId, Guid userId, CancellationToken cancellationToken = default);

    /// <summary>Returns the number of previously-unread rows just marked read.</summary>
    Task<int> MarkAllReadAsync(Guid userId, CancellationToken cancellationToken = default);
}
