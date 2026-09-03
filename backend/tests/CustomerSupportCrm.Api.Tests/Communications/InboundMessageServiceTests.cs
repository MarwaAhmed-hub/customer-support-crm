using CustomerSupportCrm.Api.Communications.Inbound;
using CustomerSupportCrm.Api.Customers;
using CustomerSupportCrm.Api.Sla;
using CustomerSupportCrm.Api.Tickets.Assignment;
using CustomerSupportCrm.Api.Tickets.History;
using CustomerSupportCrm.Api.Tickets.Tickets;
using CustomerSupportCrm.Domain.Tickets;
using CustomerSupportCrm.Domain.Users;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CustomerSupportCrm.Api.Tests.Communications;

public class InboundMessageServiceTests
{
    private static CrmDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<InboundMessageService> SeedAsync(CrmDbContext db)
    {
        db.TicketCategories.Add(new TicketCategory { Name = "General Inquiry", NormalizedName = "GENERAL INQUIRY" });
        db.TicketPriorities.Add(new TicketPriority { Name = "Medium", NormalizedName = "MEDIUM", SortOrder = 20 });
        // Anonymous ingest (correction) always attributes the ticket to the seeded system account —
        // mirror that seed row here rather than depending on DbSeeder in a unit test.
        db.Users.Add(new User { Email = DbSeeder.SystemUserEmail, DisplayName = "System (Automated)", PasswordHash = "x", IsActive = false });
        await db.SaveChangesAsync();

        var customersService = new CustomersService(db);
        var ticketsService = new TicketsService(db, new TicketHistoryService(db), new SlaService(db, NullLogger<SlaService>.Instance), new TicketAssignmentService(db, new TicketHistoryService(db), NullLogger<TicketAssignmentService>.Instance));
        return new InboundMessageService(db, customersService, ticketsService, NullLogger<InboundMessageService>.Instance);
    }

    [Fact]
    public async Task IngestAsync_creates_customer_when_phone_unknown()
    {
        await using var db = CreateDb();
        var service = await SeedAsync(db);

        var result = await service.IngestAsync(
            "WhatsApp", new InboundMessageRequest("+201001234567", "+201555", "Hello", "wa-1", null));

        Assert.False(result.Deduplicated);
        var customer = await db.Customers.SingleAsync(c => c.Id == result.CustomerId);
        Assert.Equal("+201001234567", customer.Phone);
    }

    [Fact]
    public async Task IngestAsync_reuses_customer_when_phone_matches_after_normalization()
    {
        await using var db = CreateDb();
        var service = await SeedAsync(db);

        var first = await service.IngestAsync("WhatsApp", new InboundMessageRequest("+20 100 123 4567", null, "First", "wa-1", null));
        var second = await service.IngestAsync("WhatsApp", new InboundMessageRequest("+20-100-123-4567", null, "Second", "wa-2", null));

        // Both forms normalize to "+201001234567" via PhoneNormalizer (digits + leading '+').
        Assert.Equal(1, await db.Customers.CountAsync());
        Assert.Equal(first.CustomerId, second.CustomerId);
    }

    [Fact]
    public async Task IngestAsync_creates_a_new_ticket_when_no_open_ticket_exists()
    {
        await using var db = CreateDb();
        var service = await SeedAsync(db);

        var result = await service.IngestAsync("WhatsApp", new InboundMessageRequest("+201001234567", null, "Hello", "wa-1", null));

        var ticket = await db.Tickets.SingleAsync(t => t.Id == result.TicketId);
        Assert.Equal("WhatsApp", ticket.SourceChannel);
        Assert.Equal(TicketStatuses.Open, ticket.Status);
        Assert.Equal(DbSeeder.SystemUserEmail, (await db.Users.SingleAsync(u => u.Id == ticket.CreatedByUserId)).Email);
    }

