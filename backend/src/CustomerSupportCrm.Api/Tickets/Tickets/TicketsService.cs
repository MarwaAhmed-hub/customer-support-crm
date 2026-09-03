using CustomerSupportCrm.Api.Sla;
using CustomerSupportCrm.Api.Tickets.Assignment;
using CustomerSupportCrm.Api.Tickets.History;
using CustomerSupportCrm.Api.Users;
using CustomerSupportCrm.Domain.Customers;
using CustomerSupportCrm.Domain.Sla;
using CustomerSupportCrm.Domain.Tickets;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupportCrm.Api.Tickets.Tickets;

public sealed class TicketsService(CrmDbContext db, ITicketHistoryService history, ISlaService sla, ITicketAssignmentService assignment) : ITicketsService
{
    private const int HistoryValueMaxLength = 512;

    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public async Task<PagedResult<TicketListItemDto>> ListAsync(
        Guid? customerId, Guid? categoryId, Guid? priorityId, Guid? assignedUserId, string? status, bool? isEscalated, string? search,
        int page, int pageSize, bool? unassignedOnly = null, CancellationToken cancellationToken = default)
    {
        page = page < 1 ? 1 : page;
        pageSize = pageSize < 1 ? DefaultPageSize : Math.Min(pageSize, MaxPageSize);

        var query = db.Tickets.AsNoTracking().AsQueryable();

        if (customerId.HasValue)
        {
            query = query.Where(t => t.CustomerId == customerId.Value);
        }

        if (categoryId.HasValue)
        {
            query = query.Where(t => t.CategoryId == categoryId.Value);
        }

        if (priorityId.HasValue)
        {
            query = query.Where(t => t.PriorityId == priorityId.Value);
        }

        // Story 15: generic filter for anyone with tickets.view (e.g. a manager filtering by agent),
        // and — via the /mine endpoint, which pins this to the caller's own id — the actual privacy
        // boundary for the Agent Dashboard.
        if (assignedUserId.HasValue)
        {
            query = query.Where(t => t.AssignedUserId == assignedUserId.Value);
        }

        // Story 23: the Unassigned Tickets Queue filter — independent of assignedUserId above (which
        // filters TO a specific agent); this filters to tickets with no agent at all.
        if (unassignedOnly == true)
        {
            query = query.Where(t => t.AssignedUserId == null);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(t => t.Status == status);
        }

        // The "escalated queue" filter — the missing link that lets a Manager pull up every currently
        // escalated ticket in one view instead of hunting for the red badge one ticket at a time.
        if (isEscalated.HasValue)
        {
            query = query.Where(t => t.IsEscalated == isEscalated.Value);
        }

        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            query = query.Where(t => t.Subject.Contains(term));
        }

        var total = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(ToListItemExpression)
            .ToListAsync(cancellationToken);

        // Story 22: TicketSla lives in its own table (see CrmDbContext's remarks on ToListItemExpression
        // being a static, non-EF-translatable expression for anything beyond plain Ticket columns), so
        // it's batch-loaded here and stitched onto each row rather than joined in the query above.
        // Breaches are evaluated in memory only — never persisted from a list read, unlike GetAsync's
        // single-ticket write-through, so paging through a list of tickets can never trigger up to a
        // page's worth of writes.
        if (items.Count > 0)
        {
            var ticketIds = items.Select(i => i.Id).ToArray();
            var now = DateTimeOffset.UtcNow;
            var slaByTicketId = await db.TicketSlas
                .AsNoTracking()
                .Where(s => ticketIds.Contains(s.TicketId))
                .ToDictionaryAsync(s => s.TicketId, cancellationToken);

            items = items
                .Select(item => slaByTicketId.TryGetValue(item.Id, out var row) ? item with { Sla = ToSlaDto(sla.EvaluateBreaches(row, now)) } : item)
                .ToList();
        }

