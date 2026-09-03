using System.Text.Json.Serialization;

namespace CustomerSupportCrm.Api.KnowledgeBase.Search;

/// <summary>
/// The four searchable Knowledge Base content types. Named distinctly from
/// <see cref="Domain.KnowledgeBase.KnowledgeBaseContentType"/> (which only distinguishes Faq/HelpArticle
/// within the Articles table) to avoid confusion — a search result's <see cref="Faq"/>/<see cref="Article"/>
/// split maps onto that domain enum, while <see cref="Solution"/>/<see cref="Guide"/> map onto their own
/// separate tables from Story 27.
/// </summary>
public enum KnowledgeBaseSearchContentType
{
    Faq = 1,
    Article = 2,
    Solution = 3,
    Guide = 4,
}

public sealed record KnowledgeBaseSearchQuery(
    string? Q,
    IReadOnlyCollection<KnowledgeBaseSearchContentType>? Types,
    Guid? CategoryId,
    int Page,
    int PageSize);

public sealed record KnowledgeBaseSearchResultItem(
    Guid Id,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] KnowledgeBaseSearchContentType Type,
    string Title,
    Guid? CategoryId,
    string? CategoryName,
    string Excerpt,
    DateTime? PublishedAtUtc);

public sealed record KnowledgeBaseSearchResponse(
    int Page,
    int PageSize,
    int Total,
    IReadOnlyList<KnowledgeBaseSearchResultItem> Items);
