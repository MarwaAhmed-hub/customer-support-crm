using Microsoft.AspNetCore.Authorization;

namespace CustomerSupportCrm.Api.Authorization;

public sealed class PermissionRequirement(string permission) : IAuthorizationRequirement
{
    public string Permission { get; } = permission;
}
