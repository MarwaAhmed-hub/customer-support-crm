namespace CustomerSupportCrm.Api.KnowledgeBase;

public enum KnowledgeBaseCategoryOperationOutcome
{
    Success,
    NotFound,

    /// <summary>Name is empty/whitespace-only after trimming.</summary>
    InvalidName,

    DuplicateName,

    /// <summary>
    /// Delete only — at least one <see cref="Domain.KnowledgeBase.KnowledgeBaseArticle"/>,
    /// <see cref="Domain.KnowledgeBase.KbSolution"/>, or <see cref="Domain.KnowledgeBase.KbGuide"/>
    /// still references this category (Story 27: categories are shared across all four Knowledge Base
    /// content types, not just Articles). No cascade; the category must be re-pointed or the
    /// referencing content removed first.
    /// </summary>
    ReferencedByContent,
}

public sealed record KnowledgeBaseCategoryResult(KnowledgeBaseCategoryOperationOutcome Outcome, KnowledgeBaseCategoryDto? Category = null)
{
    public static KnowledgeBaseCategoryResult Success(KnowledgeBaseCategoryDto category) => new(KnowledgeBaseCategoryOperationOutcome.Success, category);
    public static readonly KnowledgeBaseCategoryResult Deleted = new(KnowledgeBaseCategoryOperationOutcome.Success);
    public static readonly KnowledgeBaseCategoryResult NotFound = new(KnowledgeBaseCategoryOperationOutcome.NotFound);
    public static readonly KnowledgeBaseCategoryResult InvalidName = new(KnowledgeBaseCategoryOperationOutcome.InvalidName);
    public static readonly KnowledgeBaseCategoryResult DuplicateName = new(KnowledgeBaseCategoryOperationOutcome.DuplicateName);
    public static readonly KnowledgeBaseCategoryResult ReferencedByContent = new(KnowledgeBaseCategoryOperationOutcome.ReferencedByContent);
}

/// <summary>Business rules for knowledge base categories — duplicate-name rejection (case-insensitive) and, unlike <c>Tickets.Categories.TicketCategoriesService</c>, a real hard delete (blocked while any Article/Solution/Guide still references the category). Modeled on <c>TicketCategoriesService</c> otherwise.</summary>
public interface IKnowledgeBaseCategoriesService
{
    /// <summary>Active categories only by default — the article form's picker uses this; the management page passes <c>includeInactive: true</c>.</summary>
    Task<IReadOnlyList<KnowledgeBaseCategoryDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default);

    Task<KnowledgeBaseCategoryDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<KnowledgeBaseCategoryResult> CreateAsync(CreateKnowledgeBaseCategoryRequest request, CancellationToken cancellationToken = default);

    Task<KnowledgeBaseCategoryResult> UpdateAsync(Guid id, UpdateKnowledgeBaseCategoryRequest request, CancellationToken cancellationToken = default);

    Task<KnowledgeBaseCategoryResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
