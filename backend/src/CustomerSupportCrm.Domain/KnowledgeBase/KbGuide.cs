namespace CustomerSupportCrm.Domain.KnowledgeBase;

/// <summary>
/// Story 27: a single Guide — an ordered list of <see cref="KbGuideStep"/>s under a Title/Description,
/// sharing <see cref="KnowledgeBaseCategory"/>/<see cref="KnowledgeBaseAudience"/>/
/// <see cref="KnowledgeBasePublicationStatus"/> with <see cref="KnowledgeBaseArticle"/> and
/// <see cref="KbSolution"/>. Visibility is enforced server-side in <c>KbGuidesService</c>.
/// </summary>
public class KbGuide
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public KnowledgeBaseAudience Audience { get; set; }

    public KnowledgeBasePublicationStatus Status { get; set; } = KnowledgeBasePublicationStatus.Draft;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public Guid CategoryId { get; set; }

    public KnowledgeBaseCategory? Category { get; set; }

    /// <summary>Order-preserving — see <c>KbGuidesService.UpdateAsync</c>, which replaces the whole collection on every update rather than diffing individual steps.</summary>
    public ICollection<KbGuideStep> Steps { get; set; } = new List<KbGuideStep>();

    public Guid CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Guid? UpdatedByUserId { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public Guid? PublishedByUserId { get; set; }

    public DateTime? PublishedAtUtc { get; set; }
}
