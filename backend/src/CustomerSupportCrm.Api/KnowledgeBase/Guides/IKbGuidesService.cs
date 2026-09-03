using CustomerSupportCrm.Domain.KnowledgeBase;

namespace CustomerSupportCrm.Api.KnowledgeBase.Guides;

public enum KbGuideOperationOutcome
{
    Success,
    NotFound,
    CategoryNotFound,

    /// <summary>Title is empty/whitespace-only after trimming.</summary>
    InvalidTitle,

    /// <summary>Description is empty/whitespace-only after trimming.</summary>
    InvalidDescription,

    /// <summary>Steps is empty, or every entry trims to empty.</summary>
    InvalidSteps,
}

public sealed record KbGuideResult(KbGuideOperationOutcome Outcome, KbGuideDto? Guide = null)
{
    public static KbGuideResult Success(KbGuideDto guide) => new(KbGuideOperationOutcome.Success, guide);
    public static readonly KbGuideResult NotFound = new(KbGuideOperationOutcome.NotFound);
    public static readonly KbGuideResult CategoryNotFound = new(KbGuideOperationOutcome.CategoryNotFound);
    public static readonly KbGuideResult InvalidTitle = new(KbGuideOperationOutcome.InvalidTitle);
    public static readonly KbGuideResult InvalidDescription = new(KbGuideOperationOutcome.InvalidDescription);
    public static readonly KbGuideResult InvalidSteps = new(KbGuideOperationOutcome.InvalidSteps);
}

/// <summary>Story 27: Guides — same visibility model as <see cref="Solutions.IKbSolutionsService"/>, plus an ordered <c>Steps</c> collection that is replaced wholesale on every update (see <see cref="UpdateAsync"/>).</summary>
public interface IKbGuidesService
{
    Task<IReadOnlyList<KbGuideDto>> ListAsync(
        Guid? categoryId, KnowledgeBaseAudience? audience, KnowledgeBasePublicationStatus? status,
        bool canManage, bool canSeeInternal, CancellationToken cancellationToken = default);

    Task<KbGuideDto?> GetAsync(Guid id, bool canManage, bool canSeeInternal, CancellationToken cancellationToken = default);

    /// <summary>Always starts Draft. Rejects an empty (or all-whitespace) Steps list.</summary>
    Task<KbGuideResult> CreateAsync(CreateKbGuideRequest request, Guid actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Replaces the entire Steps collection (delete + re-insert, <c>Order</c> set from array index) — never a partial diff.</summary>
    Task<KbGuideResult> UpdateAsync(Guid id, UpdateKbGuideRequest request, Guid actorUserId, CancellationToken cancellationToken = default);

    Task<KbGuideResult> PublishAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default);

    Task<KbGuideResult> UnpublishAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