    /// <summary>
    /// Correction (second one — see the remarks on <see cref="InboundMessageService"/> for the full
    /// history): a matching <c>ExternalConversationId</c> on a still-open ticket for the same customer
    /// and channel reuses that ticket instead of always opening a new one. This is narrower than the
    /// original buggy behaviour (which reused "any open ticket on this channel" with no regard for the
    /// conversation) — reuse is scoped to one exact conversation id.
    /// </summary>
    [Fact]
    public async Task IngestAsync_with_a_matching_conversationId_on_a_still_open_ticket_reuses_it()
    {
        await using var db = CreateDb();
        var service = await SeedAsync(db);

        var first = await service.IngestAsync("WhatsApp", new InboundMessageRequest("+201001234567", null, "First", "wa-1", "conv-1"));
        var second = await service.IngestAsync("WhatsApp", new InboundMessageRequest("+201001234567", null, "Second", "wa-2", "conv-1"));

        Assert.Equal(first.TicketId, second.TicketId);
        Assert.Equal(first.CustomerId, second.CustomerId);
        Assert.Equal(1, await db.Tickets.CountAsync());
        Assert.Equal(2, await db.CustomerInteractions.CountAsync());
        Assert.Equal("conv-1", (await db.Tickets.SingleAsync()).ExternalConversationId);
    }

    [Fact]
    public async Task IngestAsync_with_a_different_conversationId_opens_a_separate_ticket()
    {
        await using var db = CreateDb();
        var service = await SeedAsync(db);

        var first = await service.IngestAsync("WhatsApp", new InboundMessageRequest("+201001234567", null, "First", "wa-1", "conv-1"));
        var second = await service.IngestAsync("WhatsApp", new InboundMessageRequest("+201001234567", null, "Second", "wa-2", "conv-2"));

        Assert.NotEqual(first.TicketId, second.TicketId);
        Assert.Equal(2, await db.Tickets.CountAsync());
    }

    [Fact]
    public async Task IngestAsync_with_a_matching_conversationId_on_a_closed_ticket_opens_a_new_ticket_instead_of_reusing_it()
    {
        await using var db = CreateDb();
        var service = await SeedAsync(db);

        var first = await service.IngestAsync("WhatsApp", new InboundMessageRequest("+201001234567", null, "First", "wa-1", "conv-1"));
        var openedTicket = await db.Tickets.SingleAsync(t => t.Id == first.TicketId);
        openedTicket.Status = TicketStatuses.Closed;
        await db.SaveChangesAsync();

        var second = await service.IngestAsync("WhatsApp", new InboundMessageRequest("+201001234567", null, "Second", "wa-2", "conv-1"));

        Assert.NotEqual(first.TicketId, second.TicketId);
        Assert.Equal(2, await db.Tickets.CountAsync());
    }

    [Fact]
    public async Task IngestAsync_opens_a_new_ticket_for_every_distinct_message_from_the_same_customer()
    {
        await using var db = CreateDb();
        var service = await SeedAsync(db);

        var first = await service.IngestAsync("WhatsApp", new InboundMessageRequest("+201001234567", null, "First", "wa-1", null));
        var second = await service.IngestAsync("WhatsApp", new InboundMessageRequest("+201001234567", null, "Second", "wa-2", null));

        // Correction: the earlier behaviour reused the customer's most recent open ticket, silently
        // folding every message after the first into interaction history instead of raising its own
        // ticket. Same customer (reused), but a new ticket every time.
        Assert.NotEqual(first.TicketId, second.TicketId);
        Assert.Equal(first.CustomerId, second.CustomerId);
        Assert.Equal(2, await db.Tickets.CountAsync());
        Assert.Equal(1, await db.Customers.CountAsync());
        Assert.Equal(2, await db.CustomerInteractions.CountAsync());
    }

