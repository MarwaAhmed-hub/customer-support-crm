using System.ComponentModel.DataAnnotations;

namespace CustomerSupportCrm.Api.SystemSettings;

public sealed record SystemSettingsDto(
    string ApplicationName,
    string SupportEmail,
    string DefaultTimezone,
    string DefaultCulture,
    string BrandDisplayName,
    string? LogoUrl,
    string PrimaryColor,
    string SecondaryColor,
    DateTime UpdatedAtUtc);

// Attributes target the primary-constructor parameters directly, not the generated properties —
// matching Departments/DepartmentDtos.cs and Roles/RoleDtos.cs. [Required]/[StringLength] here are
// belt-and-suspenders for [ApiController]'s automatic 400; SystemSettingsService re-validates every
// field (including the hex-color/URL rules attributes cannot express) and is the actual source of
// the 400 responses this endpoint returns.
public sealed record UpdateSystemSettingsRequest(
    [Required, StringLength(120, MinimumLength = 1)] string ApplicationName,
    [Required, StringLength(200, MinimumLength = 1)] string SupportEmail,
    [Required, StringLength(100, MinimumLength = 1)] string DefaultTimezone,
    [Required, StringLength(20, MinimumLength = 1)] string DefaultCulture,
    [Required, StringLength(120, MinimumLength = 1)] string BrandDisplayName,
    [StringLength(500)] string? LogoUrl,
    [Required] string PrimaryColor,
    [Required] string SecondaryColor);

/// <summary>
/// Response of <c>POST /api/system-settings/logo</c> — the URL the uploaded file now lives at.
/// The caller still has to include it in a subsequent <see cref="UpdateSystemSettingsRequest"/> and
/// PUT it to actually persist it as the branding logo; uploading alone does not change the row.
/// </summary>
public sealed record UploadLogoResponse(string LogoUrl);
