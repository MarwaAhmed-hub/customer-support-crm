using System.ComponentModel.DataAnnotations;

namespace CustomerSupportCrm.Api.Tickets.Tickets;

/// <summary>Story 22: null when the ticket has no <c>TicketSla</c> row (pre-dates the migration, or its policy was missing at creation) — see <c>ISlaService</c>. <c>FirstResponseStatus</c>/<c>ResolutionStatus</c> are lazily evaluated as of the moment this DTO was built, so a Running clock past its due time already reads as "breached" here even if the write-through hasn't landed yet.</summary>
public sealed record TicketSlaDto(
    DateTimeOffset StartedAt,
    DateTimeOffset FirstResponseDueAt,
    DateTimeOffset ResolutionDueAt,
    string FirstResponseStatus,
    string ResolutionStatus,
    DateTimeOffset? FirstResponseAt,
    DateTimeOffset? ResolvedAt);

public sealed record TicketListItemDto(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    string Subject,
    Guid CategoryId,
    string CategoryName,
    Guid PriorityId,
    string PriorityName,
    string Status,
    Guid CreatedByUserId,
    string? CreatedByUserName,
    Guid? AssignedUserId,
    string? AssignedUserName,
    bool IsEscalated,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? SourceChannel,
    TicketSlaDto? Sla = null);

public sealed record TicketDetailDto(
    Guid Id,
    Guid CustomerId,
    string CustomerName,
    string Subject,
    string Description,
    Guid CategoryId,
    string CategoryName,
    Guid PriorityId,
    string PriorityName,
    string Status,
    Guid CreatedByUserId,
    string? CreatedByUserName,
    Guid? AssignedUserId,
    string? AssignedUserName,
    bool IsEscalated,
    DateTimeOffset? EscalatedAt,
    Guid? EscalatedByUserId,
    string? EscalatedByUserName,
    string? EscalationReason,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string? SourceChannel,
    TicketSlaDto? Sla = null,
    // The ticket's category's department (null if the category has none) — drives the assignee
    // picker's department filter on TicketDetailPage.
    Guid? CategoryDepartmentId = null);

// Attributes target the primary-constructor parameters directly, not the generated properties —
// matching Departments/DepartmentDtos.cs.
public sealed record CreateTicketRequest(
    [Required] Guid CustomerId,
    [Required, StringLength(200, MinimumLength = 1)] string Subject,
    [Required, StringLength(4000, MinimumLength = 1)] string Description,
    [Required] Guid CategoryId,
    [Required] Guid PriorityId);

public sealed record UpdateTicketRequest(
    [Required, StringLength(200, MinimumLength = 1)] string Subject,
    [Required, StringLength(4000, MinimumLength = 1)] string Description,
    [Required] Guid CategoryId,
    [Required] Guid PriorityId);

/// <summary>A null <see cref="AssignedUserId"/> unassigns the ticket — there is no separate "unassign" endpoint.</summary>
public sealed record UpdateTicketAssignmentRequest(Guid? AssignedUserId);

/// <summary>Story 13: <see cref="Status"/> must be one of <see cref="Domain.Tickets.TicketStatuses.All"/> and a valid transition from the ticket's current status.</summary>
public sealed record UpdateTicketStatusRequest([Required] string Status);

/// <summary>Story 13: <see cref="Reason"/> is required — trimmed and validated non-empty by <c>TicketsService.EscalateAsync</c>.</summary>
public sealed record EscalateTicketRequest([Required] string Reason);

/// <summary>Story 19: <see cref="Body"/> is required — trimmed and validated non-empty by <c>TicketEmailReplyService.SendReplyAsync</c>.</summary>
public sealed record SendTicketEmailReplyRequest([Required, StringLength(4000, MinimumLength = 1)] string Body);

/// <summary>Story 20: <see cref="Body"/> is required — trimmed and validated non-empty by <c>TicketChannelReplyService.SendReplyAsync</c>.</summary>
public sealed record SendTicketChannelReplyRequest([Required, StringLength(4000, MinimumLength = 1)] string Body);
