using CustomerSupportCrm.Api.Audit;
using CustomerSupportCrm.Api.Auth;
using CustomerSupportCrm.Api.Authorization;
using CustomerSupportCrm.Api.KnowledgeBase;
using CustomerSupportCrm.Domain.KnowledgeBase;
using CustomerSupportCrm.Domain.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupportCrm.Api.KnowledgeBase.Solutions;

/// <summary>Story 27: Knowledge Base Solutions. Same shape and visibility rules as <see cref="KnowledgeBaseArticlesController"/> — see <see cref="KbSolutionsService"/>.</summary>
[ApiController]
[Route("api/knowledge-base/solutions")]
[Authorize]
public class KbSolutionsController(IKbSolutionsService solutionsService, IAuditLogService auditLogService) : ControllerBase
{
    [HasPermission(Permissions.KnowledgeBase.SolutionsView)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<KbSolutionDto>>> List(
        [FromQuery] Guid? categoryId,
        [FromQuery] KnowledgeBaseAudience? audience,
        [FromQuery] KnowledgeBasePublicationStatus? status,
        CancellationToken cancellationToken)
    {
        var (canManage, canSeeInternal) = ResolveVisibilityFlags();
        return Ok(await solutionsService.ListAsync(categoryId, audience, status, canManage, canSeeInternal, cancellationToken));
    }

    [HasPermission(Permissions.KnowledgeBase.SolutionsView)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<KbSolutionDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var (canManage, canSeeInternal) = ResolveVisibilityFlags();
        var solution = await solutionsService.GetAsync(id, canManage, canSeeInternal, cancellationToken);
        return solution is null ? NotFound() : Ok(solution);
    }

    [HasPermission(Permissions.KnowledgeBase.SolutionsManage)]
    [HttpPost]
    public async Task<ActionResult<KbSolutionDto>> Create(CreateKbSolutionRequest request, CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        if (actorUserId is null)
        {
            return Unauthorized();
        }

        var result = await solutionsService.CreateAsync(request, actorUserId.Value, cancellationToken);
        if (result.Outcome == KbSolutionOperationOutcome.Success)
        {
            await auditLogService.RecordAsync(
                action: "create",
                summary: $"Knowledge base solution '{result.Solution!.Title}' created",
                entityType: "KbSolution",
                entityId: result.Solution.Id.ToString(),
                ct: cancellationToken);
        }
        return result.Outcome switch
        {
            KbSolutionOperationOutcome.Success => CreatedAtAction(nameof(Get), new { id = result.Solution!.Id }, result.Solution),
            KbSolutionOperationOutcome.CategoryNotFound => NotFound(new { error = "category_not_found" }),
            KbSolutionOperationOutcome.InvalidTitle => Invalid("invalid_title"),
            KbSolutionOperationOutcome.InvalidProblem => Invalid("invalid_problem"),
            KbSolutionOperationOutcome.InvalidSolutionBody => Invalid("invalid_solution_body"),
            _ => Problem(statusCode: 500),
        };
    }

    [HasPermission(Permissions.KnowledgeBase.SolutionsManage)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<KbSolutionDto>> Update(Guid id, UpdateKbSolutionRequest request, CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        if (actorUserId is null)
        {
            return Unauthorized();
        }

        var result = await solutionsService.UpdateAsync(id, request, actorUserId.Value, cancellationToken);
        if (result.Outcome == KbSolutionOperationOutcome.Success)
        {
            await auditLogService.RecordAsync(
                action: "update",
                summary: $"Knowledge base solution '{result.Solution!.Title}' updated",
                entityType: "KbSolution",
                entityId: result.Solution.Id.ToString(),
                ct: cancellationToken);
        }
        return result.Outcome switch
        {
            KbSolutionOperationOutcome.Success => Ok(result.Solution),
            KbSolutionOperationOutcome.NotFound => NotFound(),
            KbSolutionOperationOutcome.CategoryNotFound => NotFound(new { error = "category_not_found" }),
            KbSolutionOperationOutcome.InvalidTitle => Invalid("invalid_title"),
            KbSolutionOperationOutcome.InvalidProblem => Invalid("invalid_problem"),
            KbSolutionOperationOutcome.InvalidSolutionBody => Invalid("invalid_solution_body"),
            _ => Problem(statusCode: 500),
        };
    }

    [HasPermission(Permissions.KnowledgeBase.SolutionsPublish)]
    [HttpPost("{id:guid}/publish")]
    public async Task<ActionResult<KbSolutionDto>> Publish(Guid id, CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        if (actorUserId is null)
        {
            return Unauthorized();
        }

        var result = await solutionsService.PublishAsync(id, actorUserId.Value, cancellationToken);
        if (result.Outcome == KbSolutionOperationOutcome.Success)
        {
            await auditLogService.RecordAsync(
                action: "publish",
                summary: $"Knowledge base solution '{result.Solution!.Title}' published",
                entityType: "KbSolution",
                entityId: result.Solution.Id.ToString(),
                ct: cancellationToken);
        }
        return result.Outcome switch
        {
            KbSolutionOperationOutcome.Success => Ok(result.Solution),
            KbSolutionOperationOutcome.NotFound => NotFound(),
            _ => Problem(statusCode: 500),
        };
    }

    [HasPermission(Permissions.KnowledgeBase.SolutionsPublish)]
    [HttpPost("{id:guid}/unpublish")]
    public async Task<ActionResult<KbSolutionDto>> Unpublish(Guid id, CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        if (actorUserId is null)
        {
            return Unauthorized();
        }

        var result = await solutionsService.UnpublishAsync(id, actorUserId.Value, cancellationToken);
        if (result.Outcome == KbSolutionOperationOutcome.Success)
        {
            await auditLogService.RecordAsync(
                action: "unpublish",
                summary: $"Knowledge base solution '{result.Solution!.Title}' unpublished",
                entityType: "KbSolution",
                entityId: result.Solution.Id.ToString(),
                ct: cancellationToken);
        }
        return result.Outcome switch
        {
            KbSolutionOperationOutcome.Success => Ok(result.Solution),
            KbSolutionOperationOutcome.NotFound => NotFound(),
            _ => Problem(statusCode: 500),
        };
    }

    [HasPermission(Permissions.KnowledgeBase.SolutionsManage)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var existing = await solutionsService.GetAsync(id, canManage: true, canSeeInternal: true, cancellationToken);

        var deleted = await solutionsService.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            return NotFound();
        }

        await auditLogService.RecordAsync(
            action: "delete",
            summary: $"Knowledge base solution '{existing?.Title}' deleted",
            entityType: "KbSolution",
            entityId: id.ToString(),
            ct: cancellationToken);

        return NoContent();
    }

    private (bool CanManage, bool CanSeeInternal) ResolveVisibilityFlags() =>
        (User.HasPermission(Permissions.KnowledgeBase.SolutionsManage), User.HasPermission(Permissions.KnowledgeBase.SolutionsViewInternal));

    private BadRequestObjectResult Invalid(string error) => BadRequest(new { error });
}
