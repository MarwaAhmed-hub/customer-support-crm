using CustomerSupportCrm.Api.Auth;
using CustomerSupportCrm.Api.Authorization;
using CustomerSupportCrm.Domain.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupportCrm.Api.Customers.Attachments;

/// <summary>List/upload/download/delete files attached to a customer — see <see cref="CustomerAttachmentsService"/> for validation and storage rules.</summary>
[ApiController]
[Route("api/customers/{customerId:guid}/attachments")]
[Authorize]
public class CustomerAttachmentsController(ICustomerAttachmentsService attachmentsService, ILogger<CustomerAttachmentsController> logger) : ControllerBase
{
    [HasPermission(Permissions.Customers.AttachmentsRead)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CustomerAttachmentDto>>> List(Guid customerId, CancellationToken cancellationToken)
    {
        var attachments = await attachmentsService.ListAsync(customerId, cancellationToken);
        return attachments is null ? NotFound() : Ok(attachments);
    }

    [HasPermission(Permissions.Customers.AttachmentsCreate)]
    [HttpPost]
    [RequestSizeLimit(CustomerAttachmentsService.MaxBytes)]
    public async Task<ActionResult<CustomerAttachmentDto>> Upload(Guid customerId, IFormFile file, CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();

        await using var stream = file.OpenReadStream();
        var result = await attachmentsService.UploadAsync(
            customerId, actorUserId, stream, file.FileName, file.ContentType, file.Length, cancellationToken);

        return result.Outcome switch
        {
            CustomerAttachmentUploadOutcome.Success => CreatedAtAction(nameof(List), new { customerId }, result.Attachment),
            CustomerAttachmentUploadOutcome.CustomerNotFound => NotFound(),
            CustomerAttachmentUploadOutcome.Empty => Invalid("attachment.empty"),
            CustomerAttachmentUploadOutcome.TooLarge => Invalid("attachment.too_large"),
            CustomerAttachmentUploadOutcome.InvalidType => Invalid("attachment.invalid_type"),
            _ => Problem(statusCode: 500),
        };
    }

    [HasPermission(Permissions.Customers.AttachmentsRead)]
    [HttpGet("{attachmentId:guid}/download")]
    public async Task<IActionResult> Download(Guid customerId, Guid attachmentId, CancellationToken cancellationToken)
    {
        try
        {
            var content = await attachmentsService.OpenReadAsync(customerId, attachmentId, cancellationToken);
            return content is null ? NotFound() : File(content.Content, content.ContentType, content.FileName);
        }
        catch (FileNotFoundException ex)
        {
            // The DB row exists but its physical file does not (see CustomerAttachmentsService's
            // remarks on OpenReadAsync) — a storage problem, not an ordinary "not found".
            logger.LogWarning(ex, "event={Event} customerId={CustomerId} attachmentId={AttachmentId}",
                "attachment_storage_missing", customerId, attachmentId);
            return Problem(statusCode: 500, title: "attachment.storage_missing");
        }
    }

    [HasPermission(Permissions.Customers.AttachmentsDelete)]
    [HttpDelete("{attachmentId:guid}")]
    public async Task<IActionResult> Delete(Guid customerId, Guid attachmentId, CancellationToken cancellationToken)
    {
        var deleted = await attachmentsService.DeleteAsync(customerId, attachmentId, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    private BadRequestObjectResult Invalid(string error) => BadRequest(new { error });
}
