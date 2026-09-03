namespace CustomerSupportCrm.Api.Customers.Attachments;

public sealed record CustomerAttachmentDto(
    Guid Id,
    Guid CustomerId,
    string FileName,
    string ContentType,
    long SizeBytes,
    Guid? UploadedByUserId,
    string? UploadedByDisplayName,
    DateTime UploadedAt,
    string DownloadUrl);
