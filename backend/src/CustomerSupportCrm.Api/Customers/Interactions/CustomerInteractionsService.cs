using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupportCrm.Api.Customers.Interactions;

/// <summary>Read-only, newest-first interaction history for a single customer. No create/update/delete here — see Story 08's "Out of scope".</summary>
public interface ICustomerInteractionsService
{
    /// <summary>
    /// A null return means the customer does not exist — the controller turns that into a 404. A
    /// customer that exists but has no interactions returns an empty page (Total = 0), not null.
    /// <paramref name="ticketId"/> narrows the customer's history down to interactions tied to one
    /// ticket (Story 19's <c>CustomerInteraction.TicketId</c>) — used by the ticket detail page's own
    /// "Interaction History" panel so a busy repeat customer's page-long history doesn't have to be
    /// scanned by hand to find the handful of messages that belong to the ticket being viewed.
    /// </summary>
    Task<CustomerInteractionListResponse?> ListForCustomerAsync(
        Guid customerId, int page, int pageSize, Guid? ticketId = null, CancellationToken cancellationToken = default);
}

public sealed class CustomerInteractionsService(CrmDbContext db) : ICustomerInteractionsService
{
    public async Task<CustomerInteractionListResponse?> ListForCustomerAsync(
        Guid customerId, int page, int pageSize, Guid? ticketId = null, CancellationToken cancellationToken = default)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == customerId, cancellationToken))
        {
            return null;
        }

        // Same clamp range as Departments/Branches/Audit-log listing.
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);

        var query = db.CustomerInteractions
            .AsNoTracking()
            .Where(i => i.CustomerId == customerId);

        if (ticketId.HasValue)
        {
            query = query.Where(i => i.TicketId == ticketId.Value);
        }

        var total = await query.CountAsync(cancellationToken);

        // Newest first by OccurredAt, then CreatedAt as a stable tiebreaker for same-instant records.
        // Projecting straight to the DTO (rather than .Include(x => x.User)) lets EF generate a single
        // query with a join for UserDisplayName, without materialising the full User entity.
        var items = await query
            .OrderByDescending(i => i.OccurredAt)
            .ThenByDescending(i => i.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(i => new CustomerInteractionDto(
                i.Id,
                i.CustomerId,
                i.OccurredAt,
                i.InteractionType,
                i.Summary,
                i.Details,
                i.UserId,
                i.User != null ? i.User.DisplayName : null))
            .ToListAsync(cancellationToken);

        return new CustomerInteractionListResponse(items, total, page, pageSize);
    }
}
