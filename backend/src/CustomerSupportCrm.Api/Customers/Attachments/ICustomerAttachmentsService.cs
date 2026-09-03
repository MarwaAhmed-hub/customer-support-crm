namespace CustomerSupportCrm.Api.Customers.Attachments;

public enum CustomerAttachmentUploadOutcome
{
    Success,
    CustomerNotFound,
    Empty,
    TooLarge,
    InvalidType,
}

public sealed record CustomerAttachmentUploadResult(CustomerAttachmentUploadOutcome Outcome, CustomerAttachmentDto? Attachment = null)
{
    public static CustomerAttachmentUploadResult Success(CustomerAttachmentDto attachment) => new(CustomerAttachmentUploadOutcome.Success, attachment);
    public static readonly CustomerAttachmentUploadResult CustomerNotFound = new(CustomerAttachmentUploadOutcome.CustomerNotFound);
    public static readonly CustomerAttachmentUploadResult Empty = new(CustomerAttachmentUploadOutcome.Empty);
    public static readonly CustomerAttachmentUploadResult TooLarge = new(CustomerAttachmentUploadOutcome.TooLarge);
    public static readonly CustomerAttachmentUploadResult InvalidType = new(CustomerAttachmentUploadOutcome.InvalidType);
}

/// <summary>A file's bytes plus the metadata needed to stream it back out. Returned by <see cref="ICustomerAttachmentsService.OpenReadAsync"/>.</summary>
public sealed record CustomerAttachmentContent(Stream Content, string ContentType, string FileName);

/// <summary>
/// Files attached to a customer. Physical bytes are stored outside <c>wwwroot</c> (see
/// <see cref="CustomerAttachmentsService"/>'s remarks) so a direct static-file request can never
/// reach them — every read goes through <see cref="OpenReadAsync"/>, which the controller only calls
/// after <c>[HasPermission(Permissions.Customers.AttachmentsRead)]</c> has already passed.
/// </summary>
public interface ICustomerAttachmentsService
{
    /// <summary>A null return means the customer does not exist — the controller turns that into a 404; an empty (non-null) list means the customer exists with no attachments yet.</summary>
    Task<IReadOnlyList<CustomerAttachmentDto>?> ListAsync(Guid customerId, CancellationToken cancellationToken = default);

    Task<CustomerAttachmentUploadResult> UploadAsync(
        Guid customerId, Guid? actorUserId, Stream fileStream, string fileName, string contentType, long length,
        CancellationToken cancellationToken = default);

    /// <summary>A null return covers both "customer not found" and "attachment not found".</summary>
    Task<CustomerAttachmentContent?> OpenReadAsync(Guid customerId, Guid attachmentId, CancellationToken cancellationToken = default);

    /// <summary>False covers both "customer not found" and "attachment not found". Removes the DB row and the physical file (a missing file on disk is tolerated, not thrown).</summary>
    Task<bool> DeleteAsync(Guid customerId, Guid attachmentId, CancellationToken cancellationToken = default);
}
