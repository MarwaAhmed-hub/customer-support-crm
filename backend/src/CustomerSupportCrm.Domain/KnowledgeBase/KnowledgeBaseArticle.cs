namespace CustomerSupportCrm.Domain.KnowledgeBase;

/// <summary>Story 26: which shape a <see cref="KnowledgeBaseArticle"/> takes — same table, different meaning for <see cref="KnowledgeBaseArticle.Title"/>/<see cref="KnowledgeBaseArticle.Body"/> (Question/Answer for <see cref="Faq"/>, Title/Content for <see cref="HelpArticle"/>). Never changes after creation — see <c>KnowledgeBaseArticlesService.UpdateAsync</c>.</summary>
public enum KnowledgeBaseContentType
{
    Faq = 1,
    HelpArticle = 2,
}

/// <summary>Story 26: <see cref="Internal"/> content must never reach a Customer principal, even when <see cref="KnowledgeBasePublicationStatus.Published"/> — the authoritative check, not a UI-only hint.</summary>
public enum KnowledgeBaseAudience
{
    CustomerFacing = 1,
    Internal = 2,
}

/// <summary>Story 26: a <see cref="Draft"/> item is visible only to callers holding <c>knowledgebase.articles.manage</c> — Agents and Customers only ever see <see cref="Published"/> items.</summary>
public enum KnowledgeBasePublicationStatus
{
    Draft = 1,
    Published = 2,
}

/// <summary>
/// Story 26: a single FAQ or Help Article — see <see cref="KnowledgeBaseContentType"/> for how the two
/// share this one table. Visibility (draft/published, customer-facing/internal) is enforced
/// server-side in <c>KnowledgeBaseArticlesService</c>, never left to the UI alone.
/// </summary>
public class KnowledgeBaseArticle
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public KnowledgeBaseContentType ContentType { get; set; }

    public KnowledgeBaseAudience Audience { get; set; }

    public KnowledgeBasePublicationStatus Status { get; set; } = KnowledgeBasePublicationStatus.Draft;

    /// <summary>The Question (Faq) or Title (HelpArticle).</summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>The Answer (Faq) or Content (HelpArticle) — long-form, no maximum length.</summary>
    public string Body { get; set; } = string.Empty;

    public Guid CategoryId { get; set; }

    public KnowledgeBaseCategory? Category { get; set; }

    public Guid CreatedByUserId { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public Guid? UpdatedByUserId { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }

    public Guid? PublishedByUserId { get; set; }

    public DateTime? PublishedAtUtc { get; set; }
}
