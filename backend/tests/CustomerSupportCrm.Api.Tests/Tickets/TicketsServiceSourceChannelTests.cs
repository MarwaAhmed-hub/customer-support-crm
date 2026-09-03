using CustomerSupportCrm.Api.Sla;
using CustomerSupportCrm.Api.Tickets.Assignment;
using CustomerSupportCrm.Api.Tickets.History;
using CustomerSupportCrm.Api.Tickets.Tickets;
using CustomerSupportCrm.Domain.Customers;
using CustomerSupportCrm.Domain.Tickets;
using CustomerSupportCrm.Domain.Users;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CustomerSupportCrm.Api.Tests.Tickets;

/// <summary>Story 19: <see cref="TicketsService.CreateAsync"/>'s new optional <c>sourceChannel</c> parameter.</summary>
public class TicketsServiceSourceChannelTests
{
    private static CrmDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<(TicketsService Service, Customer Customer, TicketCategory Category, TicketPriority Priority, User Creator)> SeedAsync(CrmDbContext db)
    {
        var customer = new Customer { FirstName = "Jane", LastName = "Doe" };
        var category = new TicketCategory { Name = "Billing", NormalizedName = "BILLING" };
        var priority = new TicketPriority { Name = "Low", NormalizedName = "LOW", SortOrder = 10 };
        var creator = new User { Email = "creator@local.test", DisplayName = "Creator", PasswordHash = "x" };
        db.AddRange(customer, category, priority, creator);
        await db.SaveChangesAsync();

        return (new TicketsService(db, new TicketHistoryService(db), new SlaService(db, NullLogger<SlaService>.Instance), new TicketAssignmentService(db, new TicketHistoryService(db), NullLogger<TicketAssignmentService>.Instance)), customer, category, priority, creator);
    }

    [Fact]
    public async Task CreateAsync_with_no_sourceChannel_leaves_ticket_SourceChannel_null_and_writes_the_ticket_interaction()
    {
        await using var db = CreateDb();
        var (service, customer, category, priority, creator) = await SeedAsync(db);

        var result = await service.CreateAsync(new CreateTicketRequest(customer.Id, "Subject", "Description", category.Id, priority.Id), creator.Id);

        Assert.Equal(TicketOperationOutcome.Success, result.Outcome);
        Assert.Null(result.Ticket!.SourceChannel);

        var interactions = await db.CustomerInteractions.Where(i => i.CustomerId == customer.Id).ToListAsync();
        Assert.Single(interactions);
        Assert.Equal("ticket", interactions[0].InteractionType);
        Assert.Equal(result.Ticket.Id, interactions[0].TicketId);
    }

    [Fact]
    public async Task CreateAsync_with_an_email_sourceChannel_sets_it_on_the_ticket_and_skips_the_generic_ticket_interaction()
    {
        await using var db = CreateDb();
        var (service, customer, category, priority, creator) = await SeedAsync(db);

        var result = await service.CreateAsync(
            new CreateTicketRequest(customer.Id, "Subject", "Description", category.Id, priority.Id), creator.Id, sourceChannel: "Email");

        Assert.Equal(TicketOperationOutcome.Success, result.Outcome);
        Assert.Equal("Email", result.Ticket!.SourceChannel);

        // No "ticket" interaction — the caller (EmailIngestionService) writes its own inbound
        // interaction right after this returns, and writing both would double up.
        Assert.Empty(await db.CustomerInteractions.Where(i => i.CustomerId == customer.Id).ToListAsync());
    }
}
