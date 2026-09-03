using CustomerSupportCrm.Domain.Sla;
using CustomerSupportCrm.Domain.Tickets;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CustomerSupportCrm.Api.Sla.Escalations;

/// <summary>
/// Routing rules (Story 24):
/// <list type="bullet">
/// <item>Warning + assigned → the assigned Agent.</item>
/// <item>Warning + unassigned → the responsible Administrator (Unassigned Tickets Queue).</item>
/// <item>Breach + assigned → the Manager resolved from the agent's own Department, falling back to
/// the agent's Branch, falling back to Administrator (with <see cref="TicketEscalation.Notes"/>
/// explaining the fallback) if no Manager can be found either way.</item>
/// <item>Breach + unassigned → the responsible Administrator, same as Warning.</item>
/// <item>Customer is never a target.</item>
/// </list>
/// Routing is decided at the moment each milestone is evaluated, not once for the ticket's lifetime —
/// an assignment change between Warning and Breach means the two can legitimately go to different
/// people (see the Warning/Breach targets being resolved independently below).
/// </summary>
public sealed class SlaEscalationService(CrmDbContext db, ILogger<SlaEscalationService> logger) : ISlaEscalationService
{
    private const string ManagerRoleName = "MANAGER";
    private const string AdministratorRoleName = "ADMINISTRATOR";

    public async Task<IReadOnlyList<TicketEscalationDto>> EvaluateAsync(Guid ticketId, DateTimeOffset? now = null, CancellationToken cancellationToken = default)
    {
        var ticket = await db.Tickets.AsNoTracking().SingleOrDefaultAsync(t => t.Id == ticketId, cancellationToken);
        if (ticket is null)
        {
            return [];
        }

        var sla = await db.TicketSlas.AsNoTracking().SingleOrDefaultAsync(s => s.TicketId == ticketId, cancellationToken);
        if (sla is null)
        {
            // Story 22 sets this at creation; a null row means a legacy ticket that pre-dates it —
            // nothing to evaluate against.
            return [];
        }

        var moment = now ?? DateTimeOffset.UtcNow;
        var created = new List<TicketEscalationDto>();

        // First Response — satisfied (and stops generating further First Response escalations) once
        // an actual reply happened, whether it was on time or not; independent of ticket status.
        if (sla.FirstResponseAt is null)
        {
            await TryCreateAsync(created, ticket, SlaType.FirstResponse, EscalationMilestone.Warning,
                WarningAt(sla.StartedAt, sla.FirstResponseDueAt), moment, cancellationToken);
            await TryCreateAsync(created, ticket, SlaType.FirstResponse, EscalationMilestone.Breach,
                sla.FirstResponseDueAt, moment, cancellationToken);
        }

        // Resolution — satisfied once the ticket reaches a resolved/closed status; independent of
        // whether First Response was ever satisfied (Acceptance 3/4: each SLA type is entirely
        // separate — a First Response breach must never flip Resolution's state, and vice versa).
        if (!TicketStatuses.IsResolvedOrClosed(ticket.Status))
        {
            await TryCreateAsync(created, ticket, SlaType.Resolution, EscalationMilestone.Warning,
                WarningAt(sla.StartedAt, sla.ResolutionDueAt), moment, cancellationToken);
            await TryCreateAsync(created, ticket, SlaType.Resolution, EscalationMilestone.Breach,
                sla.ResolutionDueAt, moment, cancellationToken);
        }

        return created;
    }

    public async Task<IReadOnlyList<TicketEscalationDto>> EvaluateAllOpenAsync(DateTimeOffset? now = null, CancellationToken cancellationToken = default)
    {
        // A resolved/closed ticket generates no further escalations of either type (see EvaluateAsync
        // above), so excluding it here is a correctness-neutral optimization, not a shortcut that
        // skips real work — the direct single-ticket EvaluateAsync above remains fully correct if
        // ever called on a resolved/closed ticket by hand (e.g. via the manual QA endpoint).
        var candidateIds = await db.Tickets
            .AsNoTracking()
            .Where(t => t.Status != TicketStatuses.Resolved && t.Status != TicketStatuses.Closed)
            .OrderBy(t => t.Id)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);

