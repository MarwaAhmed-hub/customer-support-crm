namespace CustomerSupportCrm.Api.Tickets.History;

/// <summary>
/// Records and reads a ticket's business-lifecycle timeline (Story 14). See <see cref="Domain.Tickets.TicketHistory"/>
/// for what "business lifecycle" means here — creation, field edits, assignment, and status changes;
/// not escalation, and not a substitute for <c>AuditLog</c> or <c>CustomerInteraction</c>.
/// </summary>
public interface ITicketHistoryService
{
    /// <summary>
    /// Attaches a <see cref="Domain.Tickets.TicketHistory"/> entity to the tracked
    /// <c>CrmDbContext</c> — it does **not** call <c>SaveChangesAsync</c>. The caller (always
    /// <c>TicketsService</c>, immediately before its own single <c>SaveChangesAsync</c> for the
    /// mutation) owns the unit of work, so a failure between this call and that save rolls back both
    /// the ticket change and the history row together — no orphan entries.
    /// </summary>
    void Record(
        Guid ticketId,
        string eventType,
        string summary,
        string? field = null,
        string? previousValue = null,
        string? newValue = null,
        Guid? performedByUserId = null);

    /// <summary>Chronological (oldest first) — matches the timeline narrative of the read-only history panel.</summary>
    Task<IReadOnlyList<TicketHistoryDto>> GetForTicketAsync(Guid ticketId, CancellationToken cancellationToken = default);

    Task<bool> TicketExistsAsync(Guid ticketId, CancellationToken cancellationToken = default);
}
