using CustomerSupportCrm.Api.Auth;
using CustomerSupportCrm.Api.Authorization;
using CustomerSupportCrm.Domain.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupportCrm.Api.KnowledgeBase.Search;

/// <summary>Story 28: read-only cross-content-type search over Published FAQs/Help Articles/Solutions/Guides. See <see cref="KnowledgeBaseSearchService"/> for the merge/visibility logic.</summary>
[ApiController]
[Route("api/knowledge-base/search")]
[Authorize]
public sealed class KnowledgeBaseSearchController(IKnowledgeBaseSearchService searchService) : ControllerBase
{
    [HasPermission(Permissions.KnowledgeBase.Search)]
    [HttpGet]
    public async Task<ActionResult<KnowledgeBaseSearchResponse>> Search(
        [FromQuery] string? q,
        [FromQuery(Name = "type")] KnowledgeBaseSearchContentType[]? type,
        [FromQuery] Guid? categoryId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var visibility = new KnowledgeBaseSearchVisibility(
            ArticlesView: User.HasPermission(Permissions.KnowledgeBase.ArticlesView),
            ArticlesViewInternal: User.HasPermission(Permissions.KnowledgeBase.ArticlesViewInternal),
            SolutionsView: User.HasPermission(Permissions.KnowledgeBase.SolutionsView),
            SolutionsViewInternal: User.HasPermission(Permissions.KnowledgeBase.SolutionsViewInternal),
            GuidesView: User.HasPermission(Permissions.KnowledgeBase.GuidesView),
            GuidesViewInternal: User.HasPermission(Permissions.KnowledgeBase.GuidesViewInternal));

        var query = new KnowledgeBaseSearchQuery(
            q, type is { Length: > 0 } ? type : null, categoryId, page, pageSize);

        return Ok(await searchService.SearchAsync(query, visibility, cancellationToken));
    }
}
