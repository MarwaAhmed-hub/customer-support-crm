using System.Security.Claims;

namespace CustomerSupportCrm.Api.Auth;

/// <summary>
/// The one place the JWT's claims are read by name.
/// </summary>
/// <remarks>
/// Centralizing claim-name strings here instead of scattering them across call sites. Reading claims
/// by their issued names works because <c>MapInboundClaims = false</c> is set on the JWT bearer
/// options.
/// </remarks>
public static class ClaimsPrincipalExtensions
{
    public static Guid? GetUserId(this ClaimsPrincipal principal)
    {
        var sub = principal.FindFirstValue("sub");
        return Guid.TryParse(sub, out var id) ? id : null;
    }

    /// <summary>
    /// Every effective permission code carried on the token (Story 03). One "permission" claim per
    /// code — see the comment on <see cref="JwtTokenService.IssueAccessToken"/> for how a multi-value
    /// claim becomes several individual <see cref="Claim"/> instances of the same type.
    /// </summary>
    public static IEnumerable<string> Permissions(this ClaimsPrincipal principal) =>
        principal.FindAll("permission").Select(c => c.Value);

    public static bool HasPermission(this ClaimsPrincipal principal, string permission) =>
        principal.HasClaim("permission", permission);
}
