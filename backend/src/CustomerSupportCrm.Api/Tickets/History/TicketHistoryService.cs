using CustomerSupportCrm.Domain.Tickets;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupportCrm.Api.Tickets.History;

public sealed class TicketHistoryService(CrmDbContext db) : ITicketHistoryService
{
    public void Record(
        Guid ticketId,
        string eventType,
        string summary,
        string? field = null,
        string? previousValue = null,
        string? newValue = null,
        Guid? performedByUserId = null)
    {
        db.TicketHistories.Add(new TicketHistory
        {
            TicketId = ticketId,
            EventType = eventType,
            Field = field,
            PreviousValue = previousValue,
            NewValue = newValue,
            Summary = summary,
            PerformedByUserId = performedByUserId,
            CreatedAt = DateTimeOffset.UtcNow,
        });
    }

    public async Task<IReadOnlyList<TicketHistoryDto>> GetForTicketAsync(Guid ticketId, CancellationToken cancellationToken = default) =>
        await db.TicketHistories
            .AsNoTracking()
            .Where(h => h.TicketId == ticketId)
            .OrderBy(h => h.CreatedAt)
            .ThenBy(h => h.Id)
            .Select(h => new TicketHistoryDto(
                h.Id, h.TicketId, h.EventType, h.Field, h.PreviousValue, h.NewValue, h.Summary,
                h.PerformedByUserId, h.PerformedByUser != null ? h.PerformedByUser.DisplayName : null,
                h.CreatedAt))
            .ToListAsync(cancellationToken);

    public Task<bool> TicketExistsAsync(Guid ticketId, CancellationToken cancellationToken = default) =>
        db.Tickets.AsNoTracking().AnyAsync(t => t.Id == ticketId, cancellationToken);
}
