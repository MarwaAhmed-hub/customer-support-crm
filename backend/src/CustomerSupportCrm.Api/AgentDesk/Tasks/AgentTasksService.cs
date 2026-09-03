using CustomerSupportCrm.Domain.AgentDesk;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupportCrm.Api.AgentDesk.Tasks;

public sealed class AgentTasksService(CrmDbContext db) : IAgentTasksService
{
    public async Task<IReadOnlyList<AgentTaskDto>> ListAsync(Guid currentUserId, bool? includeCompleted, AgentTaskState? state, Guid? ticketId = null, CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var query = db.AgentTasks
            .AsNoTracking()
            .Include(t => t.Ticket)
            .Where(t => t.OwnerUserId == currentUserId);

        if (ticketId.HasValue)
        {
            query = query.Where(t => t.TicketId == ticketId.Value);
        }

        var tasks = await query
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync(cancellationToken);

        // State is computed, not stored (it depends on the current clock), so the completed/state
        // filters apply in memory, after mapping — not translatable to SQL the way OwnerUserId/TicketId are.
        IEnumerable<AgentTaskDto> dtos = tasks.Select(t => ToDto(t, now, t.Ticket?.Subject));

        if (includeCompleted == false)
        {
            dtos = dtos.Where(d => d.State != AgentTaskState.Completed);
        }

        if (state.HasValue)
        {
            dtos = dtos.Where(d => d.State == state.Value);
        }

        return dtos.ToList();
    }

    public async Task<AgentTaskDto?> GetAsync(Guid currentUserId, Guid id, CancellationToken cancellationToken = default)
    {
        var task = await db.AgentTasks
            .AsNoTracking()
            .Include(t => t.Ticket)
            .SingleOrDefaultAsync(t => t.Id == id && t.OwnerUserId == currentUserId, cancellationToken);

        return task is null ? null : ToDto(task, DateTime.UtcNow, task.Ticket?.Subject);
    }

    public async Task<AgentTaskResult> CreateAsync(Guid currentUserId, CreateAgentTaskRequest request, CancellationToken cancellationToken = default)
    {
        var title = request.Title.Trim();
        if (title.Length == 0)
        {
            return AgentTaskResult.InvalidTitle;
        }

        // A single query both validates TicketId (SingleOrDefaultAsync returning null means "no such
        // ticket") and captures the subject to denormalize onto the DTO — the freshly-added entity
        // below has no tracked Ticket navigation to read it back from until a later fetch.
        var ticketSubject = await ResolveTicketSubjectAsync(request.TicketId, cancellationToken);
        if (request.TicketId.HasValue && ticketSubject is null)
        {
            return AgentTaskResult.TicketNotFound;
        }

        var description = NormalizeDescription(request.Description);

        var now = DateTime.UtcNow;
        var task = new AgentTask
        {
            OwnerUserId = currentUserId,
            Title = title,
            Description = description,
            ReminderAt = request.ReminderAt,
            TicketId = request.TicketId,
            CreatedAt = now,
            UpdatedAt = now,
        };

        db.AgentTasks.Add(task);
        await db.SaveChangesAsync(cancellationToken);

        return AgentTaskResult.Success(ToDto(task, now, ticketSubject));
    }

    public async Task<AgentTaskResult> UpdateAsync(Guid currentUserId, Guid id, UpdateAgentTaskRequest request, CancellationToken cancellationToken = default)
    {
        var task = await db.AgentTasks.SingleOrDefaultAsync(t => t.Id == id && t.OwnerUserId == currentUserId, cancellationToken);
        if (task is null)
        {
            return AgentTaskResult.NotFound;
        }

        var title = request.Title.Trim();
        if (title.Length == 0)
        {
            return AgentTaskResult.InvalidTitle;
        }

        var ticketSubject = await ResolveTicketSubjectAsync(request.TicketId, cancellationToken);
        if (request.TicketId.HasValue && ticketSubject is null)
        {
            return AgentTaskResult.TicketNotFound;
        }

        // Deliberately does not touch CreatedAt, OwnerUserId, or CompletedAt — editing a task's
        // details (including re-linking or unlinking its ticket) is a separate action from
        // completing/reopening it (see CompleteAsync).
        task.Title = title;
        task.Description = NormalizeDescription(request.Description);
        task.ReminderAt = request.ReminderAt;
        task.TicketId = request.TicketId;
        task.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return AgentTaskResult.Success(ToDto(task, DateTime.UtcNow, ticketSubject));
    }

    public async Task<AgentTaskDto?> CompleteAsync(Guid currentUserId, Guid id, bool completed, CancellationToken cancellationToken = default)
    {
        var task = await db.AgentTasks.Include(t => t.Ticket).SingleOrDefaultAsync(t => t.Id == id && t.OwnerUserId == currentUserId, cancellationToken);
        if (task is null)
        {
            return null;
        }

        // Idempotent both ways: completing an already-completed task leaves its original CompletedAt
        // untouched, and reopening a task that was never completed is a no-op — neither is an error,
        // and neither stamps UpdatedAt when nothing actually changed.
        if (completed && task.CompletedAt is null)
        {
            task.CompletedAt = DateTime.UtcNow;
            task.UpdatedAt = task.CompletedAt.Value;
            await db.SaveChangesAsync(cancellationToken);
        }
        else if (!completed && task.CompletedAt is not null)
        {
            task.CompletedAt = null;
            task.UpdatedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }

        return ToDto(task, DateTime.UtcNow, task.Ticket?.Subject);
    }

    public async Task<bool> DeleteAsync(Guid currentUserId, Guid id, CancellationToken cancellationToken = default)
    {
        var task = await db.AgentTasks.SingleOrDefaultAsync(t => t.Id == id && t.OwnerUserId == currentUserId, cancellationToken);
        if (task is null)
        {
            return false;
        }

        db.AgentTasks.Remove(task);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static string? NormalizeDescription(string? description)
    {
        var trimmed = description?.Trim();
        return trimmed is null or { Length: 0 } ? null : trimmed;
    }

    /// <summary>Null <paramref name="ticketId"/> resolves to null (no ticket to look up). A non-null <paramref name="ticketId"/> that matches no row also resolves to null — the caller distinguishes the two by checking <paramref name="ticketId"/>.HasValue itself, since both cases return the same null subject.</summary>
    private async Task<string?> ResolveTicketSubjectAsync(Guid? ticketId, CancellationToken cancellationToken) =>
        ticketId.HasValue
            ? await db.Tickets.Where(t => t.Id == ticketId.Value).Select(t => t.Subject).SingleOrDefaultAsync(cancellationToken)
            : null;

    private static AgentTaskDto ToDto(AgentTask task, DateTime nowUtc, string? ticketSubject) => new(
        task.Id, task.Title, task.Description, task.ReminderAt, task.CompletedAt,
        ComputeState(task, nowUtc), task.TicketId, ticketSubject, task.CreatedAt, task.UpdatedAt);

    private static AgentTaskState ComputeState(AgentTask task, DateTime nowUtc)
    {
        if (task.CompletedAt is not null)
        {
            return AgentTaskState.Completed;
        }

        if (task.ReminderAt is null)
        {
            return AgentTaskState.Pending;
        }

        return task.ReminderAt < nowUtc ? AgentTaskState.Overdue : AgentTaskState.Upcoming;
    }
}
