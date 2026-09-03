using CustomerSupportCrm.Api.Audit;
using CustomerSupportCrm.Api.Authorization;
using CustomerSupportCrm.Domain.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupportCrm.Api.QuickReplies;

/// <summary>Reusable response-template text for the ticket composer's quick-reply picker — see <see cref="QuickRepliesService"/> for the business rules. Never sends anything; purely CRUD over reusable text.</summary>
[ApiController]
[Route("api/quick-replies")]
[Authorize]
public class QuickRepliesController(IQuickRepliesService quickRepliesService, IAuditLogService auditLogService) : ControllerBase
{
    [HasPermission(Permissions.QuickReplies.View)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<QuickReplyDto>>> List(
        [FromQuery] bool includeInactive, [FromQuery] string? search, CancellationToken cancellationToken) =>
        Ok(await quickRepliesService.ListAsync(includeInactive, search, cancellationToken));

    [HasPermission(Permissions.QuickReplies.View)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<QuickReplyDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var quickReply = await quickRepliesService.GetAsync(id, cancellationToken);
        return quickReply is null ? NotFound() : Ok(quickReply);
    }

    [HasPermission(Permissions.QuickReplies.Manage)]
    [HttpPost]
    public async Task<ActionResult<QuickReplyDto>> Create(CreateQuickReplyRequest request, CancellationToken cancellationToken)
    {
        var result = await quickRepliesService.CreateAsync(request, cancellationToken);
        if (result.Outcome == QuickReplyOperationOutcome.Success)
        {
            await auditLogService.RecordAsync(
                action: "create",
                summary: $"Quick reply '{result.QuickReply!.Title}' created",
                entityType: "QuickReply",
                entityId: result.QuickReply.Id.ToString(),
                ct: cancellationToken);
        }
        return result.Outcome switch
        {
            QuickReplyOperationOutcome.Success => CreatedAtAction(nameof(Get), new { id = result.QuickReply!.Id }, result.QuickReply),
            QuickReplyOperationOutcome.InvalidTitle => Invalid("invalid_title"),
            QuickReplyOperationOutcome.InvalidBody => Invalid("invalid_body"),
            QuickReplyOperationOutcome.DuplicateTitle => DuplicateTitle(),
            _ => Problem(statusCode: 500),
        };
    }

    [HasPermission(Permissions.QuickReplies.Manage)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<QuickReplyDto>> Update(Guid id, UpdateQuickReplyRequest request, CancellationToken cancellationToken)
    {
        var result = await quickRepliesService.UpdateAsync(id, request, cancellationToken);
        if (result.Outcome == QuickReplyOperationOutcome.Success)
        {
            await auditLogService.RecordAsync(
                action: "update",
                summary: $"Quick reply '{result.QuickReply!.Title}' updated",
                entityType: "QuickReply",
                entityId: result.QuickReply.Id.ToString(),
                ct: cancellationToken);
        }
        return result.Outcome switch
        {
            QuickReplyOperationOutcome.Success => Ok(result.QuickReply),
            QuickReplyOperationOutcome.NotFound => NotFound(),
            QuickReplyOperationOutcome.InvalidTitle => Invalid("invalid_title"),
            QuickReplyOperationOutcome.InvalidBody => Invalid("invalid_body"),
            QuickReplyOperationOutcome.DuplicateTitle => DuplicateTitle(),
            _ => Problem(statusCode: 500),
        };
    }

    [HasPermission(Permissions.QuickReplies.Manage)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        // Captured before the delete purely for the audit summary's title — a second read, not a
        // second write, matching how TicketsController.UpdateAssignment captures "before" for its
        // audit payload.
        var existing = await quickRepliesService.GetAsync(id, cancellationToken);

        var deleted = await quickRepliesService.DeleteAsync(id, cancellationToken);
        if (!deleted)
        {
            return NotFound();
        }

        await auditLogService.RecordAsync(
            action: "delete",
            summary: $"Quick reply '{existing?.Title}' deleted",
            entityType: "QuickReply",
            entityId: id.ToString(),
            ct: cancellationToken);

        return NoContent();
    }

    private BadRequestObjectResult Invalid(string error) => BadRequest(new { error });

    private ObjectResult DuplicateTitle() => Conflict(new { error = "duplicate_quick_reply_title" });
}
