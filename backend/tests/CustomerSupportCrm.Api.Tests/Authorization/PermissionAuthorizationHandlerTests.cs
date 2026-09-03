using System.Security.Claims;
using CustomerSupportCrm.Api.Authorization;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace CustomerSupportCrm.Api.Tests.Authorization;

public class PermissionAuthorizationHandlerTests
{
    private static AuthorizationHandlerContext ContextFor(ClaimsPrincipal user, string permission)
    {
        var requirement = new PermissionRequirement(permission);
        return new AuthorizationHandlerContext([requirement], user, resource: null);
    }

    private static ClaimsPrincipal PrincipalWith(params string[] permissions)
    {
        var claims = permissions.Select(p => new Claim("permission", p));
        var identity = new ClaimsIdentity(claims, authenticationType: "test");
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public async Task Succeeds_when_the_principal_has_the_required_permission_claim()
    {
        var handler = new PermissionAuthorizationHandler();
        var context = ContextFor(PrincipalWith("users.view", "roles.view"), "users.view");

        await handler.HandleAsync(context);

        Assert.True(context.HasSucceeded);
    }

    [Fact]
    public async Task Does_not_succeed_when_the_permission_claim_is_absent()
    {
        var handler = new PermissionAuthorizationHandler();
        var context = ContextFor(PrincipalWith("roles.view"), "users.view");

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }

    [Fact]
    public async Task Does_not_succeed_for_an_unauthenticated_principal_with_no_claims()
    {
        var handler = new PermissionAuthorizationHandler();
        var context = ContextFor(new ClaimsPrincipal(new ClaimsIdentity()), "users.view");

        await handler.HandleAsync(context);

        Assert.False(context.HasSucceeded);
    }
}
