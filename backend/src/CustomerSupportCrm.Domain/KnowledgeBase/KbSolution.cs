namespace CustomerSupportCrm.Domain.KnowledgeBase;

/// <summary>
/// Story 27: a single Solution (Problem/Fix pair) — a distinct content type from
/// <see cref="KnowledgeBaseArticle"/>, but sharing the same <see cref="KnowledgeBaseCategory"/>,
/// <see cref="KnowledgeBaseAudience"/>, and <see cref="KnowledgeBasePublicationStatus"/> concepts
/// rather than introducing parallel enums for the same two axes. Visibility is enforced server-side in
/// <c>KbSolutionsService</c>, never left to the UI alone.
/// </summary>
public class KbSolution
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public KnowledgeBaseAudience Audience { get; set; }

    public KnowledgeBasePublicationStatus Status { get; set; } = KnowledgeBasePublicationStatus.Draft;

    public string Title { get; set; } = string.Empty;

    /// <summary>The problem/issue being described.</summary>
    public string Problem { get; set; } = string.Empty;

    /// <summary>The fix — long-form, no maximum length.</summary>
    public string SolutionBody { get; set; } = string.Empty;

    public Guid CategoryId { get; set; }

    public KnowledgeBaseCategory? Category { get; set; }

    public Guid CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Guid? UpdatedByUserId { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public Guid? PublishedByUserId { get; set; }

    public DateTime? PublishedAtUtc { get; set; }
}
