using CustomerSupportCrm.Domain.KnowledgeBase;

namespace CustomerSupportCrm.Api.KnowledgeBase;

public enum KnowledgeBaseArticleOperationOutcome
{
    Success,
    NotFound,
    CategoryNotFound,

    /// <summary>Title is empty/whitespace-only after trimming.</summary>
    InvalidTitle,

    /// <summary>Body is empty/whitespace-only after trimming.</summary>
    InvalidBody,
}

public sealed record KnowledgeBaseArticleResult(KnowledgeBaseArticleOperationOutcome Outcome, KnowledgeBaseArticleDto? Article = null)
{
    public static KnowledgeBaseArticleResult Success(KnowledgeBaseArticleDto article) => new(KnowledgeBaseArticleOperationOutcome.Success, article);
    public static readonly KnowledgeBaseArticleResult NotFound = new(KnowledgeBaseArticleOperationOutcome.NotFound);
    public static readonly KnowledgeBaseArticleResult CategoryNotFound = new(KnowledgeBaseArticleOperationOutcome.CategoryNotFound);
    public static readonly KnowledgeBaseArticleResult InvalidTitle = new(KnowledgeBaseArticleOperationOutcome.InvalidTitle);
    public static readonly KnowledgeBaseArticleResult InvalidBody = new(KnowledgeBaseArticleOperationOutcome.InvalidBody);
}

/// <summary>
/// Story 26: FAQs and Help Articles. Visibility is enforced here, not just hinted at by the UI —
/// <paramref name="canSeeInternal"/>/<c>canManage</c> flags below are resolved by the controller from
/// the caller's permissions (<c>knowledgebase.articles.view.internal</c> /
/// <c>knowledgebase.articles.manage</c>) and passed in as plain booleans, keeping this service itself
/// free of any ASP.NET Core / <c>ClaimsPrincipal</c> dependency — same pattern
/// <c>LiveChatController.ResolveScope</c> uses for its own scoped-vs-unscoped read.
/// </summary>
public interface IKnowledgeBaseArticlesService
{
    /// <summary>
    /// A non-manager's <paramref name="status"/>/<paramref name="audience"/> filters are silently
    /// overridden, never honored as a way to see more than they're allowed: status is forced to
    /// Published, and audience is forced to CustomerFacing when <paramref name="canSeeInternal"/> is
    /// false. A manager's filters (including "no filter" = see everything) are honored as given.
    /// </summary>
    Task<IReadOnlyList<KnowledgeBaseArticleDto>> ListAsync(
        KnowledgeBaseContentType? contentType, Guid? categoryId, KnowledgeBaseAudience? audience, KnowledgeBasePublicationStatus? status,
        bool canManage, bool canSeeInternal, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns null — a 404, never a 403 — for a Draft item to a non-manager, or an Internal item to a
    /// caller without <paramref name="canSeeInternal"/>, so a probing caller can never distinguish
    /// "doesn't exist" from "exists but you can't see it".
    /// </summary>
    Task<KnowledgeBaseArticleDto?> GetAsync(Guid id, bool canManage, bool canSeeInternal, CancellationToken cancellationToken = default);

    /// <summary>Always starts <see cref="KnowledgeBasePublicationStatus.Draft"/> — there is no Status field on the request at all, so this is structural, not a runtime check.</summary>
    Task<KnowledgeBaseArticleResult> CreateAsync(CreateKnowledgeBaseArticleRequest request, Guid actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Works regardless of current status (Draft or Published) — editing content never implicitly publishes or unpublishes it.</summary>
    Task<KnowledgeBaseArticleResult> UpdateAsync(Guid id, UpdateKnowledgeBaseArticleRequest request, Guid actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Idempotent — publishing an already-published item is a no-op that leaves the original <c>PublishedAtUtc</c> untouched, not a reset to now.</summary>
    Task<KnowledgeBaseArticleResult> PublishAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Idempotent — unpublishing an already-Draft item is a no-op.</summary>
    Task<KnowledgeBaseArticleResult> UnpublishAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Hard delete — there is no soft-delete/archive concept for an article (unlike a ticket category's IsActive-only convention).</summary>
    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
