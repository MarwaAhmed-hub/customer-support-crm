using System.ComponentModel.DataAnnotations;

namespace CustomerSupportCrm.Api.Roles;

public sealed record RoleDto(Guid Id, string Name, string? Description, bool IsSystem, IReadOnlyList<string> Permissions);

public sealed record CreateRoleRequest(
    [Required, StringLength(128, MinimumLength = 1)] string Name,
    [StringLength(512)] string? Description);

public sealed record UpdateRoleRequest(
    [Required, StringLength(128, MinimumLength = 1)] string Name,
    [StringLength(512)] string? Description);

public sealed record ReplaceRolePermissionsRequest(IReadOnlyList<string> Permissions);

public sealed record AssignRoleRequest(Guid RoleId);
