namespace CustomerSupportCrm.Api.Sla;

/// <summary>The read shape for one ticket's SLA state — same fields <see cref="ISlaService.EvaluateBreaches"/> reasons about, plus <see cref="TicketId"/> since callers of <see cref="ISlaService.GetForTicketAsync"/> don't already have the row in hand.</summary>
public sealed record TicketSlaSnapshot(
    Guid TicketId,
    DateTimeOffset StartedAt,
    DateTimeOffset FirstResponseDueAt,
    DateTimeOffset ResolutionDueAt,
    string FirstResponseStatus,
    string ResolutionStatus,
    DateTimeOffset? FirstResponseAt,
    DateTimeOffset? ResolvedAt);
