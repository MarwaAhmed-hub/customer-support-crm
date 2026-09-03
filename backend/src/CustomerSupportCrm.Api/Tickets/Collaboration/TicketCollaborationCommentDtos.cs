using System.ComponentModel.DataAnnotations;

namespace CustomerSupportCrm.Api.Tickets.Collaboration;

public sealed record TicketCollaborationCommentDto(
    Guid Id,
    Guid TicketId,
    string Body,
    Guid AuthorUserId,
    string? AuthorDisplayName,
    DateTimeOffset CreatedAt);

// MinimumLength = 1 alone lets a single space through, so the service still rejects a
// whitespace-only Body after trimming — see TicketCollaborationCommentsService.
public sealed record CreateTicketCollaborationCommentRequest([Required, StringLength(4000, MinimumLength = 1)] string Body);
