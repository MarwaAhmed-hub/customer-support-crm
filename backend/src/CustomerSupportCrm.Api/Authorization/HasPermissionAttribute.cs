using Microsoft.AspNetCore.Authorization;

namespace CustomerSupportCrm.Api.Authorization;

/// <summary>
/// Declares that an endpoint requires a specific permission. Sets the inherited <c>Policy</c> to
/// "perm:{permission}", which <see cref="PermissionPolicyProvider"/> recognises by prefix and turns
/// into a one-off <see cref="AuthorizationPolicy"/> built around a <see cref="PermissionRequirement"/>
/// — so no policy needs pre-registering per permission code.
/// </summary>
public sealed class HasPermissionAttribute : AuthorizeAttribute
{
    public HasPermissionAttribute(string permission)
    {
        Permission = permission;
        Policy = PermissionPolicyProvider.PolicyPrefix + permission;
    }

    public string Permission { get; }
}
