using System.ComponentModel.DataAnnotations;

namespace CustomerSupportCrm.Api.Branches;

public sealed record BranchDto(Guid Id, string Name, string? Code, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

// Attributes target the primary-constructor parameters directly, not the generated properties —
// matching Roles/RoleDtos.cs, Users/UserDtos.cs, and Departments/DepartmentDtos.cs.
public sealed record CreateBranchRequest(
    [Required, StringLength(128, MinimumLength = 1)] string Name,
    [StringLength(32)] string? Code);

public sealed record UpdateBranchRequest(
    [Required, StringLength(128, MinimumLength = 1)] string Name,
    [StringLength(32)] string? Code,
    bool IsActive);
