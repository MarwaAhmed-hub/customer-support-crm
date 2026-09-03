namespace CustomerSupportCrm.Domain.SystemSettings;

/// <summary>
/// Single-tenant system configuration and branding. Exactly one row exists, keyed by
/// <see cref="Id"/> = 1 — enforced by <c>SystemSettingsService</c> and the seeder, not by a database
/// constraint (there is nothing stopping a second row at the schema level, same trust boundary as
/// every other admin-only table in this codebase).
/// </summary>
/// <remarks>
/// A plain, settable POCO — matching <see cref="Departments.Department"/>'s style. Business rules
/// (required fields, e-mail/hex-color/URL validation) live in
/// <c>CustomerSupportCrm.Api.SystemSettings.SystemSettingsService</c>, not here.
/// </remarks>
public class SystemSetting
{
    public int Id { get; set; } = 1;

    public string ApplicationName { get; set; } = default!;

    public string SupportEmail { get; set; } = default!;

    public string DefaultTimezone { get; set; } = "UTC";

    public string DefaultCulture { get; set; } = "en-US";

    /// <summary>Falls back to <see cref="ApplicationName"/> on the UI when blank.</summary>
    public string BrandDisplayName { get; set; } = default!;

    /// <summary>Absolute <c>http(s)://…</c> URL when present. No binary upload — URL only (out of scope for this story).</summary>
    public string? LogoUrl { get; set; }

    /// <summary><c>#RRGGBB</c> hex.</summary>
    public string PrimaryColor { get; set; } = "#1976D2";

    /// <summary><c>#RRGGBB</c> hex.</summary>
    public string SecondaryColor { get; set; } = "#9C27B0";

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    /// <summary>No navigation property — same pattern as <see cref="Audit.AuditLog.ActorUserId"/>: a soft reference only, never joined.</summary>
    public Guid? UpdatedByUserId { get; set; }
}
