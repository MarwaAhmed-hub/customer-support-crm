using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using SystemSettingEntity = CustomerSupportCrm.Domain.SystemSettings.SystemSetting;

namespace CustomerSupportCrm.Api.SystemSettings;

public enum SystemSettingsOperationOutcome
{
    Success,
    InvalidApplicationName,
    InvalidSupportEmail,
    InvalidDefaultTimezone,
    InvalidDefaultCulture,
    InvalidBrandDisplayName,
    InvalidLogoUrl,
    InvalidPrimaryColor,
    InvalidSecondaryColor,
}

public sealed record SystemSettingsResult(SystemSettingsOperationOutcome Outcome, SystemSettingsDto? Settings = null)
{
    public static SystemSettingsResult Success(SystemSettingsDto settings) => new(SystemSettingsOperationOutcome.Success, settings);
    public static readonly SystemSettingsResult InvalidApplicationName = new(SystemSettingsOperationOutcome.InvalidApplicationName);
    public static readonly SystemSettingsResult InvalidSupportEmail = new(SystemSettingsOperationOutcome.InvalidSupportEmail);
    public static readonly SystemSettingsResult InvalidDefaultTimezone = new(SystemSettingsOperationOutcome.InvalidDefaultTimezone);
    public static readonly SystemSettingsResult InvalidDefaultCulture = new(SystemSettingsOperationOutcome.InvalidDefaultCulture);
    public static readonly SystemSettingsResult InvalidBrandDisplayName = new(SystemSettingsOperationOutcome.InvalidBrandDisplayName);
    public static readonly SystemSettingsResult InvalidLogoUrl = new(SystemSettingsOperationOutcome.InvalidLogoUrl);
    public static readonly SystemSettingsResult InvalidPrimaryColor = new(SystemSettingsOperationOutcome.InvalidPrimaryColor);
    public static readonly SystemSettingsResult InvalidSecondaryColor = new(SystemSettingsOperationOutcome.InvalidSecondaryColor);
}

/// <summary>
/// Business rules for the single-tenant system settings row: required non-empty strings, a valid
/// e-mail for <see cref="SystemSettingEntity.SupportEmail"/>, <c>#RRGGBB</c> hex colors, and an
/// absolute <c>http(s)://…</c> URL when a logo is set. Modeled on
/// <c>Departments.DepartmentsService</c> — read-or-create instead of duplicate-name rejection, since
/// there is exactly one row (<see cref="SystemSettingEntity.Id"/> = 1) instead of many.
/// </summary>
public interface ISystemSettingsService
{
    Task<SystemSettingsDto> GetAsync(CancellationToken cancellationToken = default);

    Task<SystemSettingsResult> UpdateAsync(UpdateSystemSettingsRequest request, Guid? updatedByUserId, CancellationToken cancellationToken = default);
}

public sealed class SystemSettingsService(CrmDbContext db) : ISystemSettingsService
{
    private const int RowId = 1;

    private static readonly Regex HexColorPattern = new("^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

    public async Task<SystemSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        var settings = await db.SystemSettings.AsNoTracking().SingleOrDefaultAsync(s => s.Id == RowId, cancellationToken);

        // Empty database / missing row (e.g. a rolled-back seeder): fall back to the same defaults
        // DbSeeder would have inserted, rather than 500ing. The next PUT upserts the real row.
        return settings is null ? DefaultDto() : ToDto(settings);
    }

    public async Task<SystemSettingsResult> UpdateAsync(UpdateSystemSettingsRequest request, Guid? updatedByUserId, CancellationToken cancellationToken = default)
    {
        var applicationName = request.ApplicationName.Trim();
        if (applicationName.Length == 0 || applicationName.Length > 120)
        {
            return SystemSettingsResult.InvalidApplicationName;
        }

        var supportEmail = request.SupportEmail.Trim();
        if (supportEmail.Length == 0 || supportEmail.Length > 200 || !new EmailAddressAttribute().IsValid(supportEmail))
        {
            return SystemSettingsResult.InvalidSupportEmail;
        }

        var defaultTimezone = request.DefaultTimezone.Trim();
        if (defaultTimezone.Length == 0 || defaultTimezone.Length > 100)
        {
            return SystemSettingsResult.InvalidDefaultTimezone;
        }

        var defaultCulture = request.DefaultCulture.Trim();
        if (defaultCulture.Length == 0 || defaultCulture.Length > 20)
        {
            return SystemSettingsResult.InvalidDefaultCulture;
        }

        var brandDisplayName = request.BrandDisplayName.Trim();
        if (brandDisplayName.Length == 0 || brandDisplayName.Length > 120)
        {
            return SystemSettingsResult.InvalidBrandDisplayName;
        }

        var logoUrl = string.IsNullOrWhiteSpace(request.LogoUrl) ? null : request.LogoUrl.Trim();
        if (logoUrl is not null && !IsAbsoluteHttpUrl(logoUrl))
        {
            return SystemSettingsResult.InvalidLogoUrl;
        }

        var primaryColor = request.PrimaryColor.Trim();
        if (!HexColorPattern.IsMatch(primaryColor))
        {
            return SystemSettingsResult.InvalidPrimaryColor;
        }

        var secondaryColor = request.SecondaryColor.Trim();
        if (!HexColorPattern.IsMatch(secondaryColor))
        {
            return SystemSettingsResult.InvalidSecondaryColor;
        }

        // Read-or-create on Id = 1: covers both the normal case and the "migration ran but the
        // seeder hasn't yet" edge case documented in the story's failure modes. Concurrent PUTs are
        // last-write-wins — no optimistic concurrency token for this story.
        var settings = await db.SystemSettings.SingleOrDefaultAsync(s => s.Id == RowId, cancellationToken);
        if (settings is null)
        {
            settings = new SystemSettingEntity { Id = RowId };
            db.SystemSettings.Add(settings);
        }

        settings.ApplicationName = applicationName;
        settings.SupportEmail = supportEmail;
        settings.DefaultTimezone = defaultTimezone;
        settings.DefaultCulture = defaultCulture;
        settings.BrandDisplayName = brandDisplayName;
        settings.LogoUrl = logoUrl;
        settings.PrimaryColor = primaryColor;
        settings.SecondaryColor = secondaryColor;
        settings.UpdatedAtUtc = DateTime.UtcNow;
        settings.UpdatedByUserId = updatedByUserId;

        await db.SaveChangesAsync(cancellationToken);

        return SystemSettingsResult.Success(ToDto(settings));
    }

    private static bool IsAbsoluteHttpUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static SystemSettingsDto ToDto(SystemSettingEntity settings) => new(
        settings.ApplicationName,
        settings.SupportEmail,
        settings.DefaultTimezone,
        settings.DefaultCulture,
        settings.BrandDisplayName,
        settings.LogoUrl,
        settings.PrimaryColor,
        settings.SecondaryColor,
        settings.UpdatedAtUtc);

    private static SystemSettingsDto DefaultDto() => new(
        "Customer Support CRM",
        "support@localhost",
        "UTC",
        "en-US",
        "Customer Support CRM",
        null,
        "#1976D2",
        "#9C27B0",
        DateTime.UtcNow);
}
