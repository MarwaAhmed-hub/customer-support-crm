using CustomerSupportCrm.Api.Users;

namespace CustomerSupportCrm.Api.Tickets.Tickets;

public enum TicketOperationOutcome
{
    Success,
    NotFound,
    CustomerNotFound,
    CategoryNotFound,
    PriorityNotFound,

    /// <summary>Subject is empty/whitespace-only after trimming — StringLength(MinimumLength = 1) alone lets a single space through.</summary>
    InvalidSubject,

    /// <summary>Description is empty/whitespace-only after trimming.</summary>
    InvalidDescription,

    /// <summary>Story 12: <c>AssignedUserId</c> doesn't match any user, or matches one with <c>IsActive == false</c> — same "invalid reference" 400, not 404, as <c>UsersController</c>'s Department/Branch validation.</summary>
    InvalidAssignedUser,

    /// <summary>
    /// The ticket's category is linked to a department (<c>TicketCategory.DepartmentId</c> is not
    /// null), and the requested <c>AssignedUserId</c> belongs to a different department (or none at
    /// all). A category with no department imposes no such restriction — any active user is eligible.
    /// </summary>
    AssignedUserOutsideDepartment,

    /// <summary>Story 13: the requested status is not one of <see cref="Domain.Tickets.TicketStatuses.All"/>.</summary>
    InvalidStatus,

    /// <summary>Story 13: the requested status is known, but not reachable from the ticket's current status — see <see cref="Domain.Tickets.TicketStatuses.CanTransition"/>.</summary>
    InvalidStatusTransition,

    /// <summary>Story 13: escalation <c>reason</c> is empty/whitespace-only after trimming.</summary>
    InvalidEscalationReason,

    /// <summary>Story 13: the ticket is already escalated.</summary>
    AlreadyEscalated,

    /// <summary>Story 13: the ticket is not currently escalated.</summary>
    NotEscalated,
}

public sealed record TicketResult(TicketOperationOutcome Outcome, TicketDetailDto? Ticket = null)
{
    public static TicketResult Success(TicketDetailDto ticket) => new(TicketOperationOutcome.Success, ticket);
    public static readonly TicketResult NotFound = new(TicketOperationOutcome.NotFound);
    public static readonly TicketResult CustomerNotFound = new(TicketOperationOutcome.CustomerNotFound);
    public static readonly TicketResult CategoryNotFound = new(TicketOperationOutcome.CategoryNotFound);
    public static readonly TicketResult PriorityNotFound = new(TicketOperationOutcome.PriorityNotFound);
    public static readonly TicketResult InvalidSubject = new(TicketOperationOutcome.InvalidSubject);
    public static readonly TicketResult InvalidDescription = new(TicketOperationOutcome.InvalidDescription);
    public static readonly TicketResult InvalidAssignedUser = new(TicketOperationOutcome.InvalidAssignedUser);
    public static readonly TicketResult AssignedUserOutsideDepartment = new(TicketOperationOutcome.AssignedUserOutsideDepartment);
    public static readonly TicketResult InvalidStatus = new(TicketOperationOutcome.InvalidStatus);
    public static readonly TicketResult InvalidStatusTransition = new(TicketOperationOutcome.InvalidStatusTransition);
    public static readonly TicketResult InvalidEscalationReason = new(TicketOperationOutcome.InvalidEscalationReason);
    public static readonly TicketResult AlreadyEscalated = new(TicketOperationOutcome.AlreadyEscalated);
    public static readonly TicketResult NotEscalated = new(TicketOperationOutcome.NotEscalated);
}

/// <summary>
/// Business rules for tickets: FK existence validation (Customer/Category/Priority), required-field
/// trimming, and the create-time <see cref="Customers.CustomerInteraction"/> side effect. Modeled on
/// <c>Customers.CustomersService</c>, with the create flow additionally mirroring
/// <c>Customers.Interactions</c>'s field population for the interaction row it writes.
/// </summary>
public interface ITicketsService
{
    /// <summary>
    /// Story 15: <paramref name="assignedUserId"/> powers both the generic list filter and the Agent
    /// Dashboard's <c>GET /api/tickets/mine</c>, which forces it to the caller's own id. Story 23:
    /// <paramref name="unassignedOnly"/> is the Unassigned Tickets Queue filter — independent of
    /// <paramref name="assignedUserId"/>, which filters TO a specific agent rather than to "no agent".
    /// </summary>
    Task<PagedResult<TicketListItemDto>> ListAsync(
        Guid? customerId, Guid? categoryId, Guid? priorityId, Guid? assignedUserId, string? status, bool? isEscalated, string? search,
        int page, int pageSize, bool? unassignedOnly = null, CancellationToken cancellationToken = default);

