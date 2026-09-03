using CustomerSupportCrm.Api.Customers;
using CustomerSupportCrm.Api.LiveChat;
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

namespace CustomerSupportCrm.Api.Tests.LiveChat;

public class LiveChatServiceTests
{
    private static CrmDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<LiveChatService> SeedAsync(CrmDbContext db)
    {
        db.TicketCategories.Add(new TicketCategory { Name = "General Inquiry", NormalizedName = "GENERAL INQUIRY" });
        db.TicketPriorities.Add(new TicketPriority { Name = "Medium", NormalizedName = "MEDIUM", SortOrder = 20 });
        // StartAsync always attributes the ticket to the seeded system account — mirror that seed
        // row here rather than depending on DbSeeder in a unit test.
        db.Users.Add(new User { Email = DbSeeder.SystemUserEmail, DisplayName = "System (Automated)", PasswordHash = "x", IsActive = false });
        await db.SaveChangesAsync();

        var customersService = new CustomersService(db);
        var ticketsService = new TicketsService(db, new TicketHistoryService(db), new SlaService(db, NullLogger<SlaService>.Instance), new TicketAssignmentService(db, new TicketHistoryService(db), NullLogger<TicketAssignmentService>.Instance));
        return new CustomerSupportCrm.Api.LiveChat.LiveChatService(db, customersService, ticketsService, new SlaService(db, NullLogger<SlaService>.Instance));
    }

    [Fact]
    public async Task StartAsync_valid_payload_creates_customer_ticket_session_and_one_inbound_message()
    {
        await using var db = CreateDb();
        var service = await SeedAsync(db);

        var response = await service.StartAsync(new StartLiveChatSessionRequest("Ali Hassan", "ali@example.com", null, "Hi, I need help"));

        Assert.Equal(LiveChatStatus.Waiting, response.Status);

        var ticket = await db.Tickets.SingleAsync(t => t.Id == response.TicketId);
        Assert.Equal("LiveChat", ticket.SourceChannel);
        Assert.Equal(DbSeeder.SystemUserEmail, (await db.Users.SingleAsync(u => u.Id == ticket.CreatedByUserId)).Email);

        var session = await db.LiveChatSessions.SingleAsync(s => s.Id == response.SessionId);
        Assert.Equal(response.SessionToken, session.SessionToken);
        Assert.Equal(response.TicketId, session.TicketId);
        Assert.Equal(response.CustomerId, session.CustomerId);

        var interactions = await db.CustomerInteractions.Where(i => i.TicketId == response.TicketId).ToListAsync();
        Assert.Single(interactions);
        Assert.Equal("livechat_inbound", interactions[0].InteractionType);
    }

    /// <summary>
    /// Same "either identifier matches an existing customer" rule as the corrected
    /// WebFormSubmissionService — a customer who already has a phone on file from WhatsApp/SMS must
    /// land on that same record when they start a live chat with the same number, and a previously
    /// blank email gets backfilled rather than creating a duplicate.
    /// </summary>
    [Fact]
    public async Task StartAsync_with_a_phone_matching_an_existing_phone_only_customer_reuses_it_and_backfills_the_email()
    {
        await using var db = CreateDb();
        var service = await SeedAsync(db);
        var existing = new Customer { FirstName = "+201013840094", LastName = "(via WhatsApp)", Phone = "+201013840094", Email = null };
        db.Customers.Add(existing);
        await db.SaveChangesAsync();

        var response = await service.StartAsync(new StartLiveChatSessionRequest("Mohamed Ali", "mohamed@example.com", "01013840094", "Hi"));

        Assert.Equal(existing.Id, response.CustomerId);
        Assert.Equal(1, await db.Customers.CountAsync());

        var reloaded = await db.Customers.AsNoTracking().SingleAsync(c => c.Id == existing.Id);
        Assert.Equal("mohamed@example.com", reloaded.Email);
        Assert.Equal("+201013840094", reloaded.FirstName);
    }

    [Fact]
    public async Task StartAsync_does_not_overwrite_an_existing_email_with_a_different_one()
    {
        await using var db = CreateDb();
        var service = await SeedAsync(db);
        var existing = new Customer { FirstName = "Mohamed", LastName = "Ali", Phone = "+201013840094", Email = "original@example.com" };
        db.Customers.Add(existing);
        await db.SaveChangesAsync();

        await service.StartAsync(new StartLiveChatSessionRequest("Mohamed Ali", "different@example.com", "01013840094", "Hi"));

        var reloaded = await db.Customers.AsNoTracking().SingleAsync(c => c.Id == existing.Id);
        Assert.Equal("original@example.com", reloaded.Email);
    }

