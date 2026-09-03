using System.Security.Claims;
using CustomerSupportCrm.Api.Auth;
using Xunit;

namespace CustomerSupportCrm.Api.Tests.Auth;

public class ClaimsPrincipalExtensionsTests
{
    private static ClaimsPrincipal PrincipalWith(params string[] permissions)
    {
        var claims = permissions.Select(p => new Claim("permission", p));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, authenticationType: "test"));
    }

    [Fact]
    public void HasPermission_is_true_for_a_carried_permission()
    {
        var principal = PrincipalWith("users.view", "roles.view");

        Assert.True(principal.HasPermission("users.view"));
    }

    [Fact]
    public void HasPermission_is_false_for_a_permission_not_carried()
    {
        var principal = PrincipalWith("roles.view");

        Assert.False(principal.HasPermission("users.view"));
    }

    [Fact]
    public void Permissions_returns_every_permission_claim_value()
    {
        var principal = PrincipalWith("users.view", "users.create", "roles.view");

        Assert.Equal(["users.view", "users.create", "roles.view"], principal.Permissions());
    }

    [Fact]
    public void Permissions_is_empty_when_no_permission_claims_are_present()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity());

        Assert.Empty(principal.Permissions());
    }
}
