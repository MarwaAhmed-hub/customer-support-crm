namespace CustomerSupportCrm.Api.Sla.Escalations;

/// <summary>
/// Story 24: detects First Response / Resolution SLA Warning (80% elapsed) and Breach (100% elapsed)
/// milestones and records a single, idempotent <c>TicketEscalation</c> row per
/// <c>(TicketId, SlaType, Milestone)</c>, routed to the Agent/Manager/Administrator (never the
/// Customer) per the rules documented on <see cref="SlaEscalationService"/>. Reads Story 22's
/// <c>TicketSla</c> due-at timestamps; never writes to them — SLA timers are owned entirely by
/// <c>ISlaService</c> and are never reset by anything in this story.
/// </summary>
public interface ISlaEscalationService
{
    /// <summary>
    /// Evaluates a single ticket's SLA state and records any newly-crossed milestones. Idempotent —
    /// safe to call repeatedly (a milestone already recorded is silently skipped, never duplicated or
    /// re-routed). Returns only the escalations created by *this* call, not the ticket's full history —
    /// use <see cref="ListForTicketAsync"/> for that. Returns an empty list for a ticket with no
    /// <c>TicketSla</c> row yet (e.g. one that pre-dates Story 22).
    /// </summary>
    /// <param name="now">Defaults to <see cref="DateTimeOffset.UtcNow"/> — overridable so tests can simulate "80%/100% elapsed" deterministically, the same seam <c>ISlaService.EvaluateBreaches</c> uses.</param>
    Task<IReadOnlyList<TicketEscalationDto>> EvaluateAsync(Guid ticketId, DateTimeOffset? now = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates every ticket not already Resolved/Closed (a resolved/closed ticket generates no
    /// further escalations of either SLA type — see <see cref="SlaEscalationService"/>'s remarks — so
    /// it is excluded from the sweep entirely rather than evaluated and found to have nothing to do).
    /// A single ticket's evaluation failing is logged and does not stop the rest. Returns every new
    /// escalation row created across every ticket evaluated (Story 25: the caller — the background
    /// service and the manual evaluate endpoint — uses this to fire one notification per new row).
    /// </summary>
    /// <param name="now">Same override seam as <see cref="EvaluateAsync"/> — defaults to <see cref="DateTimeOffset.UtcNow"/>.</param>
    Task<IReadOnlyList<TicketEscalationDto>> EvaluateAllOpenAsync(DateTimeOffset? now = null, CancellationToken cancellationToken = default);

    /// <summary>The full escalation history for one ticket, oldest first. Empty (not null) for a ticket with none yet.</summary>
    Task<IReadOnlyList<TicketEscalationDto>> ListForTicketAsync(Guid ticketId, CancellationToken cancellationToken = default);
}
