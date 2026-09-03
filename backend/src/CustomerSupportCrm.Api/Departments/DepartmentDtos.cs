using System.ComponentModel.DataAnnotations;

namespace CustomerSupportCrm.Api.Departments;

public sealed record DepartmentDto(Guid Id, string Name, string? Code, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

// Attributes target the primary-constructor parameters directly, not the generated properties —
// matching Roles/RoleDtos.cs and Users/UserDtos.cs.
public sealed record CreateDepartmentRequest(
    [Required, StringLength(128, MinimumLength = 1)] string Name,
    [StringLength(32)] string? Code);

public sealed record UpdateDepartmentRequest(
    [Required, StringLength(128, MinimumLength = 1)] string Name,
    [StringLength(32)] string? Code,
    bool IsActive);