    [Fact]
    public async Task StartAsync_opens_a_new_ticket_for_every_session_even_from_the_same_customer()
    {
        await using var db = CreateDb();
        var service = await SeedAsync(db);

        var first = await service.StartAsync(new StartLiveChatSessionRequest("Ali", "ali@example.com", null, "First"));
        var second = await service.StartAsync(new StartLiveChatSessionRequest("Ali", "ali@example.com", null, "Second"));

        Assert.Equal(first.CustomerId, second.CustomerId);
        Assert.NotEqual(first.TicketId, second.TicketId);
        Assert.Equal(1, await db.Customers.CountAsync());
        Assert.Equal(2, await db.Tickets.CountAsync());
    }

    [Fact]
    public async Task AppendCustomerMessageAsync_with_the_correct_token_appends_a_message()
    {
        await using var db = CreateDb();
        var service = await SeedAsync(db);
        var started = await service.StartAsync(new StartLiveChatSessionRequest("Ali", "ali@example.com", null, "Hi"));

        var result = await service.AppendCustomerMessageAsync(started.SessionId, started.SessionToken, "Are you there?");

        Assert.Equal(LiveChatOperationOutcome.Success, result.Outcome);
        Assert.Equal("Customer", result.Message!.Sender);
        var interactions = await db.CustomerInteractions.Where(i => i.TicketId == started.TicketId).ToListAsync();
        Assert.Equal(2, interactions.Count);
    }

    [Fact]
    public async Task AppendCustomerMessageAsync_with_the_wrong_token_returns_InvalidSessionToken_and_writes_nothing()
    {
        await using var db = CreateDb();
        var service = await SeedAsync(db);
        var started = await service.StartAsync(new StartLiveChatSessionRequest("Ali", "ali@example.com", null, "Hi"));

        var result = await service.AppendCustomerMessageAsync(started.SessionId, "wrong-token", "Are you there?");

        Assert.Equal(LiveChatOperationOutcome.InvalidSessionToken, result.Outcome);
        var interactions = await db.CustomerInteractions.Where(i => i.TicketId == started.TicketId).ToListAsync();
        Assert.Single(interactions);
    }

    [Fact]
    public async Task AppendCustomerMessageAsync_with_a_blank_body_returns_InvalidBody()
    {
        await using var db = CreateDb();
        var service = await SeedAsync(db);
        var started = await service.StartAsync(new StartLiveChatSessionRequest("Ali", "ali@example.com", null, "Hi"));

        var result = await service.AppendCustomerMessageAsync(started.SessionId, started.SessionToken, "   ");

        Assert.Equal(LiveChatOperationOutcome.InvalidBody, result.Outcome);
    }

    [Fact]
    public async Task AppendCustomerMessageAsync_on_a_closed_ticket_returns_ConversationClosed()
    {
        await using var db = CreateDb();
        var service = await SeedAsync(db);
        var started = await service.StartAsync(new StartLiveChatSessionRequest("Ali", "ali@example.com", null, "Hi"));

        var ticket = await db.Tickets.SingleAsync(t => t.Id == started.TicketId);
        ticket.Status = TicketStatuses.Closed;
        await db.SaveChangesAsync();

        var result = await service.AppendCustomerMessageAsync(started.SessionId, started.SessionToken, "Still there?");

        Assert.Equal(LiveChatOperationOutcome.ConversationClosed, result.Outcome);
    }

    [Fact]
    public async Task AppendAgentMessageAsync_records_the_sending_agent()
    {
        await using var db = CreateDb();
        var service = await SeedAsync(db);
        var started = await service.StartAsync(new StartLiveChatSessionRequest("Ali", "ali@example.com", null, "Hi"));
        var agent = new User { Email = "agent@example.com", DisplayName = "Agent Smith", PasswordHash = "x", IsActive = true };
        db.Users.Add(agent);
        await db.SaveChangesAsync();

        var result = await service.AppendAgentMessageAsync(started.SessionId, agent.Id, "How can I help?");

        Assert.Equal(LiveChatOperationOutcome.Success, result.Outcome);
        Assert.Equal("Agent", result.Message!.Sender);
        Assert.Equal(agent.Id, result.Message.SenderUserId);
        Assert.Equal("Agent Smith", result.Message.SenderName);
    }

    [Fact]
    public async Task GetPublicSessionAsync_returns_messages_in_chronological_order()
    {
        await using var db = CreateDb();
        var service = await SeedAsync(db);
        var started = await service.StartAsync(new StartLiveChatSessionRequest("Ali", "ali@example.com", null, "First"));
        await service.AppendCustomerMessageAsync(started.SessionId, started.SessionToken, "Second");

        var result = await service.GetPublicSessionAsync(started.SessionId, started.SessionToken);

        Assert.Equal(LiveChatOperationOutcome.Success, result.Outcome);
        Assert.Equal(2, result.Session!.Messages.Count);
        Assert.Equal("First", result.Session.Messages[0].Body);
        Assert.Equal("Second", result.Session.Messages[1].Body);
    }

    [Fact]
    public async Task GetPublicSessionAsync_with_the_wrong_token_returns_InvalidSessionToken()
    {
        await using var db = CreateDb();
        var service = await SeedAsync(db);
        var started = await service.StartAsync(new StartLiveChatSessionRequest("Ali", "ali@example.com", null, "Hi"));

        var result = await service.GetPublicSessionAsync(started.SessionId, "wrong-token");

        Assert.Equal(LiveChatOperationOutcome.InvalidSessionToken, result.Outcome);
    }

