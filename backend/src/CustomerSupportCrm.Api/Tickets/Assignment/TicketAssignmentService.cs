using CustomerSupportCrm.Api.Tickets.History;
using CustomerSupportCrm.Domain.Tickets;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CustomerSupportCrm.Api.Tickets.Assignment;

public sealed class TicketAssignmentService(CrmDbContext db, ITicketHistoryService history, ILogger<TicketAssignmentService> logger) : ITicketAssignmentService
{
    public async Task<TicketAssignmentResult> TryAutoAssignAsync(Ticket ticket, CancellationToken cancellationToken = default)
    {
        if (ticket.AssignedUserId is not null)
        {
            return new TicketAssignmentResult(false, null, "already_assigned");
        }

        var category = await db.TicketCategories
            .AsNoTracking()
            .Where(c => c.Id == ticket.CategoryId)
            .Select(c => new { c.DepartmentId, c.NormalizedName })
            .SingleOrDefaultAsync(cancellationToken);

        // Same "General Inquiry" identity check Communications.ChannelTicketDefaults.ResolveAsync uses
        // to pick the default category for a channel-created ticket — deliberately kept in sync by
        // name rather than a shared IsDefault flag, so this guard and that one can never disagree.
        if (category is null || category.DepartmentId is null || category.NormalizedName == "GENERAL INQUIRY")
        {
            logger.LogDebug("Ticket {TicketId} not auto-assigned: category {CategoryId} has no department or is the default.", ticket.Id, ticket.CategoryId);
            return new TicketAssignmentResult(false, null, "default_or_no_department");
        }

        var departmentId = category.DepartmentId.Value;

        var eligible = await db.Users
            .AsNoTracking()
            .Where(u => u.DepartmentId == departmentId && u.IsActive)
            .Select(u => new { u.Id, u.MaxActiveTickets })
            .ToListAsync(cancellationToken);

        if (eligible.Count == 0)
        {
            logger.LogDebug("Ticket {TicketId} not auto-assigned: no active agent in department {DepartmentId}.", ticket.Id, departmentId);
            return new TicketAssignmentResult(false, null, "no_eligible_agent");
        }

        // A separate aggregate query rather than a correlated Count() inside the projection above —
        // simpler to reason about and portable across the SQL Server and EF InMemory (test) providers.
        // "Active" here means not yet Resolved or Closed — a ticket that's done no longer counts
        // toward an agent's current workload, even though it's still technically "assigned".
        var candidateIds = eligible.Select(u => u.Id).ToArray();
        var activeCounts = await db.Tickets
            .AsNoTracking()
            .Where(t => t.AssignedUserId != null && candidateIds.Contains(t.AssignedUserId.Value)
                     && t.Status != TicketStatuses.Resolved && t.Status != TicketStatuses.Closed)
            .GroupBy(t => t.AssignedUserId!.Value)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.UserId, g => g.Count, cancellationToken);

        var candidates = eligible
            .Select(u => new { u.Id, ActiveTickets = activeCounts.GetValueOrDefault(u.Id, 0), u.MaxActiveTickets })
            .Where(c => c.MaxActiveTickets is null || c.ActiveTickets < c.MaxActiveTickets)
            .ToList();

        if (candidates.Count == 0)
        {
            logger.LogDebug("Ticket {TicketId} not auto-assigned: every agent in department {DepartmentId} is at capacity.", ticket.Id, departmentId);
            return new TicketAssignmentResult(false, null, "no_eligible_agent");
        }

        var lowestWorkload = candidates.Min(c => c.ActiveTickets);
        var tier = candidates.Where(c => c.ActiveTickets == lowestWorkload).Select(c => c.Id).OrderBy(id => id).ToList();

        var cursor = await db.AssignmentRoundRobinCursors.SingleOrDefaultAsync(c => c.DepartmentId == departmentId, cancellationToken);
        var lastAssignedId = cursor?.LastAssignedUserId ?? Guid.Empty;

        // Cyclic pick within the lowest-workload tier: the first member sorting after the
        // last-assigned id, wrapping to the tier's first member if the last winner was the tier's last
        // (or isn't in this tier at all — a different tier won last time, or this department has never
        // auto-assigned before, in which case Guid.Empty sorts before everything).
        var chosenId = tier.FirstOrDefault(id => id.CompareTo(lastAssignedId) > 0);
        if (chosenId == Guid.Empty)
        {
            chosenId = tier[0];
        }

        ticket.AssignedUserId = chosenId;

        if (cursor is null)
        {
            db.AssignmentRoundRobinCursors.Add(new AssignmentRoundRobinCursor { DepartmentId = departmentId, LastAssignedUserId = chosenId });
        }
        else
        {
            cursor.LastAssignedUserId = chosenId;
            cursor.UpdatedAt = DateTimeOffset.UtcNow;
        }

        // performedByUserId: null — this is a system action, distinct from the admin's own
        // "CategoryChanged" history row recorded by the caller for the same edit.
        history.Record(ticket.Id, "Assigned", "Ticket auto-assigned", field: "AssignedUserId",
            previousValue: null, newValue: chosenId.ToString(), performedByUserId: null);

        logger.LogInformation("Ticket {TicketId} auto-assigned to user {UserId} in department {DepartmentId}.", ticket.Id, chosenId, departmentId);

        return new TicketAssignmentResult(true, chosenId, "assigned");
    }
}
