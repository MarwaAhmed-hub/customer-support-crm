using CustomerSupportCrm.Domain.KnowledgeBase;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupportCrm.Api.KnowledgeBase.Search;

/// <summary>
/// Deliberate simplification vs. a literal SQL <c>UNION</c>: the three content-type queries below each
/// run and fully materialize independently (never more than a few hundred KB items in practice), then
/// the merge/sort/page/total all happen in memory. This trades a little query efficiency for code that
/// is simple, easy to reason about, and behaves identically on the EF Core InMemory provider used by
/// this project's tests — a true cross-table <c>UNION</c> with per-branch conditional filters would be
/// far more fragile to translate and to test.
/// </summary>
public sealed class KnowledgeBaseSearchService(CrmDbContext db) : IKnowledgeBaseSearchService
{
    private const int ExcerptRadius = 100;
    private const int ExcerptMaxLength = 240;

    private sealed record SearchRow(
        Guid Id, KnowledgeBaseSearchContentType Type, string Title, Guid CategoryId, string CategoryName,
        string ExcerptSource, DateTime? PublishedAtUtc);

    public async Task<KnowledgeBaseSearchResponse> SearchAsync(
        KnowledgeBaseSearchQuery query, KnowledgeBaseSearchVisibility visibility, CancellationToken cancellationToken = default)
    {
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize <= 0 ? 20 : query.PageSize, 1, 50);

        var trimmedQ = query.Q?.Trim();
        var hasQuery = !string.IsNullOrEmpty(trimmedQ);
        var hasTypeFilter = query.Types is { Count: > 0 };

        // Empty query AND no other filter at all: an unscoped browse-everything request. Matches the
        // story's "avoid leaking counts" rule without blocking a legitimate filter-only browse (e.g.
        // "show me everything in this category").
        if (!hasQuery && !hasTypeFilter && query.CategoryId is null)
        {
            return new KnowledgeBaseSearchResponse(page, pageSize, 0, []);
        }

        var wantFaq = !hasTypeFilter || query.Types!.Contains(KnowledgeBaseSearchContentType.Faq);
        var wantArticle = !hasTypeFilter || query.Types!.Contains(KnowledgeBaseSearchContentType.Article);
        var wantSolution = !hasTypeFilter || query.Types!.Contains(KnowledgeBaseSearchContentType.Solution);
        var wantGuide = !hasTypeFilter || query.Types!.Contains(KnowledgeBaseSearchContentType.Guide);

        var rows = new List<SearchRow>();

        if ((wantFaq || wantArticle) && visibility.ArticlesView)
        {
            rows.AddRange(await SearchArticlesAsync(wantFaq, wantArticle, visibility.ArticlesViewInternal, query.CategoryId, trimmedQ, cancellationToken));
        }

        if (wantSolution && visibility.SolutionsView)
        {
            rows.AddRange(await SearchSolutionsAsync(visibility.SolutionsViewInternal, query.CategoryId, trimmedQ, cancellationToken));
        }

        if (wantGuide && visibility.GuidesView)
        {
            rows.AddRange(await SearchGuidesAsync(visibility.GuidesViewInternal, query.CategoryId, trimmedQ, cancellationToken));
        }

        var ordered = rows
            .OrderByDescending(r => r.PublishedAtUtc)
            .ThenBy(r => r.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var total = ordered.Count;
        var pageItems = ordered
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(r => new KnowledgeBaseSearchResultItem(
                r.Id, r.Type, r.Title, r.CategoryId, r.CategoryName, BuildExcerpt(r.ExcerptSource, trimmedQ), r.PublishedAtUtc))
            .ToList();

        return new KnowledgeBaseSearchResponse(page, pageSize, total, pageItems);
    }

    private async Task<List<SearchRow>> SearchArticlesAsync(
        bool wantFaq, bool wantArticle, bool canSeeInternal, Guid? categoryId, string? trimmedQ, CancellationToken cancellationToken)
    {
        var contentQuery = db.KnowledgeBaseArticles.AsNoTracking().Where(a => a.Status == KnowledgeBasePublicationStatus.Published);

        if (!canSeeInternal)
        {
            contentQuery = contentQuery.Where(a => a.Audience == KnowledgeBaseAudience.CustomerFacing);
        }

        if (categoryId.HasValue)
        {
            contentQuery = contentQuery.Where(a => a.CategoryId == categoryId.Value);
        }

        if (!(wantFaq && wantArticle))
        {
            var wantedContentType = wantFaq ? KnowledgeBaseContentType.Faq : KnowledgeBaseContentType.HelpArticle;
            contentQuery = contentQuery.Where(a => a.ContentType == wantedContentType);
        }

        if (!string.IsNullOrEmpty(trimmedQ))
        {
            var lowered = trimmedQ.ToLower();
            contentQuery = contentQuery.Where(a => a.Title.ToLower().Contains(lowered) || a.Body.ToLower().Contains(lowered));
        }

        return await contentQuery
            .Select(a => new SearchRow(
                a.Id,
                a.ContentType == KnowledgeBaseContentType.Faq ? KnowledgeBaseSearchContentType.Faq : KnowledgeBaseSearchContentType.Article,
                a.Title, a.CategoryId, a.Category!.Name, a.Body, a.PublishedAtUtc))
            .ToListAsync(cancellationToken);
    }

