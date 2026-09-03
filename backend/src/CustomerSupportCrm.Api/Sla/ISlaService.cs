using CustomerSupportCrm.Domain.Sla;

namespace CustomerSupportCrm.Api.Sla;

/// <summary>
/// Story 22: First Response and Resolution SLA tracking, started once per ticket and never reset by
/// assignment or category changes — see <see cref="SlaService"/> for the algorithm.
/// </summary>
public interface ISlaService
{
    /// <summary>
    /// Starts the SLA clock for a just-created ticket using its own <c>CreatedAt</c> as the start time.
    /// Idempotent (a second call for the same ticket is a no-op) and never throws: a ticket with no
    /// applicable active <see cref="SlaPolicy"/> logs a warning and is left with no <see cref="TicketSla"/>
    /// row rather than failing ticket creation.
    /// </summary>
    Task StartForTicketAsync(Guid ticketId, CancellationToken cancellationToken = default);

    /// <summary>Records the first outbound agent/support message on a ticket. No-ops if First Response is no longer <see cref="SlaStatuses.Running"/> (already Met or Breached) or if the ticket has no SLA row at all.</summary>
    Task MarkFirstResponseAsync(Guid ticketId, DateTimeOffset respondedAt, CancellationToken cancellationToken = default);

    /// <summary>Called on every ticket status change; no-ops unless <paramref name="newStatus"/> is Resolved/Closed and Resolution is still <see cref="SlaStatuses.Running"/>.</summary>
    Task MarkResolvedIfApplicableAsync(Guid ticketId, string newStatus, DateTimeOffset changedAt, CancellationToken cancellationToken = default);

    /// <summary>Null if the ticket has no SLA row (pre-dates this story, or its policy was missing at creation). Lazily persists a Running → Breached transition it detects before returning — see <see cref="EvaluateBreaches"/>.</summary>
    Task<TicketSlaSnapshot?> GetForTicketAsync(Guid ticketId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Pure — no database access, no writes. A still-<see cref="SlaStatuses.Running"/> clock whose due
    /// time has passed evaluates as <see cref="SlaStatuses.Breached"/> in the returned snapshot even
    /// though <paramref name="sla"/> itself is untouched; callers that want that breach persisted call
    /// <see cref="GetForTicketAsync"/> instead of this directly.
    /// </summary>
    TicketSlaSnapshot EvaluateBreaches(TicketSla sla, DateTimeOffset now);
}
