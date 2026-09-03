using System.Text;
using CustomerSupportCrm.Domain.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CustomerSupportCrm.Api.Auth;

public record AccessToken(string Token, DateTimeOffset ExpiresAt);

/// <summary>
/// Issues signed JWT access tokens.
/// </summary>
/// <remarks>
/// <b>The token payload is an additive-only contract.</b>
/// <list type="bullet">
/// <item>Claims may only be <b>added</b>. Never rename or repurpose <c>sub</c>, <c>email</c>,
/// <c>name</c>, <c>iss</c>, <c>aud</c>, <c>exp</c>.</item>
/// <item><c>role</c> (single string) is emitted conditionally as of Story 02, for
/// <see cref="Domain.Users.User.IsAdmin"/> accounts only, and is kept exactly as-is by Story 03 for
/// backward compatibility with already-issued tokens and the pre-existing
/// <c>[Authorize(Policy = "Admin")]</c> call sites — see the comment at the call site below.</item>
/// <item><c>permission</c> (Story 03): one claim per effective permission code the caller has via
/// their roles. Consumers must ignore unknown claims rather than validating a closed shape.</item>
/// </list>
/// </remarks>
public class JwtTokenService(IOptions<JwtOptions> options)
{
    private readonly JwtOptions _options = options.Value;

    /// <param name="permissions">
    /// The caller's effective permission codes (union across their roles), computed by
    /// <see cref="Infrastructure.Persistence.IUserPermissionsQuery"/>. Taken as a parameter rather
    /// than resolved from a query service inside this class so this class can stay a cheap,
    /// stateless singleton — the query needs the scoped <c>CrmDbContext</c>, and callers
    /// (<see cref="Auth.AuthController"/>) already have the permissions in hand for the response body
    /// anyway, so fetching them twice would be wasted work.
    /// </param>
    public AccessToken IssueAccessToken(User user, IReadOnlyList<string> permissions)
    {
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddMinutes(_options.AccessTokenMinutes);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));

        // Written to the payload verbatim — no inbound/outbound claim-type mapping.
        var claims = new Dictionary<string, object>
        {
            ["sub"] = user.Id.ToString("D"),
            ["email"] = user.Email,
            ["name"] = user.DisplayName,
            // Gives the future audit-logs story a token correlation id at no cost.
            ["jti"] = Guid.NewGuid().ToString("D"),
        };

        if (user.IsAdmin)
        {
            // A single hard-coded role, not a real roles/permissions model — superseded by the
            // "permission" claims below, but kept unchanged: RoleClaimType = "role" was already
            // declared in Program.cs by Story 01, existing [Authorize(Policy = "Admin")] call sites
            // still read it, and already-issued tokens must keep validating the same way until they
            // expire (see the intake's backward-compatibility requirement).
            claims["role"] = "Admin";
        }

        if (permissions.Count > 0)
        {
            // A JSON-array claim value is exploded into one Claim per element with the same claim
            // type when the token is validated — the same mechanism ASP.NET Core uses for multi-
            // valued "role" claims — so this yields one "permission" claim per code, exactly what
            // ClaimsPrincipalExtensions.Permissions()/HasPermission() expect to read.
            claims["permission"] = permissions.ToArray();
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            Claims = claims,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };

        var token = new JsonWebTokenHandler().CreateToken(descriptor);
        return new AccessToken(token, expires);
    }
}
