using CustomerSupportCrm.Api.Auth;
using CustomerSupportCrm.Api.Authorization;
using CustomerSupportCrm.Domain.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupportCrm.Api.SystemSettings;

/// <summary>
/// Read/update the single-tenant system configuration and branding row. No create/delete endpoint —
/// there is exactly one row; <see cref="Update"/> upserts it. See
/// <see cref="SystemSettingsService"/> for the business rules.
/// </summary>
[ApiController]
[Route("api/system-settings")]
[Authorize]
public sealed class SystemSettingsController(ISystemSettingsService systemSettingsService, IWebHostEnvironment environment) : ControllerBase
{
    private const long MaxLogoBytes = 2 * 1024 * 1024; // 2 MB

    private static readonly Dictionary<string, string> AllowedLogoContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/png"] = ".png",
        ["image/jpeg"] = ".jpg",
        ["image/gif"] = ".gif",
        ["image/webp"] = ".webp",
        ["image/svg+xml"] = ".svg",
    };

    [HasPermission(Permissions.SystemConfig.View)]
    [HttpGet]
    public async Task<ActionResult<SystemSettingsDto>> Get(CancellationToken cancellationToken) =>
        Ok(await systemSettingsService.GetAsync(cancellationToken));

    [HasPermission(Permissions.SystemConfig.Update)]
    [HttpPut]
    public async Task<ActionResult<SystemSettingsDto>> Update(UpdateSystemSettingsRequest request, CancellationToken cancellationToken)
    {
        var updatedByUserId = User.GetUserId();
        var result = await systemSettingsService.UpdateAsync(request, updatedByUserId, cancellationToken);
        return result.Outcome switch
        {
            SystemSettingsOperationOutcome.Success => Ok(result.Settings),
            SystemSettingsOperationOutcome.InvalidApplicationName => Invalid("invalid_application_name"),
            SystemSettingsOperationOutcome.InvalidSupportEmail => Invalid("invalid_support_email"),
            SystemSettingsOperationOutcome.InvalidDefaultTimezone => Invalid("invalid_default_timezone"),
            SystemSettingsOperationOutcome.InvalidDefaultCulture => Invalid("invalid_default_culture"),
            SystemSettingsOperationOutcome.InvalidBrandDisplayName => Invalid("invalid_brand_display_name"),
            SystemSettingsOperationOutcome.InvalidLogoUrl => Invalid("invalid_logo_url"),
            SystemSettingsOperationOutcome.InvalidPrimaryColor => Invalid("invalid_primary_color"),
            SystemSettingsOperationOutcome.InvalidSecondaryColor => Invalid("invalid_secondary_color"),
            _ => Problem(statusCode: 500),
        };
    }

    /// <summary>
    /// Uploads a logo image file and returns the absolute URL it now lives at — the "upload from
    /// device" convenience the plain URL-only <see cref="Update"/> field doesn't offer on its own.
    /// This alone does not change the persisted branding: the caller still PUTs the returned URL
    /// back via <see cref="Update"/>, exactly like pasting an externally-hosted URL would.
    /// </summary>
    [HasPermission(Permissions.SystemConfig.Update)]
    [HttpPost("logo")]
    [RequestSizeLimit(MaxLogoBytes)]
    public async Task<ActionResult<UploadLogoResponse>> UploadLogo(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length == 0)
        {
            return Invalid("logo_file_required");
        }

        if (file.Length > MaxLogoBytes)
        {
            return Invalid("logo_file_too_large");
        }

        if (!AllowedLogoContentTypes.TryGetValue(file.ContentType, out var extension))
        {
            return Invalid("logo_file_type_not_supported");
        }

        var webRoot = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
        var logosDirectory = Path.Combine(webRoot, "uploads", "logos");
        Directory.CreateDirectory(logosDirectory);

        // A fresh Guid name — never the caller-supplied file name — so nothing about the upload
        // (path traversal, an unexpected double extension, a collision with another admin's file)
        // makes it onto disk.
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(logosDirectory, fileName);

        await using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream, cancellationToken);

        var logoUrl = $"{Request.Scheme}://{Request.Host}/uploads/logos/{fileName}";
        return Ok(new UploadLogoResponse(logoUrl));
    }

    private BadRequestObjectResult Invalid(string error) => BadRequest(new { error });
}
