using CustomerSupportCrm.Api.Auth;
using Microsoft.AspNetCore.Authorization;

namespace CustomerSupportCrm.Api.Authorization;

/// <summary>
/// Succeeds when the current principal carries a "permission" claim matching the requirement.
/// An authenticated caller who lacks it is neither succeeded nor failed explicitly — ASP.NET Core
/// treats "no handler succeeded" as Forbidden (403), which is exactly the contract this story wants
/// (403, not 401, for an authenticated-but-unauthorized caller).
/// </summary>
public sealed class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        if (context.User.HasPermission(requirement.Permission))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