    Task<TicketDetailDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Atomically persists the new <see cref="Domain.Tickets.Ticket"/> together with one "Created"
    /// <see cref="Domain.Tickets.TicketHistory"/> row (Story 14) — both added to the same
    /// <c>DbContext</c> and saved with a single <c>SaveChangesAsync</c> call, which EF Core already
    /// wraps in its own transaction (see the remarks on <c>Program.cs</c>'s <c>EnableRetryOnFailure</c>
    /// call: a *manual* <c>BeginTransactionAsync</c> would need extra
    /// <c>CreateExecutionStrategy</c> handling that a single implicit-transaction SaveChanges doesn't).
    /// Audit logging happens after that save succeeds, as a separate, best-effort concern.
    /// </summary>
    /// <param name="sourceChannel">
    /// Story 19: null for every existing call site (manual/internal creation via the authenticated
    /// UI) — unchanged behaviour, including the one <see cref="Customers.CustomerInteraction"/>
    /// (Type = "ticket") this method has always written as part of the same save. When non-null
    /// ("Email" or "WebForm"), that generic interaction is skipped instead: the caller (an email/
    /// web-form ingestion service) writes its own richer interaction row (with subject/body/message-id)
    /// referencing this ticket right after this method returns, and writing both would violate "exactly
    /// one interaction per submission".
    /// </param>
    Task<TicketResult> CreateAsync(CreateTicketRequest request, Guid actorUserId, string? sourceChannel = null, CancellationToken cancellationToken = default);

    /// <summary>Does not touch CreatedAt/CreatedByUserId/Status and does not create another CustomerInteraction. Story 14: records a <c>TicketHistory</c> row per meaningfully changed field (Subject/Description/Category/Priority).</summary>
    Task<TicketResult> UpdateAsync(Guid id, UpdateTicketRequest request, Guid actorUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Story 12: assign, reassign, or unassign (<paramref name="assignedUserId"/> = null) a ticket.
    /// Touches only <c>AssignedUserId</c> — <c>CreatedAt</c>, <c>CreatedByUserId</c>, <c>Status</c>,
    /// <c>CategoryId</c>, <c>PriorityId</c>, and <c>CustomerId</c> are left untouched, and no
    /// <see cref="Customers.CustomerInteraction"/> is written (that is a create-only side effect from
    /// Story 11). Audit logging is the caller's responsibility, same as <see cref="CreateAsync"/>/
    /// <see cref="UpdateAsync"/> — see <c>TicketsController</c>. Story 14: records one <c>TicketHistory</c>
    /// row ("Assigned" or "Reassigned") when <paramref name="assignedUserId"/> actually changes.
    /// </summary>
    Task<TicketResult> UpdateAssignmentAsync(Guid id, Guid? assignedUserId, Guid actorUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Story 13: transitions <c>Status</c> per <see cref="Domain.Tickets.TicketStatuses.CanTransition"/>.
    /// Touches only <c>Status</c>/<c>UpdatedAt</c> — <c>CreatedAt</c>, <c>CreatedByUserId</c>,
    /// <c>AssignedUserId</c>, and every escalation field are left untouched, and no
    /// <see cref="Customers.CustomerInteraction"/> is written. Story 14: records one "StatusChanged"
    /// <c>TicketHistory</c> row.
    /// </summary>
    Task<TicketResult> UpdateStatusAsync(Guid id, string status, Guid actorUserId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Story 13: sets <c>IsEscalated</c>/<c>EscalatedAt</c>/<c>EscalatedByUserId</c>/<c>EscalationReason</c>.
    /// Does not touch <c>Status</c> or <c>AssignedUserId</c>, and does not write a
    /// <see cref="Customers.CustomerInteraction"/>.
    /// </summary>
    Task<TicketResult> EscalateAsync(Guid id, string reason, Guid actorUserId, CancellationToken cancellationToken = default);

    /// <summary>Story 13: clears every escalation field. Does not touch <c>Status</c> or <c>AssignedUserId</c>.</summary>
    Task<TicketResult> DeEscalateAsync(Guid id, CancellationToken cancellationToken = default);
}
