using CustomerSupportCrm.Domain.Customers;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupportCrm.Api.Customers.Attachments;

/// <remarks>
/// <para>
/// Physical files are stored under <c>&lt;ContentRootPath&gt;/App_Data/customer-attachments/</c> —
/// deliberately <b>outside</b> <c>wwwroot</c>, unlike the branding-logo upload
/// (<c>SystemSettingsController</c>). <c>Program.cs</c> serves the whole of <c>wwwroot</c> via
/// <c>UseStaticFiles</c>, so a file placed anywhere under it is reachable by anyone with the URL, no
/// permission check involved — fine for a public logo, wrong for a customer's uploaded document.
/// Every read of an attachment's bytes goes through <see cref="OpenReadAsync"/>, called only from
/// behind <c>[HasPermission(Permissions.Customers.AttachmentsRead)]</c>.
/// </para>
/// </remarks>
public sealed class CustomerAttachmentsService(CrmDbContext db, IWebHostEnvironment environment) : ICustomerAttachmentsService
{
    // Larger than the 2 MB branding-logo cap (SystemSettingsController.MaxLogoBytes): attachments
    // here are general documents (PDF/Word/Excel), not a single small brand image.
    public const long MaxBytes = 10 * 1024 * 1024;

    // Extension (lower-invariant) -> content-types a browser plausibly reports for it. Checked after
    // the extension itself is confirmed to be in this same whitelist; "application/octet-stream" is
    // accepted everywhere because real-world OS/browser combinations commonly fall back to it for
    // anything they don't recognize by extension — a strict per-type-only match would reject valid
    // uploads far too often.
    private static readonly Dictionary<string, string[]> AllowedContentTypesByExtension = new(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = ["application/pdf"],
        [".png"] = ["image/png"],
        [".jpg"] = ["image/jpeg"],
        [".jpeg"] = ["image/jpeg"],
        [".gif"] = ["image/gif"],
        [".doc"] = ["application/msword"],
        [".docx"] = ["application/vnd.openxmlformats-officedocument.wordprocessingml.document"],
        [".xls"] = ["application/vnd.ms-excel"],
        [".xlsx"] = ["application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"],
        [".txt"] = ["text/plain"],
        [".csv"] = ["text/csv", "application/vnd.ms-excel"],
    };

    private const string OctetStream = "application/octet-stream";