    [Fact]
    public async Task IngestAsync_leaves_category_and_priority_at_the_channel_defaults()
    {
        await using var db = CreateDb();
        var service = await SeedAsync(db);

        var result = await service.IngestAsync("WhatsApp", new InboundMessageRequest("+201001234567", null, "Hello", "wa-1", null));

        var ticket = await db.Tickets.Include(t => t.Category).Include(t => t.Priority).SingleAsync(t => t.Id == result.TicketId);
        Assert.Equal("General Inquiry", ticket.Category!.Name);
        Assert.Equal("Medium", ticket.Priority!.Name);
    }

    [Fact]
    public async Task IngestAsync_is_idempotent_on_a_duplicate_externalMessageId()
    {
        await using var db = CreateDb();
        var service = await SeedAsync(db);

        var first = await service.IngestAsync("WhatsApp", new InboundMessageRequest("+201001234567", null, "Hello", "wa-1", null));
        var replay = await service.IngestAsync("WhatsApp", new InboundMessageRequest("+201001234567", null, "Hello", "wa-1", null));

        Assert.True(replay.Deduplicated);
        Assert.Equal(first.TicketId, replay.TicketId);
        Assert.Equal(1, await db.Tickets.CountAsync());
        Assert.Equal(1, await db.CustomerInteractions.CountAsync());
    }

    [Fact]
    public async Task IngestAsync_for_sms_uses_the_sms_channel_and_a_separate_ticket_from_whatsapp()
    {
        await using var db = CreateDb();
        var service = await SeedAsync(db);

        var whatsapp = await service.IngestAsync("WhatsApp", new InboundMessageRequest("+201001234567", null, "Hi via WhatsApp", "wa-1", null));
        var sms = await service.IngestAsync("Sms", new InboundMessageRequest("+201001234567", null, "Hi via SMS", "sms-1", null));

        Assert.NotEqual(whatsapp.TicketId, sms.TicketId);
        var smsTicket = await db.Tickets.SingleAsync(t => t.Id == sms.TicketId);
        Assert.Equal("Sms", smsTicket.SourceChannel);
        var smsInteraction = await db.CustomerInteractions.SingleAsync(i => i.TicketId == sms.TicketId);
        Assert.Equal("sms_inbound", smsInteraction.InteractionType);
    }

    /// <summary>
    /// Correction: the same phone number must resolve to the same customer regardless of which
    /// channel it arrives through or how it happens to be formatted (local "0..." vs full "20...")
    /// — the reported bug was that a WhatsApp number typed with the country code and the same number
    /// typed in local form via SMS were treated as two different people.
    /// </summary>
    [Fact]
    public async Task IngestAsync_treats_the_local_and_international_form_of_the_same_number_as_the_same_customer_across_channels()
    {
        await using var db = CreateDb();
        var service = await SeedAsync(db);

        var whatsapp = await service.IngestAsync("WhatsApp", new InboundMessageRequest("201234500001", null, "Complaint via WhatsApp", "wa-1", null));
        var sms = await service.IngestAsync("Sms", new InboundMessageRequest("01234500001", null, "Complaint via SMS", "sms-1", null));

        Assert.Equal(whatsapp.CustomerId, sms.CustomerId);
        Assert.NotEqual(whatsapp.TicketId, sms.TicketId);
        Assert.Equal(1, await db.Customers.CountAsync());
        Assert.Equal(2, await db.Tickets.CountAsync());
    }

    [Fact]
    public async Task IngestAsync_without_a_conversationId_never_reuses_even_a_closed_ticket()
    {
        await using var db = CreateDb();
        var service = await SeedAsync(db);

        var first = await service.IngestAsync("WhatsApp", new InboundMessageRequest("+201001234567", null, "First", "wa-1", null));
        var ticket = await db.Tickets.SingleAsync(t => t.Id == first.TicketId);
        ticket.Status = TicketStatuses.Closed;
        await db.SaveChangesAsync();

        var second = await service.IngestAsync("WhatsApp", new InboundMessageRequest("+201001234567", null, "Second", "wa-2", null));

        Assert.NotEqual(first.TicketId, second.TicketId);
        Assert.Equal(2, await db.Tickets.CountAsync());
    }
}
