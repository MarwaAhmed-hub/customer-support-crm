using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace CustomerSupportCrm.Api.AgentDesk.Tasks;

/// <summary>
/// Computed at read time from <c>CompletedAt</c>/<c>ReminderAt</c> vs. <c>DateTime.UtcNow</c> — never
/// persisted. See <c>AgentTasksService.ComputeState</c>.
/// </summary>
/// <remarks>
/// No global <c>JsonStringEnumConverter</c> is registered in <c>Program.cs</c> (every other enum in
/// this codebase — <c>TicketOperationOutcome</c>, <c>CustomerNoteOperationOutcome</c>, etc. — stays
/// internal to a service/result type and is switched into a concrete <c>ActionResult</c> before it
/// ever reaches JSON, so none of them needed one). This is the first enum actually serialized to a
/// client, so it carries its own converter rather than changing global behavior for every future one.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AgentTaskState
{
    Pending,
    Upcoming,
    Overdue,
    Completed,
}

public sealed record AgentTaskDto(
    Guid Id,
    string Title,
    string? Description,
    DateTime? ReminderAt,
    DateTime? CompletedAt,
    AgentTaskState State,
    Guid? TicketId,
    string? TicketSubject,
    DateTime CreatedAt,
    DateTime UpdatedAt);

// Attributes target the primary-constructor parameters directly, not the generated properties —
// matching Customers/Notes/CustomerNoteDtos.cs. MinimumLength = 1 alone lets a single space through,
// so the service still rejects a whitespace-only Title after trimming — see AgentTasksService.
// TicketId defaults to null so existing 3-positional-argument call sites (tests, older clients) keep
// compiling — the linking correction is additive, not a breaking change to this DTO's shape.
public sealed record CreateAgentTaskRequest(
    [Required, StringLength(200, MinimumLength = 1)] string Title,
    [StringLength(4000)] string? Description,
    DateTime? ReminderAt,
    Guid? TicketId = null);

public sealed record UpdateAgentTaskRequest(
    [Required, StringLength(200, MinimumLength = 1)] string Title,
    [StringLength(4000)] string? Description,
    DateTime? ReminderAt,
    Guid? TicketId = null);