    public async Task<IReadOnlyList<CustomerAttachmentDto>?> ListAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == customerId, cancellationToken))
        {
            return null;
        }

        var attachments = await db.CustomerAttachments
            .AsNoTracking()
            .Where(a => a.CustomerId == customerId)
            .OrderByDescending(a => a.UploadedAt)
            .Select(a => new
            {
                a.Id,
                a.CustomerId,
                a.FileName,
                a.ContentType,
                a.SizeBytes,
                a.UploadedByUserId,
                UploadedByDisplayName = a.UploadedByUserId != null ? a.UploadedByUser!.DisplayName : null,
                a.UploadedAt,
            })
            .ToListAsync(cancellationToken);

        return attachments
            .Select(a => new CustomerAttachmentDto(
                a.Id, a.CustomerId, a.FileName, a.ContentType, a.SizeBytes,
                a.UploadedByUserId, a.UploadedByDisplayName, a.UploadedAt, BuildDownloadUrl(a.CustomerId, a.Id)))
            .ToList();
    }

    public async Task<CustomerAttachmentUploadResult> UploadAsync(
        Guid customerId, Guid? actorUserId, Stream fileStream, string fileName, string contentType, long length,
        CancellationToken cancellationToken = default)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == customerId, cancellationToken))
        {
            return CustomerAttachmentUploadResult.CustomerNotFound;
        }

        if (length == 0)
        {
            return CustomerAttachmentUploadResult.Empty;
        }

        if (length > MaxBytes)
        {
            return CustomerAttachmentUploadResult.TooLarge;
        }

        var extension = Path.GetExtension(fileName);
        if (extension.Length == 0 || !AllowedContentTypesByExtension.TryGetValue(extension, out var allowedContentTypes))
        {
            return CustomerAttachmentUploadResult.InvalidType;
        }

        if (!allowedContentTypes.Contains(contentType, StringComparer.OrdinalIgnoreCase) &&
            !string.Equals(contentType, OctetStream, StringComparison.OrdinalIgnoreCase))
        {
            return CustomerAttachmentUploadResult.InvalidType;
        }

        var storageDirectory = GetStorageDirectory();
        Directory.CreateDirectory(storageDirectory);

        // A fresh Guid name — never the caller-supplied file name — so nothing about the upload
        // (path traversal, an unexpected double extension, a collision with another upload) makes it
        // onto disk. Same pattern as SystemSettingsController.UploadLogo.
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(storageDirectory, storedFileName);

        await using (var destination = new FileStream(filePath, FileMode.Create))
        {
            await fileStream.CopyToAsync(destination, cancellationToken);
        }

        var attachment = new CustomerAttachment
        {
            CustomerId = customerId,
            FileName = fileName,
            StoredFileName = storedFileName,
            ContentType = contentType,
            SizeBytes = length,
            UploadedByUserId = actorUserId,
        };

        db.CustomerAttachments.Add(attachment);
        await db.SaveChangesAsync(cancellationToken);

        string? uploadedByDisplayName = actorUserId is null
            ? null
            : await db.Users.Where(u => u.Id == actorUserId).Select(u => u.DisplayName).SingleOrDefaultAsync(cancellationToken);

        return CustomerAttachmentUploadResult.Success(new CustomerAttachmentDto(
            attachment.Id, attachment.CustomerId, attachment.FileName, attachment.ContentType, attachment.SizeBytes,
            attachment.UploadedByUserId, uploadedByDisplayName, attachment.UploadedAt, BuildDownloadUrl(customerId, attachment.Id)));
    }

    public async Task<CustomerAttachmentContent?> OpenReadAsync(Guid customerId, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var attachment = await db.CustomerAttachments
            .AsNoTracking()
            .SingleOrDefaultAsync(a => a.CustomerId == customerId && a.Id == attachmentId, cancellationToken);
        if (attachment is null)
        {
            return null;
        }

        var filePath = Path.Combine(GetStorageDirectory(), attachment.StoredFileName);

        // Deliberately not caught here: the row exists but the file does not (edge case — physical
        // file missing on disk). The controller catches FileNotFoundException specifically and maps
        // it to 500 attachment.storage_missing with a logged warning, rather than this method
        // returning null (which would read as an ordinary 404, hiding a real storage problem).
        var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        return new CustomerAttachmentContent(stream, attachment.ContentType, attachment.FileName);
    }

    public async Task<bool> DeleteAsync(Guid customerId, Guid attachmentId, CancellationToken cancellationToken = default)
    {
        var attachment = await db.CustomerAttachments
            .SingleOrDefaultAsync(a => a.CustomerId == customerId && a.Id == attachmentId, cancellationToken);
        if (attachment is null)
        {
            return false;
        }

        db.CustomerAttachments.Remove(attachment);
        await db.SaveChangesAsync(cancellationToken);

        // Best-effort: a missing physical file must not block removing the (now orphaned-anyway) row.
        var filePath = Path.Combine(GetStorageDirectory(), attachment.StoredFileName);
        try
        {
            File.Delete(filePath);
        }
        catch (IOException)
        {
            // Deliberately swallowed — the DB row is already gone, which is what the caller asked
            // for; a locked/missing file on disk is a cleanup nicety, not a failure to report.
        }

        return true;
    }

    private string GetStorageDirectory() =>
        Path.Combine(environment.ContentRootPath, "App_Data", "customer-attachments");

    private static string BuildDownloadUrl(Guid customerId, Guid attachmentId) =>
        $"/api/customers/{customerId}/attachments/{attachmentId}/download";
}