        return new PagedResult<TicketListItemDto>(items, page, pageSize, total);
    }

    public async Task<TicketDetailDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var dto = await db.Tickets
            .AsNoTracking()
            .Where(t => t.Id == id)
            .Select(ToDetailExpression)
            .SingleOrDefaultAsync(cancellationToken);
        if (dto is null)
        {
            return null;
        }

        // Story 22: GetForTicketAsync also write-through persists a Running -> Breached transition it
        // discovers, so a ticket nobody has looked at since its due time passed still self-heals to a
        // correct, terminal status the next time someone views it.
        var snapshot = await sla.GetForTicketAsync(id, cancellationToken);
        return snapshot is null ? dto : dto with { Sla = ToSlaDto(snapshot) };
    }

    private static TicketSlaDto ToSlaDto(TicketSlaSnapshot snapshot) =>
        new(snapshot.StartedAt, snapshot.FirstResponseDueAt, snapshot.ResolutionDueAt,
            snapshot.FirstResponseStatus, snapshot.ResolutionStatus, snapshot.FirstResponseAt, snapshot.ResolvedAt);

    public async Task<TicketResult> CreateAsync(CreateTicketRequest request, Guid actorUserId, string? sourceChannel = null, CancellationToken cancellationToken = default)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == request.CustomerId, cancellationToken))
        {
            return TicketResult.CustomerNotFound;
        }

        if (!await db.TicketCategories.AnyAsync(c => c.Id == request.CategoryId, cancellationToken))
        {
            return TicketResult.CategoryNotFound;
        }

        if (!await db.TicketPriorities.AnyAsync(p => p.Id == request.PriorityId, cancellationToken))
        {
            return TicketResult.PriorityNotFound;
        }

        var subject = request.Subject.Trim();
        if (subject.Length == 0)
        {
            return TicketResult.InvalidSubject;
        }

        var description = request.Description.Trim();
        if (description.Length == 0)
        {
            return TicketResult.InvalidDescription;
        }

        // One instant shared by the ticket and the interaction it produces — Ticket uses
        // DateTimeOffset (matching TicketCategory/TicketPriority); CustomerInteraction uses DateTime
        // (Story 08's own convention), so both are derived from the same UtcNow rather than each
        // calling it separately.
        var now = DateTimeOffset.UtcNow;

        var ticket = new Ticket
        {
            CustomerId = request.CustomerId,
            Subject = subject,
            Description = description,
            CategoryId = request.CategoryId,
            PriorityId = request.PriorityId,
            Status = TicketStatuses.Open,
            CreatedByUserId = actorUserId,
            CreatedAt = now,
            UpdatedAt = now,
            SourceChannel = sourceChannel,
        };

        // See PersistTicketAndInteractionAsync's remarks: this single SaveChangesAsync call is what
        // makes the ticket and its interaction atomic — no explicit BeginTransactionAsync needed.
        await PersistTicketAndInteractionAsync(ticket, actorUserId, now, sourceChannel, cancellationToken);

        // Story 22: starts the SLA clock from this same ticket's own CreatedAt, for every ticket
        // regardless of channel, category, or assignment. A separate call (not folded into the
        // SaveChangesAsync above) since it needs the ticket's row to already exist to query it back —
        // never throws, so a missing policy degrades to "no SLA row" rather than failing the create.
        await sla.StartForTicketAsync(ticket.Id, cancellationToken);

        var dto = await GetAsync(ticket.Id, cancellationToken);
        return TicketResult.Success(dto!);
    }

    /// <summary>
    /// Also where Story 14's "Created" <see cref="Domain.Tickets.TicketHistory"/> row is attached,
    /// alongside this same <see cref="Domain.Tickets.Ticket"/> insert and <see cref="CustomerInteraction"/>
    /// insert — without touching the interaction step itself. Do not split this into two SaveChanges
    /// calls; that would reopen the "ticket persisted but interaction/history fails" failure mode this
    /// method exists to close.
    /// </summary>
    private async Task PersistTicketAndInteractionAsync(Ticket ticket, Guid actorUserId, DateTimeOffset occurredAt, string? sourceChannel, CancellationToken cancellationToken)
    {
        db.Tickets.Add(ticket);

        // Story 19: a channel-originated ticket (sourceChannel != null) gets its own richer
        // CustomerInteraction (email_inbound/web_form) written by the caller right after CreateAsync
        // returns — skip the generic "ticket" interaction here so exactly one interaction is produced
        // per submission. Manual/internal creation (sourceChannel: null) is unchanged.
        if (sourceChannel is null)
        {
            db.CustomerInteractions.Add(new CustomerInteraction
            {
                CustomerId = ticket.CustomerId,
                TicketId = ticket.Id,
                OccurredAt = occurredAt.UtcDateTime,
                InteractionType = "ticket",
                Summary = $"Ticket created: {ticket.Subject}",
                Details = ticket.Description,
                UserId = actorUserId,
                CreatedAt = occurredAt.UtcDateTime,
            });
        }

        history.Record(ticket.Id, "Created", "Ticket created", performedByUserId: actorUserId);

        await db.SaveChangesAsync(cancellationToken);
    }

    // NOTE (Story 24): SLA timers are anchored to TicketSla.StartedAt (== Ticket.CreatedAt) and MUST
    // NOT be reset by assignment, reassignment, or category changes — this method (category/priority
    // edits) touches neither TicketSla nor calls ISlaService at all.
    public async Task<TicketResult> UpdateAsync(Guid id, UpdateTicketRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var ticket = await db.Tickets.SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (ticket is null)
        {
            return TicketResult.NotFound;
        }

        if (!await db.TicketCategories.AnyAsync(c => c.Id == request.CategoryId, cancellationToken))
        {
            return TicketResult.CategoryNotFound;
        }

        if (!await db.TicketPriorities.AnyAsync(p => p.Id == request.PriorityId, cancellationToken))
        {
            return TicketResult.PriorityNotFound;
        }

        var subject = request.Subject.Trim();
        if (subject.Length == 0)
        {
            return TicketResult.InvalidSubject;
        }

        var description = request.Description.Trim();
        if (description.Length == 0)
        {
            return TicketResult.InvalidDescription;
        }

        // Story 14: pre-image captured before any field is overwritten, so each meaningfully changed
        // field below gets its own TicketHistory row in the same SaveChangesAsync as the mutation.
        // Unchanged fields (the common case — most edits touch one field) record nothing, per the
        // story's "avoid recording when the value is unchanged" rule.
        var previousSubject = ticket.Subject;
        var previousDescription = ticket.Description;
        var previousCategoryId = ticket.CategoryId;
        var previousPriorityId = ticket.PriorityId;

        // Deliberately does not touch CustomerId, CreatedAt, CreatedByUserId, or Status, and does not
        // create another CustomerInteraction — this story's "ticket created" interaction is a
        // one-time side effect of Create, not of every edit.
        ticket.Subject = subject;
        ticket.Description = description;
        ticket.CategoryId = request.CategoryId;
        ticket.PriorityId = request.PriorityId;
        ticket.UpdatedAt = DateTimeOffset.UtcNow;

        if (previousSubject != subject)
        {
            history.Record(ticket.Id, "Updated", "Subject updated", field: "Subject",
                previousValue: Truncate(previousSubject), newValue: Truncate(subject), performedByUserId: actorUserId);
        }

        if (previousDescription != description)
        {
            history.Record(ticket.Id, "Updated", "Description updated", field: "Description",
                previousValue: Truncate(previousDescription), newValue: Truncate(description), performedByUserId: actorUserId);
        }

        var categoryChanged = previousCategoryId != request.CategoryId;
        if (categoryChanged)
        {
            var previousCategoryName = await db.TicketCategories.Where(c => c.Id == previousCategoryId).Select(c => c.Name).SingleOrDefaultAsync(cancellationToken);
            var newCategoryName = await db.TicketCategories.Where(c => c.Id == request.CategoryId).Select(c => c.Name).SingleOrDefaultAsync(cancellationToken);
            history.Record(ticket.Id, "CategoryChanged", $"Category changed to {newCategoryName}", field: "CategoryId",
                previousValue: previousCategoryName, newValue: newCategoryName, performedByUserId: actorUserId);
        }

        if (previousPriorityId != request.PriorityId)
        {
            var previousPriorityName = await db.TicketPriorities.Where(p => p.Id == previousPriorityId).Select(p => p.Name).SingleOrDefaultAsync(cancellationToken);
            var newPriorityName = await db.TicketPriorities.Where(p => p.Id == request.PriorityId).Select(p => p.Name).SingleOrDefaultAsync(cancellationToken);
            history.Record(ticket.Id, "PriorityChanged", $"Priority changed to {newPriorityName}", field: "PriorityId",
                previousValue: previousPriorityName, newValue: newPriorityName, performedByUserId: actorUserId);
        }

        // Story 23: only a real category change on a still-unassigned ticket is eligible — re-saving
        // the same category, or editing any other field on an already-assigned ticket, never triggers
        // this. TryAutoAssignAsync stages its own history/cursor rows on this same db instance without
        // saving, so the single SaveChangesAsync below commits the edit and the auto-assignment (if
        // any) atomically together.
        if (categoryChanged && ticket.AssignedUserId is null)
        {
            await assignment.TryAutoAssignAsync(ticket, cancellationToken);
        }

        await db.SaveChangesAsync(cancellationToken);

        var dto = await GetAsync(ticket.Id, cancellationToken);
        return TicketResult.Success(dto!);
    }

    // NOTE (Story 24): SLA timers are anchored to TicketSla.StartedAt (== Ticket.CreatedAt) and MUST
    // NOT be reset by assignment, reassignment, or category changes — this method (assign/reassign/
    // unassign) touches neither TicketSla nor calls ISlaService at all.
    public async Task<TicketResult> UpdateAssignmentAsync(Guid id, Guid? assignedUserId, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var ticket = await db.Tickets.SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (ticket is null)
        {
            return TicketResult.NotFound;
        }

        if (assignedUserId.HasValue)
        {
            // Same "invalid reference" 400 UsersController uses for an unknown or inactive
            // Department/Branch — not a 404, since AssignedUserId is an optional reference, not a
            // primary resource lookup like the ticket id above.
            var candidate = await db.Users
                .Where(u => u.Id == assignedUserId.Value && u.IsActive)
                .Select(u => new { u.DepartmentId })
                .SingleOrDefaultAsync(cancellationToken);
            if (candidate is null)
            {
                return TicketResult.InvalidAssignedUser;
            }

            // Enforced here, not just filtered client-side, so a caller hitting this endpoint directly
            // cannot assign across departments either. A category with no department imposes no
            // restriction — see the remarks on TicketOperationOutcome.AssignedUserOutsideDepartment.
            var categoryDepartmentId = await db.TicketCategories
                .Where(c => c.Id == ticket.CategoryId)
                .Select(c => c.DepartmentId)
                .SingleOrDefaultAsync(cancellationToken);
            if (categoryDepartmentId is not null && candidate.DepartmentId != categoryDepartmentId)
            {
                return TicketResult.AssignedUserOutsideDepartment;
            }
        }

        var previousAssignedUserId = ticket.AssignedUserId;

        // Touches only AssignedUserId — CreatedAt, CreatedByUserId, Status, CategoryId, PriorityId,
        // and CustomerId are left exactly as they were, and no CustomerInteraction is written (that
        // is a create-only side effect from Story 11, not repeated here).
        ticket.AssignedUserId = assignedUserId;
        ticket.UpdatedAt = DateTimeOffset.UtcNow;

        if (previousAssignedUserId != assignedUserId)
        {
            var previousName = previousAssignedUserId.HasValue
                ? await db.Users.Where(u => u.Id == previousAssignedUserId.Value).Select(u => u.DisplayName).SingleOrDefaultAsync(cancellationToken)
                : null;
            var newName = assignedUserId.HasValue
                ? await db.Users.Where(u => u.Id == assignedUserId.Value).Select(u => u.DisplayName).SingleOrDefaultAsync(cancellationToken)
                : null;

            // Story 14: a real user-to-user handoff is "Reassigned"; both the first assignment
            // (null -> user) and an unassignment (user -> null) are "Assigned" — there is no separate
            // "Unassigned" event type, the cleared state is carried by a null NewValue instead.
            var isHandoff = previousAssignedUserId.HasValue && assignedUserId.HasValue;
            var eventType = isHandoff ? "Reassigned" : "Assigned";
            var summary = newName is not null
                ? $"Ticket {(isHandoff ? "reassigned" : "assigned")} to {newName}"
                : "Ticket unassigned";

            history.Record(ticket.Id, eventType, summary, field: "AssignedUserId",
                previousValue: previousName, newValue: newName, performedByUserId: actorUserId);
        }

        await db.SaveChangesAsync(cancellationToken);

        var dto = await GetAsync(ticket.Id, cancellationToken);
        return TicketResult.Success(dto!);
    }

    public async Task<TicketResult> UpdateStatusAsync(Guid id, string status, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var ticket = await db.Tickets.SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (ticket is null)
        {
            return TicketResult.NotFound;
        }

        if (!TicketStatuses.IsKnown(status))
        {
            return TicketResult.InvalidStatus;
        }

        if (!TicketStatuses.CanTransition(ticket.Status, status))
        {
            return TicketResult.InvalidStatusTransition;
        }

        var previousStatus = ticket.Status;

        // Touches only Status/UpdatedAt — CreatedAt, CreatedByUserId, AssignedUserId, category/
        // priority, and every escalation field are left exactly as they were, and no
        // CustomerInteraction is written (same boundary UpdateAssignmentAsync already respects).
        ticket.Status = status;
        var changedAt = DateTimeOffset.UtcNow;
        ticket.UpdatedAt = changedAt;

        // CanTransition already guarantees previousStatus != status (a same-status "transition" is
        // rejected above), so this always records — no guard needed, unlike the Update/Assignment paths.
        history.Record(ticket.Id, "StatusChanged", $"Status changed to {StatusLabel(status)}", field: "Status",
            previousValue: StatusLabel(previousStatus), newValue: StatusLabel(status), performedByUserId: actorUserId);

        await db.SaveChangesAsync(cancellationToken);

        // Story 22: a no-op for every transition except into Resolved/Closed, and a no-op again if
        // Resolution is no longer Running (e.g. it already lazily breached on an earlier read).
        await sla.MarkResolvedIfApplicableAsync(ticket.Id, status, changedAt, cancellationToken);

        var dto = await GetAsync(ticket.Id, cancellationToken);
        return TicketResult.Success(dto!);
    }

    public async Task<TicketResult> EscalateAsync(Guid id, string reason, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var ticket = await db.Tickets.SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (ticket is null)
        {
            return TicketResult.NotFound;
        }

        var trimmedReason = reason.Trim();
        if (trimmedReason.Length == 0)
        {
            return TicketResult.InvalidEscalationReason;
        }

        if (ticket.IsEscalated)
        {
            return TicketResult.AlreadyEscalated;
        }

        // Does not touch Status or AssignedUserId, and does not write a CustomerInteraction.
        var now = DateTimeOffset.UtcNow;
        ticket.IsEscalated = true;
        ticket.EscalatedAt = now;
        ticket.EscalatedByUserId = actorUserId;
        ticket.EscalationReason = trimmedReason;
        ticket.UpdatedAt = now;

        await db.SaveChangesAsync(cancellationToken);

        var dto = await GetAsync(ticket.Id, cancellationToken);
        return TicketResult.Success(dto!);
    }

    public async Task<TicketResult> DeEscalateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var ticket = await db.Tickets.SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (ticket is null)
        {
            return TicketResult.NotFound;
        }

        if (!ticket.IsEscalated)
        {
            return TicketResult.NotEscalated;
        }

        ticket.IsEscalated = false;
        ticket.EscalatedAt = null;
        ticket.EscalatedByUserId = null;
        ticket.EscalationReason = null;
        ticket.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        var dto = await GetAsync(ticket.Id, cancellationToken);
        return TicketResult.Success(dto!);
    }

    /// <summary>Story 14: <c>PreviousValue</c>/<c>NewValue</c> are capped at <see cref="HistoryValueMaxLength"/> by the EF column, so free-text fields (Subject/Description) are truncated before storing, not left to fail at save time.</summary>
    private static string Truncate(string value) => value.Length <= HistoryValueMaxLength ? value : value[..HistoryValueMaxLength];

    private static string StatusLabel(string status) => status switch
    {
        TicketStatuses.Open => "Open",
        TicketStatuses.InProgress => "In Progress",
        TicketStatuses.Pending => "Pending",
        TicketStatuses.Resolved => "Resolved",
        TicketStatuses.Closed => "Closed",
        _ => status,
    };

    private static readonly System.Linq.Expressions.Expression<Func<Ticket, TicketListItemDto>> ToListItemExpression =
        t => new TicketListItemDto(
            t.Id, t.CustomerId, t.Customer!.FirstName + " " + t.Customer.LastName,
            t.Subject, t.CategoryId, t.Category!.Name,
            t.PriorityId, t.Priority!.Name, t.Status,
            t.CreatedByUserId, t.CreatedByUser != null ? t.CreatedByUser.DisplayName : null,
            t.AssignedUserId, t.AssignedUser != null ? t.AssignedUser.DisplayName : null,
            t.IsEscalated,
            t.CreatedAt, t.UpdatedAt, t.SourceChannel);

    private static readonly System.Linq.Expressions.Expression<Func<Ticket, TicketDetailDto>> ToDetailExpression =
        t => new TicketDetailDto(
            t.Id, t.CustomerId, t.Customer!.FirstName + " " + t.Customer.LastName,
            t.Subject, t.Description,
            t.CategoryId, t.Category!.Name,
            t.PriorityId, t.Priority!.Name,
            t.Status, t.CreatedByUserId, t.CreatedByUser != null ? t.CreatedByUser.DisplayName : null,
            t.AssignedUserId, t.AssignedUser != null ? t.AssignedUser.DisplayName : null,
            t.IsEscalated, t.EscalatedAt, t.EscalatedByUserId,
            t.EscalatedByUser != null ? t.EscalatedByUser.DisplayName : null,
            t.EscalationReason,
            t.CreatedAt, t.UpdatedAt, t.SourceChannel,
            null, t.Category!.DepartmentId);
}
