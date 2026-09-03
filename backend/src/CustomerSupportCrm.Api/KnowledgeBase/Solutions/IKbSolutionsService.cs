using CustomerSupportCrm.Domain.KnowledgeBase;

namespace CustomerSupportCrm.Api.KnowledgeBase.Solutions;

public enum KbSolutionOperationOutcome
{
    Success,
    NotFound,
    CategoryNotFound,

    /// <summary>Title is empty/whitespace-only after trimming.</summary>
    InvalidTitle,

    /// <summary>Problem is empty/whitespace-only after trimming.</summary>
    InvalidProblem,

    /// <summary>SolutionBody is empty/whitespace-only after trimming.</summary>
    InvalidSolutionBody,
}

public sealed record KbSolutionResult(KbSolutionOperationOutcome Outcome, KbSolutionDto? Solution = null)
{
    public static KbSolutionResult Success(KbSolutionDto solution) => new(KbSolutionOperationOutcome.Success, solution);
    public static readonly KbSolutionResult NotFound = new(KbSolutionOperationOutcome.NotFound);
    public static readonly KbSolutionResult CategoryNotFound = new(KbSolutionOperationOutcome.CategoryNotFound);
    public static readonly KbSolutionResult InvalidTitle = new(KbSolutionOperationOutcome.InvalidTitle);
    public static readonly KbSolutionResult InvalidProblem = new(KbSolutionOperationOutcome.InvalidProblem);
    public static readonly KbSolutionResult InvalidSolutionBody = new(KbSolutionOperationOutcome.InvalidSolutionBody);
}

/// <summary>
/// Story 27: Solutions — a distinct content type from <see cref="KnowledgeBaseArticle"/>, but visibility
/// is resolved exactly the same way: the controller turns the caller's permissions into
/// <c>canManage</c>/<c>canSeeInternal</c> booleans (never a raw <c>ClaimsPrincipal</c> here), so the
/// same rules apply uniformly whether the caller is staff or a Customer — there is no separate
/// "public" code path.
/// </summary>
public interface IKbSolutionsService
{
    Task<IReadOnlyList<KbSolutionDto>> ListAsync(
        Guid? categoryId, KnowledgeBaseAudience? audience, KnowledgeBasePublicationStatus? status,
        bool canManage, bool canSeeInternal, CancellationToken cancellationToken = default);

    /// <summary>Returns null (404, never 403) for a Draft item to a non-manager, or an Internal item without <paramref name="canSeeInternal"/>.</summary>
    Task<KbSolutionDto?> GetAsync(Guid id, bool canManage, bool canSeeInternal, CancellationToken cancellationToken = default);

    /// <summary>Always starts Draft — <see cref="CreateKbSolutionRequest"/> has no Status field at all.</summary>
    Task<KbSolutionResult> CreateAsync(CreateKbSolutionRequest request, Guid actorUserId, CancellationToken cancellationToken = default);

    Task<KbSolutionResult> UpdateAsync(Guid id, UpdateKbSolutionRequest request, Guid actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Idempotent — publishing an already-published item leaves the original PublishedAtUtc untouched.</summary>
    Task<KbSolutionResult> PublishAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Idempotent — unpublishing an already-Draft item is a no-op.</summary>
    Task<KbSolutionResult> UnpublishAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
