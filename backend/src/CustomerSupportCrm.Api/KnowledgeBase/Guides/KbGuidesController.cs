using CustomerSupportCrm.Api.Audit;
using CustomerSupportCrm.Api.Auth;
using CustomerSupportCrm.Api.Authorization;
using CustomerSupportCrm.Api.KnowledgeBase;
using CustomerSupportCrm.Domain.KnowledgeBase;
using CustomerSupportCrm.Domain.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupportCrm.Api.KnowledgeBase.Guides;

/// <summary>Story 27: Knowledge Base Guides. Same shape and visibility rules as <see cref="KnowledgeBaseArticlesController"/> — see <see cref="KbGuidesService"/>.</summary>
[ApiController]
[Route("api/knowledge-base/guides")]
[Authorize]
public class KbGuidesController(IKbGuidesService guidesService, IAuditLogService auditLogService) : ControllerBase
{
    [HasPermission(Permissions.KnowledgeBase.GuidesView)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<KbGuideDto>>> List(
        [FromQuery] Guid? categoryId,
        [FromQuery] KnowledgeBaseAudience? audience,
        [FromQuery] KnowledgeBasePublicationStatus? status,
        CancellationToken cancellationToken)
    {
        var (canManage, canSeeInternal) = ResolveVisibilityFlags();
        return Ok(await guidesService.ListAsync(categoryId, audience, status, canManage, canSeeInternal, cancellationToken));
    }

    [HasPermission(Permissions.KnowledgeBase.GuidesView)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<KbGuideDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var (canManage, canSeeInternal) = ResolveVisibilityFlags();
        var guide = await guidesService.GetAsync(id, canManage, canSeeInternal, cancellationToken);
        return guide is null ? NotFound() : Ok(guide);
    }

    [HasPermission(Permissions.KnowledgeBase.GuidesManage)]
    [HttpPost]
    public async Task<ActionResult<KbGuideDto>> Create(CreateKbGuideRequest request, CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        if (actorUserId is null)
        {
            return Unauthorized();
        }

        var result = await guidesService.CreateAsync(request, actorUserId.Value, cancellationToken);
        if (result.Outcome == KbGuideOperationOutcome.Success)
        {
            await auditLogService.RecordAsync(
                action: "create",
                summary: $"Knowledge base guide '{result.Guide!.Title}' created",
                entityType: "KbGuide",
                entityId: result.Guide.Id.ToString(),
                ct: cancellationToken);
        }
        return result.Outcome switch
        {
            KbGuideOperationOutcome.Success => CreatedAtAction(nameof(Get), new { id = result.Guide!.Id }, result.Guide),
            KbGuideOperationOutcome.CategoryNotFound => NotFound(new { error = "category_not_found" }),
            KbGuideOperationOutcome.InvalidTitle => Invalid("invalid_title"),
            KbGuideOperationOutcome.InvalidDescription => Invalid("invalid_description"),
            KbGuideOperationOutcome.InvalidSteps => Invalid("invalid_steps"),
            _ => Problem(statusCode: 500),
        };
    }

    [HasPermission(Permissions.KnowledgeBase.GuidesManage)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<KbGuideDto>> Update(Guid id, UpdateKbGuideRequest request, CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        if (actorUserId is null)
        {
            return Unauthorized();
        }

        var result = await guidesService.UpdateAsync(id, request, actorUserId.Value, cancellationToken);
        if (result.Outcome == KbGuideOperationOutcome.Success)
        {
            await auditLogService.RecordAsync(
                action: "update",
                summary: $"Knowledge base guide '{result.Guide!.Title}' updated",
                entityType: "KbGuide",
                entityId: result.Guide.Id.ToString(),
                ct: cancellationToken);
        }
        return result.Outcome switch
        {
            KbGuideOperationOutcome.Success => Ok(result.Guide),
            KbGuideOperationOutcome.NotFound => NotFound(),
            KbGuideOperationOutcome.CategoryNotFound => NotFound(new { error = "category_not_found" }),
            KbGuideOperationOutcome.InvalidTitle => Invalid("invalid_title"),
            KbGuideOperationOutcome.InvalidDescription => Invalid("invalid_description"),
            KbGuideOperationOutcome.InvalidSteps => Invalid("invalid_steps"),
            _ => Problem(statusCode: 500),
        };
    }

    [HasPermission(Permissions.KnowledgeBase.GuidesPublish)]
    [HttpPost("{id:guid}/publish")]
    public async Task<ActionResult<KbGuideDto>> Publish(Guid id, CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        if (actorUserId is null)
        {
            return Unauthorized();
        }

        var result = await guidesService.PublishAsync(id, actorUserId.Value, cancellationToken);
        if (result.Outcome == KbGuideOperationOutcome.Success)
        {
            await auditLogService.RecordAsync(
                action: "publish",
                summary: $"Knowledge base guide '{result.Guide!.Title}' published",
                entityType: "KbGuide",
                entityId: result.Guide.Id.ToString(),
                ct: cancellationToken);
        }
        return result.Outcome switch
        {
            KbGuideOperationOutcome.Success => Ok(result.Guide),
            KbGuideOperationOutcome.NotFound => NotFound(),
            _ => Problem(statusCode: 500),
        };
    }

    [HasPermission(Permissions.KnowledgeBase.GuidesPublish)]
    [HttpPost("{id:guid}/unpublish")]
    public async Task<ActionResult<KbGuideDto>> Unpublish(Guid id, CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        if (actorUserId is null)
        {
            return Unauthorized();
        }

        var result = await guidesService.UnpublishAsync(id, actorUserId.Value, cancellationToken);
        if (result.Outcome == KbGuideOperationOutcome.Success)
        {
            await auditLogService.RecordAsync(
                action: "unpublish",
                summary: $"Knowledge base guide '{result.Guide!.Title}' unpublished",
                entityType: "KbGuide",
                entityId: result.Guide.Id.ToString(),
                ct: cancellationToken);
        }
        return result.Outcome switch
        {
            KbGuideOperationOutcome.Success => Ok(result.Guide),
            KbGuideOperationOutcome.NotFound => NotFound(),
            _ => Problem(statusCode: 500),
        };
    }

    [HasPermission(Permissions.KnowledgeBase.GuidesManage)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var existing = await guidesService.GetAsync(id, canManage: true, canSeeInternal: true, cancellationToken);

        var deleted = await guidesService.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            return NotFound();
        }

        await auditLogService.RecordAsync(
            action: "delete",
            summary: $"Knowledge base guide '{existing?.Title}' deleted",
            entityType: "KbGuide",
            entityId: id.ToString(),
            ct: cancellationToken);

        return NoContent();
    }

    private (bool CanManage, bool CanSeeInternal) ResolveVisibilityFlags() =>
        (User.HasPermission(Permissions.KnowledgeBase.GuidesManage), User.HasPermission(Permissions.KnowledgeBase.GuidesViewInternal));

    private BadRequestObjectResult Invalid(string error) => BadRequest(new { error });
}
