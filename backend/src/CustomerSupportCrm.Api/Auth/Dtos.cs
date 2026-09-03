using System.ComponentModel.DataAnnotations;

namespace CustomerSupportCrm.Api.Auth;

// The attributes target the primary-constructor *parameters*, not the generated properties.
// MVC throws InvalidOperationException at bind time for `[property: ...]` on a record parameter:
// "validation metadata must be associated with the constructor parameter".
public record LoginRequest(
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(256, MinimumLength = 1)] string Password);

/// <summary>
/// The minimal user projection returned by login and <c>GET /api/auth/me</c>.
/// </summary>
/// <remarks>
/// Additive-only, exactly like the JWT payload. Projecting into this DTO (rather than annotating the
/// entity with [JsonIgnore]) is what guarantees PasswordHash can never reach a response.
/// </remarks>
public record UserDto(Guid Id, string Email, string DisplayName);

/// <summary>
/// <c>Permissions</c> (Story 03) is additive: existing clients that only read <c>AccessToken</c>,
/// <c>ExpiresAt</c>, and <c>User</c> are unaffected.
/// </summary>
public record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, UserDto User, IReadOnlyList<string> Permissions);

/// <summary>The body of <c>GET /api/auth/me</c>, refreshed on every page load so the frontend's permission gates stay current without re-logging in.</summary>
public record MeResponse(Guid Id, string Email, string DisplayName, IReadOnlyList<string> Permissions);
