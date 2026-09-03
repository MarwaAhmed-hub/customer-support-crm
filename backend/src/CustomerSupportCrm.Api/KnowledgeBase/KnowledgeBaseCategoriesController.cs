using CustomerSupportCrm.Api.Audit;
using CustomerSupportCrm.Api.Authorization;
using CustomerSupportCrm.Domain.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupportCrm.Api.KnowledgeBase;

/// <summary>Knowledge base categories (Story 26) — master data. <c>GET</c> is gated by the same <c>ArticlesView</c> permission as the articles themselves (anyone who can browse articles needs the category list for filters); write actions require <c>CategoriesManage</c>. See <see cref="KnowledgeBaseCategoriesService"/> for the business rules.</summary>
[ApiController]
[Route("api/knowledge-base/categories")]
[Authorize]
public class KnowledgeBaseCategoriesController(IKnowledgeBaseCategoriesService categoriesService, IAuditLogService auditLogService) : ControllerBase
{
    [HasPermission(Permissions.KnowledgeBase.ArticlesView)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<KnowledgeBaseCategoryDto>>> List(
        [FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        Ok(await categoriesService.ListAsync(includeInactive, cancellationToken));

    [HasPermission(Permissions.KnowledgeBase.ArticlesView)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<KnowledgeBaseCategoryDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var category = await categoriesService.GetAsync(id, cancellationToken);
        return category is null ? NotFound() : Ok(category);
    }

    [HasPermission(Permissions.KnowledgeBase.CategoriesManage)]
    [HttpPost]
    public async Task<ActionResult<KnowledgeBaseCategoryDto>> Create(CreateKnowledgeBaseCategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await categoriesService.CreateAsync(request, cancellationToken);
        if (result.Outcome == KnowledgeBaseCategoryOperationOutcome.Success)
        {
            await auditLogService.RecordAsync(
                action: "create",
                summary: $"Knowledge base category '{result.Category!.Name}' created",
                entityType: "KnowledgeBaseCategory",
                entityId: result.Category.Id.ToString(),
                ct: cancellationToken);
        }
        return result.Outcome switch
        {
            KnowledgeBaseCategoryOperationOutcome.Success => CreatedAtAction(nameof(Get), new { id = result.Category!.Id }, result.Category),
            KnowledgeBaseCategoryOperationOutcome.InvalidName => Invalid("invalid_name"),
            KnowledgeBaseCategoryOperationOutcome.DuplicateName => DuplicateName(),
            _ => Problem(statusCode: 500),
        };
    }

    [HasPermission(Permissions.KnowledgeBase.CategoriesManage)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<KnowledgeBaseCategoryDto>> Update(Guid id, UpdateKnowledgeBaseCategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await categoriesService.UpdateAsync(id, request, cancellationToken);
        if (result.Outcome == KnowledgeBaseCategoryOperationOutcome.Success)
        {
            await auditLogService.RecordAsync(
                action: "update",
                summary: $"Knowledge base category '{result.Category!.Name}' updated",
                entityType: "KnowledgeBaseCategory",
                entityId: result.Category.Id.ToString(),
                ct: cancellationToken);
        }
        return result.Outcome switch
        {
            KnowledgeBaseCategoryOperationOutcome.Success => Ok(result.Category),
            KnowledgeBaseCategoryOperationOutcome.NotFound => NotFound(),
            KnowledgeBaseCategoryOperationOutcome.InvalidName => Invalid("invalid_name"),
            KnowledgeBaseCategoryOperationOutcome.DuplicateName => DuplicateName(),
            _ => Problem(statusCode: 500),
        };
    }

    [HasPermission(Permissions.KnowledgeBase.CategoriesManage)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var existing = await categoriesService.GetAsync(id, cancellationToken);

        var result = await categoriesService.DeleteAsync(id, cancellationToken);
        if (result.Outcome == KnowledgeBaseCategoryOperationOutcome.Success)
        {
            await auditLogService.RecordAsync(
                action: "delete",
                summary: $"Knowledge base category '{existing?.Name}' deleted",
                entityType: "KnowledgeBaseCategory",
                entityId: id.ToString(),
                ct: cancellationToken);
        }
        return result.Outcome switch
        {
            KnowledgeBaseCategoryOperationOutcome.Success => NoContent(),
            KnowledgeBaseCategoryOperationOutcome.NotFound => NotFound(),
            KnowledgeBaseCategoryOperationOutcome.ReferencedByContent => Conflict(new { error = "category_referenced_by_content" }),
            _ => Problem(statusCode: 500),
        };
    }

    private BadRequestObjectResult Invalid(string error) => BadRequest(new { error });

    private ObjectResult DuplicateName() => Conflict(new { error = "duplicate_knowledge_base_category_name" });
}
