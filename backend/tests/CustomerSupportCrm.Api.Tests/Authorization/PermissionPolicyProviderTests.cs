using CustomerSupportCrm.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Options;
using Xunit;

namespace CustomerSupportCrm.Api.Tests.Authorization;

public class PermissionPolicyProviderTests
{
    private static PermissionPolicyProvider CreateProvider()
    {
        var options = new AuthorizationOptions();
        options.AddPolicy("Admin", p => p.RequireRole("Admin"));
        return new PermissionPolicyProvider(Options.Create(options));
    }

    [Fact]
    public async Task Resolves_a_perm_prefixed_policy_name_into_a_PermissionRequirement()
    {
        var provider = CreateProvider();

        var policy = await provider.GetPolicyAsync("perm:users.view");

        Assert.NotNull(policy);
        var requirement = Assert.Single(policy.Requirements.OfType<PermissionRequirement>());
        Assert.Equal("users.view", requirement.Permission);
        Assert.Contains(policy.Requirements, r => r is DenyAnonymousAuthorizationRequirement);
    }

    [Fact]
    public async Task Falls_through_to_the_default_provider_for_a_pre_registered_policy_name()
    {
        var provider = CreateProvider();

        var policy = await provider.GetPolicyAsync("Admin");

        Assert.NotNull(policy);
        Assert.DoesNotContain(policy.Requirements, r => r is PermissionRequirement);
    }

    [Fact]
    public async Task Returns_null_for_an_unknown_non_perm_policy_name()
    {
        var provider = CreateProvider();

        var policy = await provider.GetPolicyAsync("SomethingElse");

        Assert.Null(policy);
    }
}