    [Fact]
    public async Task ListForAgentAsync_filters_by_derived_status()
    {
        await using var db = CreateDb();
        var service = await SeedAsync(db);
        var waiting = await service.StartAsync(new StartLiveChatSessionRequest("Ali", "ali@example.com", null, "Hi"));
        var toAssign = await service.StartAsync(new StartLiveChatSessionRequest("Sara", "sara@example.com", null, "Hi"));

        var agent = new User { Email = "agent@example.com", DisplayName = "Agent Smith", PasswordHash = "x", IsActive = true };
        db.Users.Add(agent);
        var assignedTicket = await db.Tickets.SingleAsync(t => t.Id == toAssign.TicketId);
        assignedTicket.AssignedUserId = agent.Id;
        await db.SaveChangesAsync();

        var waitingList = await service.ListForAgentAsync(LiveChatStatus.Waiting);
        var activeList = await service.ListForAgentAsync(LiveChatStatus.Active);

        Assert.Single(waitingList);
        Assert.Equal(waiting.SessionId, waitingList[0].SessionId);
        Assert.Single(activeList);
        Assert.Equal(toAssign.SessionId, activeList[0].SessionId);
    }

    /// <summary>
    /// A plain Agent (no tickets.assign — the controller passes their own id as scopeToUserId) only
    /// sees conversations assigned to them; a caller with tickets.assign (Manager/Admin — the
    /// controller passes null) still sees everything, matching the pre-existing behavior.
    /// </summary>
    [Fact]
    public async Task ListForAgentAsync_with_a_scope_returns_only_conversations_assigned_to_that_user()
    {
        await using var db = CreateDb();
        var service = await SeedAsync(db);
        var mine = await service.StartAsync(new StartLiveChatSessionRequest("Ali", "ali@example.com", null, "Hi"));
        var someoneElses = await service.StartAsync(new StartLiveChatSessionRequest("Sara", "sara@example.com", null, "Hi"));
        // An unassigned conversation, thrown in to prove the scope excludes it too, not just other agents'.
        await service.StartAsync(new StartLiveChatSessionRequest("Omar", "omar@example.com", null, "Hi"));

        var me = new User { Email = "me@example.com", DisplayName = "Me", PasswordHash = "x", IsActive = true };
        var someoneElse = new User { Email = "other@example.com", DisplayName = "Other Agent", PasswordHash = "x", IsActive = true };
        db.Users.AddRange(me, someoneElse);
        (await db.Tickets.SingleAsync(t => t.Id == mine.TicketId)).AssignedUserId = me.Id;
        (await db.Tickets.SingleAsync(t => t.Id == someoneElses.TicketId)).AssignedUserId = someoneElse.Id;
        await db.SaveChangesAsync();

        var scoped = await service.ListForAgentAsync(status: null, scopeToUserId: me.Id);

        Assert.Single(scoped);
        Assert.Equal(mine.SessionId, scoped[0].SessionId);
    }

    [Fact]
    public async Task GetForAgentAsync_with_a_scope_returns_null_for_a_conversation_assigned_to_someone_else()
    {
        await using var db = CreateDb();
        var service = await SeedAsync(db);
        var session = await service.StartAsync(new StartLiveChatSessionRequest("Ali", "ali@example.com", null, "Hi"));
        var someoneElse = new User { Email = "other@example.com", DisplayName = "Other Agent", PasswordHash = "x", IsActive = true };
        db.Users.Add(someoneElse);
        (await db.Tickets.SingleAsync(t => t.Id == session.TicketId)).AssignedUserId = someoneElse.Id;
        await db.SaveChangesAsync();

        var result = await service.GetForAgentAsync(session.SessionId, scopeToUserId: Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetForAgentAsync_with_a_scope_returns_the_conversation_when_assigned_to_that_user()
    {
        await using var db = CreateDb();
        var service = await SeedAsync(db);
        var session = await service.StartAsync(new StartLiveChatSessionRequest("Ali", "ali@example.com", null, "Hi"));
        var me = new User { Email = "me@example.com", DisplayName = "Me", PasswordHash = "x", IsActive = true };
        db.Users.Add(me);
        (await db.Tickets.SingleAsync(t => t.Id == session.TicketId)).AssignedUserId = me.Id;
        await db.SaveChangesAsync();

        var result = await service.GetForAgentAsync(session.SessionId, scopeToUserId: me.Id);

        Assert.NotNull(result);
        Assert.Equal(session.SessionId, result!.SessionId);
    }

    [Fact]
    public async Task GetForAgentAsync_returns_null_for_an_unknown_session()
    {
        await using var db = CreateDb();
        var service = await SeedAsync(db);

        var result = await service.GetForAgentAsync(Guid.NewGuid());

        Assert.Null(result);
    }
}
