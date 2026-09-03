using System.ComponentModel.DataAnnotations;

namespace CustomerSupportCrm.Api.Tickets.Priorities;

public sealed record TicketPriorityDto(Guid Id, string Name, int SortOrder, string? Description, bool IsActive, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);

public sealed record CreateTicketPriorityRequest(
    [Required, StringLength(128, MinimumLength = 1)] string Name,
    int SortOrder,
    [StringLength(512)] string? Description);

public sealed record UpdateTicketPriorityRequest(
    [Required, StringLength(128, MinimumLength = 1)] string Name,
    int SortOrder,
    [StringLength(512)] string? Description,
    bool IsActive);
