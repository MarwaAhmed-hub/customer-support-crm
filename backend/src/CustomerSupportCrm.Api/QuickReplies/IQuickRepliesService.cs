namespace CustomerSupportCrm.Api.QuickReplies;

public enum QuickReplyOperationOutcome
{
    Success,
    NotFound,

    /// <summary>Title is empty/whitespace-only after trimming — StringLength(MinimumLength = 1) alone lets a single space through.</summary>
    InvalidTitle,

    /// <summary>Body is empty/whitespace-only after trimming.</summary>
    InvalidBody,

    DuplicateTitle,
}

public sealed record QuickReplyResult(QuickReplyOperationOutcome Outcome, QuickReplyDto? QuickReply = null)
{
    public static QuickReplyResult Success(QuickReplyDto quickReply) => new(QuickReplyOperationOutcome.Success, quickReply);
    public static readonly QuickReplyResult NotFound = new(QuickReplyOperationOutcome.NotFound);
    public static readonly QuickReplyResult InvalidTitle = new(QuickReplyOperationOutcome.InvalidTitle);
    public static readonly QuickReplyResult InvalidBody = new(QuickReplyOperationOutcome.InvalidBody);
    public static readonly QuickReplyResult DuplicateTitle = new(QuickReplyOperationOutcome.DuplicateTitle);
}

/// <summary>
/// Business rules for quick replies: duplicate-title rejection (case-insensitive). Modeled directly on
/// <c>Tickets.Categories.TicketCategoriesService</c>, with one difference — unlike ticket categories
/// (which tickets reference and therefore never hard-delete), a quick reply has no downstream
/// dependents, so <see cref="DeleteAsync"/> is a real hard delete.
/// </summary>
public interface IQuickRepliesService
{
    /// <summary>Active replies only by default — the ticket composer picker uses this; the management list page passes <c>includeInactive: true</c>. <paramref name="search"/>, when non-empty, matches Title or Body (case-insensitive).</summary>
    Task<IReadOnlyList<QuickReplyDto>> ListAsync(bool includeInactive, string? search, CancellationToken cancellationToken = default);

    Task<QuickReplyDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<QuickReplyResult> CreateAsync(CreateQuickReplyRequest request, CancellationToken cancellationToken = default);

    Task<QuickReplyResult> UpdateAsync(Guid id, UpdateQuickReplyRequest request, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
