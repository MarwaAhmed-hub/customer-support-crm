using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace CustomerSupportCrm.Api.Authorization;

/// <summary>
/// Builds an <see cref="AuthorizationPolicy"/> on the fly for any policy name starting with
/// <see cref="PolicyPrefix"/>, instead of requiring one <c>AddPolicy(...)</c> call per permission
/// code in Program.cs. Every other policy name (including the pre-existing "Admin" and
/// "Authenticated" policies) falls through to the default provider unchanged.
/// </summary>
public sealed class PermissionPolicyProvider(IOptions<AuthorizationOptions> options) : IAuthorizationPolicyProvider
{
    public const string PolicyPrefix = "perm:";

    private readonly DefaultAuthorizationPolicyProvider _fallback = new(options);

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync() => _fallback.GetDefaultPolicyAsync();

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync() => _fallback.GetFallbackPolicyAsync();

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith(PolicyPrefix, StringComparison.Ordinal))
        {
            var permission = policyName[PolicyPrefix.Length..];
            var policy = new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .AddRequirements(new PermissionRequirement(permission))
                .Build();
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }

        return _fallback.GetPolicyAsync(policyName);
    }
}
