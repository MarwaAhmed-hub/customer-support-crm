namespace CustomerSupportCrm.Api.Tickets.Collaboration;

public enum TicketCollaborationCommentOperationOutcome
{
    Success,
    TicketNotFound,

    /// <summary>Body is empty/whitespace-only after trimming — StringLength(MinimumLength = 1) alone lets a single space through.</summary>
    InvalidBody,
}

public sealed record TicketCollaborationCommentResult(TicketCollaborationCommentOperationOutcome Outcome, TicketCollaborationCommentDto? Comment = null)
{
    public static TicketCollaborationCommentResult Success(TicketCollaborationCommentDto comment) => new(TicketCollaborationCommentOperationOutcome.Success, comment);
    public static readonly TicketCollaborationCommentResult TicketNotFound = new(TicketCollaborationCommentOperationOutcome.TicketNotFound);
    public static readonly TicketCollaborationCommentResult InvalidBody = new(TicketCollaborationCommentOperationOutcome.InvalidBody);
}

/// <summary>
/// Internal, staff-only discussion thread on a ticket (Story 18). Never touches <c>Ticket.Status</c>,
/// <c>Ticket.AssignedUserId</c>, or <c>Ticket.UpdatedAt</c> — adding a comment is purely additive to
/// this table. No edit/delete — out of scope for this story.
/// </summary>
public interface ITicketCollaborationCommentsService
{
    /// <summary>Chronological (oldest first) — a discussion thread reads top to bottom. A null return means the ticket does not exist — the controller turns that into a 404; an empty (non-null) list means the ticket exists with no comments yet.</summary>
    Task<IReadOnlyList<TicketCollaborationCommentDto>?> ListAsync(Guid ticketId, CancellationToken cancellationToken = default);

    Task<TicketCollaborationCommentResult> CreateAsync(Guid ticketId, Guid authorUserId, CreateTicketCollaborationCommentRequest request, CancellationToken cancellationToken = default);
}
