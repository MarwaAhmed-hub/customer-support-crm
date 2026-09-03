using System.ComponentModel.DataAnnotations;

namespace CustomerSupportCrm.Api.Users;

/// <summary><c>DepartmentId</c>/<c>BranchId</c> and their denormalised names (Story 04) are additive on top of Story 03's shape.</summary>
public sealed record UserListItemDto(
    Guid Id, string Email, string DisplayName, bool IsActive,
    Guid? DepartmentId, string? DepartmentName, Guid? BranchId, string? BranchName);

/// <summary><c>Roles</c> (Story 03) and <c>DepartmentId</c>/<c>BranchId</c> (Story 04) are additive on top of Story 02's shape.</summary>
public sealed record UserDetailDto(
    Guid Id, string Email, string DisplayName, bool IsActive, DateTimeOffset CreatedAt, IReadOnlyList<UserRoleDto> Roles,
    Guid? DepartmentId, string? DepartmentName, Guid? BranchId, string? BranchName);

// Attributes target the primary-constructor parameters directly, not the generated properties —
// matching Auth/Dtos.cs. MVC throws InvalidOperationException at bind time for `[property: ...]` on
// a record parameter ("validation metadata must be associated with the constructor parameter").
public sealed record CreateUserRequest(
    [Required, EmailAddress, StringLength(256)] string Email,
    // 128 matches the DisplayName column's HasMaxLength(128) in CrmDbContext.
    [Required, StringLength(128, MinimumLength = 1)] string DisplayName,
    [Required, StringLength(200, MinimumLength = 8)] string Password,
    Guid? DepartmentId = null,
    Guid? BranchId = null);

public sealed record UpdateUserRequest(
    [Required, EmailAddress, StringLength(256)] string Email,
    [Required, StringLength(128, MinimumLength = 1)] string DisplayName,
    Guid? DepartmentId = null,
    Guid? BranchId = null);

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int Total);