    private async Task<List<SearchRow>> SearchSolutionsAsync(bool canSeeInternal, Guid? categoryId, string? trimmedQ, CancellationToken cancellationToken)
    {
        var contentQuery = db.KbSolutions.AsNoTracking().Where(s => s.Status == KnowledgeBasePublicationStatus.Published);

        if (!canSeeInternal)
        {
            contentQuery = contentQuery.Where(s => s.Audience == KnowledgeBaseAudience.CustomerFacing);
        }

        if (categoryId.HasValue)
        {
            contentQuery = contentQuery.Where(s => s.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrEmpty(trimmedQ))
        {
            var lowered = trimmedQ.ToLower();
            contentQuery = contentQuery.Where(s =>
                s.Title.ToLower().Contains(lowered) || s.Problem.ToLower().Contains(lowered) || s.SolutionBody.ToLower().Contains(lowered));
        }

        return await contentQuery
            .Select(s => new SearchRow(s.Id, KnowledgeBaseSearchContentType.Solution, s.Title, s.CategoryId, s.Category!.Name, s.SolutionBody, s.PublishedAtUtc))
            .ToListAsync(cancellationToken);
    }

    private async Task<List<SearchRow>> SearchGuidesAsync(bool canSeeInternal, Guid? categoryId, string? trimmedQ, CancellationToken cancellationToken)
    {
        var contentQuery = db.KbGuides.AsNoTracking().Where(g => g.Status == KnowledgeBasePublicationStatus.Published);

        if (!canSeeInternal)
        {
            contentQuery = contentQuery.Where(g => g.Audience == KnowledgeBaseAudience.CustomerFacing);
        }

        if (categoryId.HasValue)
        {
            contentQuery = contentQuery.Where(g => g.CategoryId == categoryId.Value);
        }

        if (!string.IsNullOrEmpty(trimmedQ))
        {
            var lowered = trimmedQ.ToLower();
            contentQuery = contentQuery.Where(g =>
                g.Title.ToLower().Contains(lowered) ||
                g.Description.ToLower().Contains(lowered) ||
                g.Steps.Any(step => step.Instruction.ToLower().Contains(lowered)));
        }

        return await contentQuery
            .Select(g => new SearchRow(g.Id, KnowledgeBaseSearchContentType.Guide, g.Title, g.CategoryId, g.Category!.Name, g.Description, g.PublishedAtUtc))
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// A ±<see cref="ExcerptRadius"/>-character window around the first case-insensitive occurrence of
    /// <paramref name="trimmedQuery"/> in <paramref name="text"/>, capped at <see cref="ExcerptMaxLength"/>
    /// and marked with a leading/trailing "…" when the window doesn't start/end at a text boundary.
    /// Falls back to the start of the text when there's no query (filter-only browse) or no match in
    /// this particular field (the row matched on Title/Problem instead — matching the excerpt to
    /// whichever field actually matched is a snippet-ranking nuance this story treats as out of scope).
    /// </summary>
    private static string BuildExcerpt(string text, string? trimmedQuery)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var index = string.IsNullOrEmpty(trimmedQuery) ? -1 : text.IndexOf(trimmedQuery, StringComparison.OrdinalIgnoreCase);

        if (index < 0)
        {
            return text.Length <= ExcerptMaxLength ? text : text[..ExcerptMaxLength].TrimEnd() + "…";
        }

        var start = Math.Max(0, index - ExcerptRadius);
        var end = Math.Min(text.Length, index + trimmedQuery!.Length + ExcerptRadius);
        var window = text[start..end];
        if (window.Length > ExcerptMaxLength)
        {
            window = window[..ExcerptMaxLength];
        }

        var prefix = start > 0 ? "…" : "";
        var suffix = end < text.Length ? "…" : "";
        return prefix + window.Trim() + suffix;
    }
}
