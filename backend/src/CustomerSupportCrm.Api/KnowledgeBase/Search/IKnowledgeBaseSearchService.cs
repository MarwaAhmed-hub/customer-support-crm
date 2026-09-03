namespace CustomerSupportCrm.Api.KnowledgeBase.Search;

/// <summary>
/// Per-content-type visibility, resolved once by the controller from the caller's permission claims —
/// same <c>canView</c>/<c>canSeeInternal</c> shape as <c>KnowledgeBaseArticlesService</c>/
/// <c>KbSolutionsService</c>/<c>KbGuidesService</c>, just three of them side by side. A content type is
/// skipped entirely from search when its own View flag is false (holding <c>knowledgebase.search</c>
/// does not bypass a type's own read gate); ViewInternal additionally governs whether Internal-audience
/// items of that type are included.
/// </summary>
public sealed record KnowledgeBaseSearchVisibility(
    bool ArticlesView, bool ArticlesViewInternal,
    bool SolutionsView, bool SolutionsViewInternal,
    bool GuidesView, bool GuidesViewInternal);

/// <summary>
/// Story 28: read-only cross-content-type search over Published FAQs/Help Articles/Solutions/Guides.
/// Never returns Draft content to anyone, regardless of permissions — search is a discovery surface
/// over what's already live, not a management preview.
/// </summary>
public interface IKnowledgeBaseSearchService
{
    Task<KnowledgeBaseSearchResponse> SearchAsync(
        KnowledgeBaseSearchQuery query, KnowledgeBaseSearchVisibility visibility, CancellationToken cancellationToken = default);
}