        var created = new List<TicketEscalationDto>();
        foreach (var ticketId in candidateIds)
        {
            try
            {
                created.AddRange(await EvaluateAsync(ticketId, now, cancellationToken));
            }
            catch (Exception ex)
            {
                // One ticket's evaluation failing (e.g. a transient DB error) must never stop the rest
                // of the sweep — the background service relies on this per-ticket isolation.
                logger.LogError(ex, "Failed to evaluate SLA escalations for ticket {TicketId}", ticketId);
            }
        }

        return created;
    }

    public async Task<IReadOnlyList<TicketEscalationDto>> ListForTicketAsync(Guid ticketId, CancellationToken cancellationToken = default) =>
        await db.TicketEscalations
            .AsNoTracking()
            .Where(e => e.TicketId == ticketId)
            .OrderBy(e => e.CreatedAtUtc)
            .Select(e => new TicketEscalationDto(
                e.Id, e.TicketId, e.SlaType, e.Milestone, e.TargetRole, e.TargetUserId,
                e.ThresholdAtUtc, e.CreatedAtUtc, e.WasUnassigned, e.Notes))
            .ToListAsync(cancellationToken);

    private static DateTimeOffset WarningAt(DateTimeOffset startedAt, DateTimeOffset dueAt) =>
        startedAt + TimeSpan.FromTicks((long)((dueAt - startedAt).Ticks * 0.8));

    /// <summary>
    /// No-ops silently if <paramref name="thresholdAt"/> hasn't been reached yet, or a row for this
    /// exact milestone already exists — the ordinary (non-concurrent) idempotency path; the unique
    /// index in <c>CrmDbContext</c> is the defense-in-depth backstop for a genuine race between two
    /// concurrent evaluator runs, which this existence check alone cannot fully prevent.
    /// </summary>
    private async Task TryCreateAsync(
        List<TicketEscalationDto> created, Ticket ticket, SlaType slaType, EscalationMilestone milestone,
        DateTimeOffset thresholdAt, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (now < thresholdAt)
        {
            return;
        }

        var exists = await db.TicketEscalations.AnyAsync(
            e => e.TicketId == ticket.Id && e.SlaType == slaType && e.Milestone == milestone, cancellationToken);
        if (exists)
        {
            return;
        }

        var (targetRole, targetUserId, notes) = await ResolveTargetAsync(ticket, milestone, cancellationToken);

        var escalation = new TicketEscalation
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            SlaType = slaType,
            Milestone = milestone,
            TargetRole = targetRole,
            TargetUserId = targetUserId,
            ThresholdAtUtc = thresholdAt.UtcDateTime,
            CreatedAtUtc = now.UtcDateTime,
            WasUnassigned = ticket.AssignedUserId is null,
            Notes = notes,
        };

        db.TicketEscalations.Add(escalation);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Lost a race against a concurrent evaluator run for this exact milestone — the unique
            // index did its job; treat it as already-created, not a failure.
            db.Entry(escalation).State = EntityState.Detached;
            return;
        }

        if (targetUserId is null)
        {
            logger.LogError(
                "Escalation for ticket {TicketId} ({SlaType}/{Milestone}) has no resolvable target user (role {TargetRole}).",
                ticket.Id, slaType, milestone, targetRole);
        }
        else if (notes is not null)
        {
            logger.LogWarning(
                "Escalation for ticket {TicketId} ({SlaType}/{Milestone}) fell back: {Notes}",
                ticket.Id, slaType, milestone, notes);
        }

        created.Add(new TicketEscalationDto(
            escalation.Id, escalation.TicketId, escalation.SlaType, escalation.Milestone, escalation.TargetRole,
            escalation.TargetUserId, escalation.ThresholdAtUtc, escalation.CreatedAtUtc, escalation.WasUnassigned, escalation.Notes));
    }

    private async Task<(EscalationTargetRole Role, Guid? UserId, string? Notes)> ResolveTargetAsync(
        Ticket ticket, EscalationMilestone milestone, CancellationToken cancellationToken)
    {
        if (ticket.AssignedUserId is null)
        {
            var administratorId = await ResolveAdministratorAsync(cancellationToken);
            return (EscalationTargetRole.Administrator, administratorId, administratorId is null ? "no active administrator found" : null);
        }

        if (milestone == EscalationMilestone.Warning)
        {
            return (EscalationTargetRole.Agent, ticket.AssignedUserId, null);
        }

        // Breach + assigned: Manager resolved via the AGENT's own Department, falling back to their
        // Branch — this is a different relationship than Story 23's "category's department", since a
        // ticket has no department of its own; only the agent working it does.
        var agent = await db.Users
            .AsNoTracking()
            .Where(u => u.Id == ticket.AssignedUserId.Value)
            .Select(u => new { u.DepartmentId, u.BranchId })
            .SingleOrDefaultAsync(cancellationToken);

        Guid? managerId = agent?.DepartmentId is { } departmentId
            ? await ResolveManagerForDepartmentAsync(departmentId, cancellationToken)
            : null;

        managerId ??= agent?.BranchId is { } branchId
            ? await ResolveManagerForBranchAsync(branchId, cancellationToken)
            : null;

        if (managerId is not null)
        {
            return (EscalationTargetRole.Manager, managerId, null);
        }

        var fallbackAdministratorId = await ResolveAdministratorAsync(cancellationToken);
        return (EscalationTargetRole.Administrator, fallbackAdministratorId, "no manager resolved; fell back to administrator");
    }

    /// <summary>Deterministic ordering (CreatedAt then Id) since nothing enforces exactly one Manager per Department — the earliest-created active Manager in it wins.</summary>
    private Task<Guid?> ResolveManagerForDepartmentAsync(Guid departmentId, CancellationToken cancellationToken) =>
        db.UserRoles
            .Where(ur => ur.Role.NormalizedName == ManagerRoleName)
            .Where(ur => ur.User.IsActive && ur.User.DepartmentId == departmentId)
            .OrderBy(ur => ur.User.CreatedAt).ThenBy(ur => ur.UserId)
            .Select(ur => (Guid?)ur.UserId)
            .FirstOrDefaultAsync(cancellationToken);

    /// <summary>Branch-level fallback when the agent's Department has no Manager — same deterministic ordering.</summary>
    private Task<Guid?> ResolveManagerForBranchAsync(Guid branchId, CancellationToken cancellationToken) =>
        db.UserRoles
            .Where(ur => ur.Role.NormalizedName == ManagerRoleName)
            .Where(ur => ur.User.IsActive && ur.User.BranchId == branchId)
            .OrderBy(ur => ur.User.CreatedAt).ThenBy(ur => ur.UserId)
            .Select(ur => (Guid?)ur.UserId)
            .FirstOrDefaultAsync(cancellationToken);

    private Task<Guid?> ResolveAdministratorAsync(CancellationToken cancellationToken) =>
        db.UserRoles
            .Where(ur => ur.Role.NormalizedName == AdministratorRoleName)
            .Where(ur => ur.User.IsActive)
            .OrderBy(ur => ur.User.CreatedAt).ThenBy(ur => ur.UserId)
            .Select(ur => (Guid?)ur.UserId)
            .FirstOrDefaultAsync(cancellationToken);

    // Same pattern as TicketCategoriesService.IsUniqueViolation: a synchronous check on the SQL error
    // number (2601/2627), not a second DB round-trip.
    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
}
