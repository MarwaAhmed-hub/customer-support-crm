using System.ComponentModel.DataAnnotations;

namespace CustomerSupportCrm.Api.QuickReplies;

public sealed record QuickReplyDto(Guid Id, string Title, string Body, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

// Attributes target the primary-constructor parameters directly, not the generated properties —
// matching Tickets/Categories/TicketCategoryDtos.cs. MinimumLength = 1 alone lets a whitespace-only
// value through, so the service still rejects a whitespace-only Title/Body after trimming — see
// QuickRepliesService.
public sealed record CreateQuickReplyRequest(
    [Required, StringLength(200, MinimumLength = 1)] string Title,
    [Required, StringLength(5000, MinimumLength = 1)] string Body);

public sealed record UpdateQuickReplyRequest(
    [Required, StringLength(200, MinimumLength = 1)] string Title,
    [Required, StringLength(5000, MinimumLength = 1)] string Body,
    bool IsActive);
