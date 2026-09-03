using CustomerSupportCrm.Domain.Sla;
using CustomerSupportCrm.Domain.Tickets;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CustomerSupportCrm.Api.Sla;

/// <summary>
/// Algorithm (see <c>ISlaService</c> for the per-method contract):
/// 1. <see cref="StartForTicketAsync"/> resolves the applicable <see cref="SlaPolicy"/> — the active
///    policy for the ticket's own priority, falling back to the active default (<c>PriorityId</c> null)
///    — and snapshots it into a new <see cref="TicketSla"/> row using the ticket's own
///    <c>CreatedAt</c> as the clock start. Idempotent via a pre-check, and never throws: a missing
///    policy is logged and left with no SLA row rather than failing ticket creation (a deliberate
///    simplification of the story's original "throw, and have every caller remember to catch it").
/// 2. <see cref="MarkFirstResponseAsync"/>/<see cref="MarkResolvedIfApplicableAsync"/> each act only
///    while their own clock is still <see cref="SlaStatuses.Running"/> — once Met or Breached, further
///    calls are no-ops, so a duplicate or out-of-order call is always safe.
/// 3. <see cref="EvaluateBreaches"/> is pure (no I/O): a still-Running clock whose due time has passed
///    evaluates as Breached in the result without touching the row. <see cref="GetForTicketAsync"/> is
///    the one place that write-through persists a breach this way discovers, so a ticket nobody has
///    read since its due time passed still ends up with a correct, terminal, persisted status the next
///    time anyone does look at it.
/// </summary>
public sealed class SlaService(CrmDbContext db, ILogger<SlaService> logger) : ISlaService
{
    public async Task StartForTicketAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        if (await db.TicketSlas.AnyAsync(s => s.TicketId == ticketId, cancellationToken))
        {
            return;
        }

        var ticket = await db.Tickets
            .Where(t => t.Id == ticketId)
            .Select(t => new { t.CreatedAt, t.PriorityId })
            .SingleOrDefaultAsync(cancellationToken);
        if (ticket is null)
        {
            return;
        }

        var policy = await db.SlaPolicies
            .Where(p => p.IsActive && p.PriorityId == ticket.PriorityId)
            .FirstOrDefaultAsync(cancellationToken);
        policy ??= await db.SlaPolicies
            .Where(p => p.IsActive && p.PriorityId == null)
            .FirstOrDefaultAsync(cancellationToken);

        if (policy is null)
        {
            logger.LogWarning(
                "No applicable SLA policy for ticket {TicketId} (priority {PriorityId}) — no SLA row created.",
                ticketId, ticket.PriorityId);
            return;
        }

        db.TicketSlas.Add(new TicketSla
        {
            TicketId = ticketId,
            SlaPolicyId = policy.Id,
            StartedAt = ticket.CreatedAt,
            FirstResponseDueAt = ticket.CreatedAt.AddMinutes(policy.FirstResponseMinutes),
            ResolutionDueAt = ticket.CreatedAt.AddMinutes(policy.ResolutionMinutes),
        });

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkFirstResponseAsync(Guid ticketId, DateTimeOffset respondedAt, CancellationToken cancellationToken = default)
    {
        var sla = await db.TicketSlas.SingleOrDefaultAsync(s => s.TicketId == ticketId, cancellationToken);
        if (sla is null || sla.FirstResponseStatus != SlaStatuses.Running)
        {
            return;
        }

        // respondedAt earlier than the clock's own StartedAt (clock skew) is inherently <= the due
        // time, so it falls into the Met branch below without any separate check.
        var breached = respondedAt > sla.FirstResponseDueAt;
        sla.FirstResponseAt = respondedAt;
        sla.FirstResponseStatus = breached ? SlaStatuses.Breached : SlaStatuses.Met;
        sla.FirstResponseBreachedAt = breached ? respondedAt : null;
        sla.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkResolvedIfApplicableAsync(Guid ticketId, string newStatus, DateTimeOffset changedAt, CancellationToken cancellationToken = default)
    {
        if (newStatus != TicketStatuses.Resolved && newStatus != TicketStatuses.Closed)
        {
            // TODO Story 24/25: reopening a resolved ticket (status back to InProgress) never lands
            // here either — ResolutionStatus stays whatever terminal value it already had. Whether a
            // reopen should restart the Resolution clock is a product decision for a later story, not
            // assumed here.
            return;
        }

        var sla = await db.TicketSlas.SingleOrDefaultAsync(s => s.TicketId == ticketId, cancellationToken);
        if (sla is null || sla.ResolutionStatus != SlaStatuses.Running)
        {
            return;
        }

        var breached = changedAt > sla.ResolutionDueAt;
        sla.ResolvedAt = changedAt;
        sla.ResolutionStatus = breached ? SlaStatuses.Breached : SlaStatuses.Met;
        sla.ResolutionBreachedAt = breached ? changedAt : null;
        sla.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<TicketSlaSnapshot?> GetForTicketAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        var sla = await db.TicketSlas.SingleOrDefaultAsync(s => s.TicketId == ticketId, cancellationToken);
        if (sla is null)
        {
            return null;
        }

        var now = DateTimeOffset.UtcNow;
        var evaluated = EvaluateBreaches(sla, now);

        var changed = false;
        if (sla.FirstResponseStatus == SlaStatuses.Running && evaluated.FirstResponseStatus == SlaStatuses.Breached)
        {
            sla.FirstResponseStatus = SlaStatuses.Breached;
            sla.FirstResponseBreachedAt = now;
            changed = true;
        }
        if (sla.ResolutionStatus == SlaStatuses.Running && evaluated.ResolutionStatus == SlaStatuses.Breached)
        {
            sla.ResolutionStatus = SlaStatuses.Breached;
            sla.ResolutionBreachedAt = now;
            changed = true;
        }

        if (changed)
        {
            sla.UpdatedAt = now;
            await db.SaveChangesAsync(cancellationToken);
        }

        return evaluated;
    }

    public TicketSlaSnapshot EvaluateBreaches(TicketSla sla, DateTimeOffset now)
    {
        var firstResponseStatus = sla.FirstResponseStatus == SlaStatuses.Running && now >= sla.FirstResponseDueAt
            ? SlaStatuses.Breached
            : sla.FirstResponseStatus;
        var resolutionStatus = sla.ResolutionStatus == SlaStatuses.Running && now >= sla.ResolutionDueAt
            ? SlaStatuses.Breached
            : sla.ResolutionStatus;

        return new TicketSlaSnapshot(
            sla.TicketId, sla.StartedAt, sla.FirstResponseDueAt, sla.ResolutionDueAt,
            firstResponseStatus, resolutionStatus, sla.FirstResponseAt, sla.ResolvedAt);
    }
}
