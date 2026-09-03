namespace CustomerSupportCrm.Api.AgentDesk.Tasks;

public enum AgentTaskOperationOutcome
{
    Success,
    NotFound,

    /// <summary>Title is empty/whitespace-only after trimming — StringLength(MinimumLength = 1) alone lets a single space through.</summary>
    InvalidTitle,

    /// <summary>The requested <c>TicketId</c> does not reference an existing ticket — same "invalid reference" 404 shape as <c>CustomerNotesService</c>'s <c>CustomerNotFound</c>.</summary>
    TicketNotFound,
}

public sealed record AgentTaskResult(AgentTaskOperationOutcome Outcome, AgentTaskDto? Task = null)
{
    public static AgentTaskResult Success(AgentTaskDto task) => new(AgentTaskOperationOutcome.Success, task);
    public static readonly AgentTaskResult NotFound = new(AgentTaskOperationOutcome.NotFound);
    public static readonly AgentTaskResult InvalidTitle = new(AgentTaskOperationOutcome.InvalidTitle);
    public static readonly AgentTaskResult TicketNotFound = new(AgentTaskOperationOutcome.TicketNotFound);
}

/// <summary>
/// Personal to-do items owned by exactly one Agent (Story 16) — never linked to a ticket or customer.
/// Every method scopes its query to <c>OwnerUserId == currentUserId</c>; a task belonging to another
/// user is indistinguishable from a nonexistent one (null/false/NotFound), which the controller turns
/// into 404 either way. This is what stops one Agent from reading, editing, completing, or deleting
/// another Agent's tasks even if they guess a valid task id — holding the same <c>agenttasks.*</c>
/// permission never widens the scope to someone else's rows.
/// </summary>
public interface IAgentTasksService
{
    /// <summary>
    /// <paramref name="includeCompleted"/> = <c>false</c> excludes <see cref="AgentTaskState.Completed"/>
    /// rows; <c>null</c>/<c>true</c> includes everything. Never returns null — an owner with no tasks
    /// gets an empty list. <paramref name="ticketId"/>, when set, scopes to tasks linked to that ticket
    /// — this is what powers the ticket detail page's "Tasks" section, still filtered to the caller's
    /// own tasks (a task's owner-privacy is not relaxed just because it is also ticket-linked).
    /// </summary>
    Task<IReadOnlyList<AgentTaskDto>> ListAsync(Guid currentUserId, bool? includeCompleted, AgentTaskState? state, Guid? ticketId = null, CancellationToken cancellationToken = default);

    Task<AgentTaskDto?> GetAsync(Guid currentUserId, Guid id, CancellationToken cancellationToken = default);

    Task<AgentTaskResult> CreateAsync(Guid currentUserId, CreateAgentTaskRequest request, CancellationToken cancellationToken = default);

    Task<AgentTaskResult> UpdateAsync(Guid currentUserId, Guid id, UpdateAgentTaskRequest request, CancellationToken cancellationToken = default);

    /// <summary>Sets or clears <c>CompletedAt</c>. Idempotent: completing an already-completed task, or reopening one that was never completed, is a no-op — not an error, and does not disturb the existing <c>CompletedAt</c>/<c>UpdatedAt</c>.</summary>
    Task<AgentTaskDto?> CompleteAsync(Guid currentUserId, Guid id, bool completed, CancellationToken cancellationToken = default);

    Task<bool> DeleteAsync(Guid currentUserId, Guid id, CancellationToken cancellationToken = default);
}
