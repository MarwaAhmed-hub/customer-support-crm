using CustomerSupportCrm.Domain.Users;

namespace CustomerSupportCrm.Domain.Customers;

/// <summary>
/// Metadata for a file uploaded against a <see cref="Customer"/>. The physical bytes live outside
/// <c>wwwroot</c> (see <c>CustomerAttachmentsService</c>'s remarks) — this row is the only thing a
/// direct-static-file request could never bypass to reach them; every read goes through the
/// permission-checked download endpoint.
/// </summary>
/// <remarks>A plain, settable POCO — matching <see cref="Branches.Branch"/>/<see cref="Departments.Department"/>'s style.</remarks>
public class CustomerAttachment
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CustomerId { get; set; }

    /// <summary>The caller's original file name — never used to build a path on disk.</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>GUID-based name (plus extension) the file is actually stored under.</summary>
    public string StoredFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long SizeBytes { get; set; }

    /// <summary>Nullable — the uploader's account may have since been deleted; the row and its file stay (see the SetNull FK in <c>CrmDbContext</c>).</summary>
    public Guid? UploadedByUserId { get; set; }

    public User? UploadedByUser { get; set; }

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
