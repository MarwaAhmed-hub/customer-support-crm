using CustomerSupportCrm.Api.Customers.Interactions;
using CustomerSupportCrm.Domain.Customers;
using CustomerSupportCrm.Domain.Tickets;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CustomerSupportCrm.Api.Tests.Customers;

/// <summary>
/// The ticket-scoped read the ticket detail page's "Interaction History" panel relies on — narrowing
/// a customer's full interaction history down to the handful tied to one <c>Ticket</c> via
/// <c>CustomerInteraction.TicketId</c>.
/// </summary>
public class CustomerInteractionsServiceTests
{
    private static CrmDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<T> AddAsync<T>(CrmDbContext db, T entity) where T : class
    {
        db.Set<T>().Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }

    private static async Task<(CrmDbContext db, Customer customer, Ticket ticketA, Ticket ticketB)> SeedAsync()
    {
        var db = CreateDb();
        var customer = await AddAsync(db, new Customer { FirstName = "Jane", LastName = "Doe" });
        var category = await AddAsync(db, new TicketCategory { Name = "General Inquiry", NormalizedName = "GENERAL INQUIRY" });
        var priority = await AddAsync(db, new TicketPriority { Name = "Low", NormalizedName = "LOW", SortOrder = 10 });
        var creator = await AddAsync(db, new Domain.Users.User { Email = "creator@local.test", DisplayName = "Creator", PasswordHash = "x" });
        var ticketA = await AddAsync(db, new Ticket { CustomerId = customer.Id, Subject = "A", Description = "A", CategoryId = category.Id, PriorityId = priority.Id, CreatedByUserId = creator.Id });
        var ticketB = await AddAsync(db, new Ticket { CustomerId = customer.Id, Subject = "B", Description = "B", CategoryId = category.Id, PriorityId = priority.Id, CreatedByUserId = creator.Id });

        await AddAsync(db, new CustomerInteraction { CustomerId = customer.Id, TicketId = ticketA.Id, InteractionType = "email_inbound", OccurredAt = DateTime.UtcNow });
        await AddAsync(db, new CustomerInteraction { CustomerId = customer.Id, TicketId = ticketA.Id, InteractionType = "email_outbound", OccurredAt = DateTime.UtcNow });
        await AddAsync(db, new CustomerInteraction { CustomerId = customer.Id, TicketId = ticketB.Id, InteractionType = "email_inbound", OccurredAt = DateTime.UtcNow });
        await AddAsync(db, new CustomerInteraction { CustomerId = customer.Id, TicketId = null, InteractionType = "note-log", OccurredAt = DateTime.UtcNow });

        return (db, customer, ticketA, ticketB);
    }

    [Fact]
    public async Task ListForCustomerAsync_without_a_ticketId_returns_every_interaction()
    {
        var (db, customer, _, _) = await SeedAsync();
        var service = new CustomerInteractionsService(db);

        var result = await service.ListForCustomerAsync(customer.Id, page: 1, pageSize: 25);

        Assert.Equal(4, result!.Total);
    }

    [Fact]
    public async Task ListForCustomerAsync_with_a_ticketId_returns_only_that_tickets_interactions()
    {
        var (db, customer, ticketA, _) = await SeedAsync();
        var service = new CustomerInteractionsService(db);

        var result = await service.ListForCustomerAsync(customer.Id, page: 1, pageSize: 25, ticketId: ticketA.Id);

        Assert.Equal(2, result!.Total);
        Assert.All(result.Items, item => Assert.Contains(item.InteractionType, new[] { "email_inbound", "email_outbound" }));
    }

    [Fact]
    public async Task ListForCustomerAsync_with_a_ticketId_that_has_no_interactions_returns_an_empty_page()
    {
        var (db, customer, _, _) = await SeedAsync();
        var service = new CustomerInteractionsService(db);

        var result = await service.ListForCustomerAsync(customer.Id, page: 1, pageSize: 25, ticketId: Guid.NewGuid());

        Assert.Equal(0, result!.Total);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task ListForCustomerAsync_returns_null_for_an_unknown_customer_regardless_of_ticketId()
    {
        await using var db = CreateDb();
        var service = new CustomerInteractionsService(db);

        var result = await service.ListForCustomerAsync(Guid.NewGuid(), page: 1, pageSize: 25, ticketId: Guid.NewGuid());

        Assert.Null(result);
    }
}
