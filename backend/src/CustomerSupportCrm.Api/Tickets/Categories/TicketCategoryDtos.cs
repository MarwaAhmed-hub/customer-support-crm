using System.ComponentModel.DataAnnotations;

namespace CustomerSupportCrm.Api.Tickets.Categories;

/// <summary><c>DepartmentId</c>/<c>DepartmentName</c> (null = no department) drive the ticket detail page's assignee picker — see the remarks on <c>Domain.Tickets.TicketCategory.DepartmentId</c>.</summary>
public sealed record TicketCategoryDto(Guid Id, string Name, string? Description, bool IsActive, Guid? DepartmentId, string? DepartmentName, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

// Attributes target the primary-constructor parameters directly, not the generated properties —
// matching Departments/DepartmentDtos.cs.
public sealed record CreateTicketCategoryRequest(
    [Required, StringLength(128, MinimumLength = 1)] string Name,
    [StringLength(512)] string? Description,
    Guid? DepartmentId = null);

public sealed record UpdateTicketCategoryRequest(
    [Required, StringLength(128, MinimumLength = 1)] string Name,
    [StringLength(512)] string? Description,
    bool IsActive,
    Guid? DepartmentId = null);
