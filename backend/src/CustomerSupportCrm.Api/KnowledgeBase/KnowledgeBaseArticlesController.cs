using CustomerSupportCrm.Api.Audit;
using CustomerSupportCrm.Api.Auth;
using CustomerSupportCrm.Api.Authorization;
using CustomerSupportCrm.Domain.KnowledgeBase;
using CustomerSupportCrm.Domain.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupportCrm.Api.KnowledgeBase;

/// <summary>FAQs and Help Articles (Story 26) — see <see cref="KnowledgeBaseArticlesService"/> for the visibility rules. <see cref="ResolveVisibilityFlags"/> is the one place permission strings become the plain booleans the service consumes.</summary>
[ApiController]
[Route("api/knowledge-base/articles")]
[Authorize]
public class KnowledgeBaseArticlesController(IKnowledgeBaseArticlesService articlesService, IAuditLogService auditLogService) : ControllerBase
{
    [HasPermission(Permissions.KnowledgeBase.ArticlesView)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<KnowledgeBaseArticleDto>>> List(
        [FromQuery] KnowledgeBaseContentType? contentType,
        [FromQuery] Guid? categoryId,
        [FromQuery] KnowledgeBaseAudience? audience,
        [FromQuery] KnowledgeBasePublicationStatus? status,
        CancellationToken cancellationToken)
    {
        var (canManage, canSeeInternal) = ResolveVisibilityFlags();
        return Ok(await articlesService.ListAsync(contentType, categoryId, audience, status, canManage, canSeeInternal, cancellationToken));
    }

    [HasPermission(Permissions.KnowledgeBase.ArticlesView)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<KnowledgeBaseArticleDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var (canManage, canSeeInternal) = ResolveVisibilityFlags();
        var article = await articlesService.GetAsync(id, canManage, canSeeInternal, cancellationToken);
        return article is null ? NotFound() : Ok(article);
    }

    [HasPermission(Permissions.KnowledgeBase.ArticlesManage)]
    [HttpPost]
    public async Task<ActionResult<KnowledgeBaseArticleDto>> Create(CreateKnowledgeBaseArticleRequest request, CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        if (actorUserId is null)
        {
            return Unauthorized();
        }

        var result = await articlesService.CreateAsync(request, actorUserId.Value, cancellationToken);
        if (result.Outcome == KnowledgeBaseArticleOperationOutcome.Success)
        {
            await auditLogService.RecordAsync(
                action: "create",
                summary: $"Knowledge base article '{result.Article!.Title}' created",
                entityType: "KnowledgeBaseArticle",
                entityId: result.Article.Id.ToString(),
                ct: cancellationToken);
        }
        return result.Outcome switch
        {
            KnowledgeBaseArticleOperationOutcome.Success => CreatedAtAction(nameof(Get), new { id = result.Article!.Id }, result.Article),
            KnowledgeBaseArticleOperationOutcome.CategoryNotFound => NotFound(new { error = "category_not_found" }),
            KnowledgeBaseArticleOperationOutcome.InvalidTitle => Invalid("invalid_title"),
            KnowledgeBaseArticleOperationOutcome.InvalidBody => Invalid("invalid_body"),
            _ => Problem(statusCode: 500),
        };
    }

    [HasPermission(Permissions.KnowledgeBase.ArticlesManage)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<KnowledgeBaseArticleDto>> Update(Guid id, UpdateKnowledgeBaseArticleRequest request, CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        if (actorUserId is null)
        {
            return Unauthorized();
        }

        var result = await articlesService.UpdateAsync(id, request, actorUserId.Value, cancellationToken);
        if (result.Outcome == KnowledgeBaseArticleOperationOutcome.Success)
        {
            await auditLogService.RecordAsync(
                action: "update",
                summary: $"Knowledge base article '{result.Article!.Title}' updated",
                entityType: "KnowledgeBaseArticle",
                entityId: result.Article.Id.ToString(),
                ct: cancellationToken);
        }
        return result.Outcome switch
        {
            KnowledgeBaseArticleOperationOutcome.Success => Ok(result.Article),
            KnowledgeBaseArticleOperationOutcome.NotFound => NotFound(),
            KnowledgeBaseArticleOperationOutcome.CategoryNotFound => NotFound(new { error = "category_not_found" }),
            KnowledgeBaseArticleOperationOutcome.InvalidTitle => Invalid("invalid_title"),
            KnowledgeBaseArticleOperationOutcome.InvalidBody => Invalid("invalid_body"),
            _ => Problem(statusCode: 500),
        };
    }

    [HasPermission(Permissions.KnowledgeBase.ArticlesPublish)]
    [HttpPost("{id:guid}/publish")]
    public async Task<ActionResult<KnowledgeBaseArticleDto>> Publish(Guid id, CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        if (actorUserId is null)
        {
            return Unauthorized();
        }

        var result = await articlesService.PublishAsync(id, actorUserId.Value, cancellationToken);
        if (result.Outcome == KnowledgeBaseArticleOperationOutcome.Success)
        {
            await auditLogService.RecordAsync(
                action: "publish",
                summary: $"Knowledge base article '{result.Article!.Title}' published",
                entityType: "KnowledgeBaseArticle",
                entityId: result.Article.Id.ToString(),
                ct: cancellationToken);
        }
        return result.Outcome switch
        {
            KnowledgeBaseArticleOperationOutcome.Success => Ok(result.Article),
            KnowledgeBaseArticleOperationOutcome.NotFound => NotFound(),
            _ => Problem(statusCode: 500),
        };
    }

    [HasPermission(Permissions.KnowledgeBase.ArticlesPublish)]
    [HttpPost("{id:guid}/unpublish")]
    public async Task<ActionResult<KnowledgeBaseArticleDto>> Unpublish(Guid id, CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        if (actorUserId is null)
        {
            return Unauthorized();
        }

        var result = await articlesService.UnpublishAsync(id, actorUserId.Value, cancellationToken);
        if (result.Outcome == KnowledgeBaseArticleOperationOutcome.Success)
        {
            await auditLogService.RecordAsync(
                action: "unpublish",
                summary: $"Knowledge base article '{result.Article!.Title}' unpublished",
                entityType: "KnowledgeBaseArticle",
                entityId: result.Article.Id.ToString(),
                ct: cancellationToken);
        }
        return result.Outcome switch
        {
            KnowledgeBaseArticleOperationOutcome.Success => Ok(result.Article),
            KnowledgeBaseArticleOperationOutcome.NotFound => NotFound(),
            _ => Problem(statusCode: 500),
        };
    }

    [HasPermission(Permissions.KnowledgeBase.ArticlesManage)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        // Captured before the delete purely for the audit summary's title, matching
        // QuickRepliesController.Delete's own pattern.
        var existing = await articlesService.GetAsync(id, canManage: true, canSeeInternal: true, cancellationToken);

        var deleted = await articlesService.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            return NotFound();
        }

        await auditLogService.RecordAsync(
            action: "delete",
            summary: $"Knowledge base article '{existing?.Title}' deleted",
            entityType: "KnowledgeBaseArticle",
            entityId: id.ToString(),
            ct: cancellationToken);

        return NoContent();
    }

    /// <summary>(canManage, canSeeInternal) resolved once per request from the caller's permission claims.</summary>
    private (bool CanManage, bool CanSeeInternal) ResolveVisibilityFlags() =>
        (User.HasPermission(Permissions.KnowledgeBase.ArticlesManage), User.HasPermission(Permissions.KnowledgeBase.ArticlesViewInternal));

    private BadRequestObjectResult Invalid(string error) => BadRequest(new { error });
}
