using CustomerSupportCrm.Api.Communications.Email;
using CustomerSupportCrm.Api.Customers;
using CustomerSupportCrm.Api.Sla;
using CustomerSupportCrm.Api.Tickets.Assignment;
using CustomerSupportCrm.Api.Tickets.History;
using CustomerSupportCrm.Api.Tickets.Tickets;
using CustomerSupportCrm.Domain.Tickets;
using CustomerSupportCrm.Domain.Users;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CustomerSupportCrm.Api.Tests.Communications;

public class EmailIngestionServiceTests
{
    private static CrmDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<EmailIngestionService> SeedAsync(CrmDbContext db)
    {
        db.TicketCategories.Add(new TicketCategory { Name = "General Inquiry", NormalizedName = "GENERAL INQUIRY" });
        db.TicketPriorities.Add(new TicketPriority { Name = "Medium", NormalizedName = "MEDIUM", SortOrder = 20 });
        // Anonymous ingest (correction) always attributes the ticket to the seeded system account —
        // mirror that seed row here rather than depending on DbSeeder in a unit test.
        db.Users.Add(new User { Email = DbSeeder.SystemUserEmail, DisplayName = "System (Automated)", PasswordHash = "x", IsActive = false });
        await db.SaveChangesAsync();

        var customersService = new CustomersService(db);
        var ticketsService = new TicketsService(db, new TicketHistoryService(db), new SlaService(db, NullLogger<SlaService>.Instance), new TicketAssignmentService(db, new TicketHistoryService(db), NullLogger<TicketAssignmentService>.Instance));
        return new EmailIngestionService(db, customersService, ticketsService, NullLogger<EmailIngestionService>.Instance);
    }

    [Fact]
    public async Task IngestAsync_new_sender_creates_customer_ticket_and_one_inbound_interaction()
    {
        await using var db = CreateDb();
        var service = await SeedAsync(db);

        var result = await service.IngestAsync(
            new IncomingEmailRequest("jane@example.com", "support@crm.test", "Cannot log in", "Password reset fails", "msg-1", null));

        Assert.Equal(EmailIngestionOutcome.Success, result.Outcome);
        Assert.True(result.TicketCreated);

        var ticket = await db.Tickets.SingleAsync(t => t.Id == result.TicketId);
        Assert.Equal("Email", ticket.SourceChannel);
        Assert.Equal(DbSeeder.SystemUserEmail, (await db.Users.SingleAsync(u => u.Id == ticket.CreatedByUserId)).Email);

        var customer = await db.Customers.SingleAsync(c => c.Id == result.CustomerId);
        Assert.Equal("jane@example.com", customer.Email);

        var interactions = await db.CustomerInteractions.Where(i => i.TicketId == result.TicketId).ToListAsync();
        Assert.Single(interactions);
        Assert.Equal("email_inbound", interactions[0].InteractionType);
        Assert.Equal("msg-1", interactions[0].ExternalMessageId);
        Assert.Equal("Cannot log in", interactions[0].Summary);
    }

    [Fact]
    public async Task IngestAsync_existing_sender_reuses_the_customer()
    {
        await using var db = CreateDb();
        var service = await SeedAsync(db);

        var first = await service.IngestAsync(
            new IncomingEmailRequest("jane@example.com", null, "First issue", "Body one", "msg-1", null));
        var second = await service.IngestAsync(
            new IncomingEmailRequest("jane@example.com", null, "Second issue", "Body two", "msg-2", null));

        Assert.Equal(first.CustomerId, second.CustomerId);
        Assert.NotEqual(first.TicketId, second.TicketId);
        Assert.Equal(1, await db.Customers.CountAsync());
    }

    [Fact]
    public async Task IngestAsync_reply_to_a_previous_message_links_the_existing_ticket_without_duplicating()
    {
        await using var db = CreateDb();
        var service = await SeedAsync(db);

        var original = await service.IngestAsync(
            new IncomingEmailRequest("jane@example.com", null, "Cannot log in", "Help please", "msg-1", null));

        // Simulate the agent's earlier outbound reply that the customer is now replying to.
        db.CustomerInteractions.Add(new Domain.Customers.CustomerInteraction
        {
            CustomerId = original.CustomerId!.Value,
            TicketId = original.TicketId,
            OccurredAt = DateTime.UtcNow,
            InteractionType = "email_outbound",
            ExternalMessageId = "msg-2-outbound",
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var reply = await service.IngestAsync(
            new IncomingEmailRequest("jane@example.com", null, "Re: Cannot log in", "Still broken", "msg-3", "msg-2-outbound"));

        Assert.Equal(EmailIngestionOutcome.Success, reply.Outcome);
        Assert.False(reply.TicketCreated);
        Assert.Equal(original.TicketId, reply.TicketId);
        Assert.Equal(1, await db.Tickets.CountAsync());
    }

    [Fact]
    public async Task IngestAsync_duplicate_externalMessageId_is_idempotent()
    {
        await using var db = CreateDb();
        var service = await SeedAsync(db);

        var first = await service.IngestAsync(
            new IncomingEmailRequest("jane@example.com", null, "Cannot log in", "Help please", "msg-1", null));
        var replay = await service.IngestAsync(
            new IncomingEmailRequest("jane@example.com", null, "Cannot log in", "Help please", "msg-1", null));

        Assert.Equal(EmailIngestionOutcome.AlreadyProcessed, replay.Outcome);
        Assert.Equal(first.TicketId, replay.TicketId);
        Assert.Equal(1, await db.Tickets.CountAsync());
        Assert.Equal(1, await db.Customers.CountAsync());
        Assert.Equal(1, await db.CustomerInteractions.CountAsync());
    }
}
