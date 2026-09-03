using CustomerSupportCrm.Api.Communications.Channels;
using CustomerSupportCrm.Api.Sla;
using CustomerSupportCrm.Api.Tickets.Assignment;
using CustomerSupportCrm.Api.Tickets.History;
using CustomerSupportCrm.Api.Tickets.Tickets;
using CustomerSupportCrm.Domain.Customers;
using CustomerSupportCrm.Domain.Sla;
using CustomerSupportCrm.Domain.Tickets;
using CustomerSupportCrm.Domain.Users;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CustomerSupportCrm.Api.Tests.Communications;

public class TicketChannelReplyServiceTests
{
    private static CrmDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<(Ticket Ticket, Guid AgentId)> SeedChannelTicketAsync(CrmDbContext db, string sourceChannel, string? customerPhone = "+201001234567")
    {
        var customer = new Customer { FirstName = "Jane", LastName = "Doe", Phone = customerPhone };
        var category = new TicketCategory { Name = "Billing", NormalizedName = "BILLING" };
        var priority = new TicketPriority { Name = "Low", NormalizedName = "LOW", SortOrder = 10 };
        var agent = new User { Email = "agent@local.test", DisplayName = "Agent", PasswordHash = "x" };
        db.AddRange(customer, category, priority, agent);
        await db.SaveChangesAsync();

        var ticket = new Ticket
        {
            CustomerId = customer.Id,
            Subject = "Cannot log in",
            Description = "Help please",
            CategoryId = category.Id,
            PriorityId = priority.Id,
            CreatedByUserId = agent.Id,
            SourceChannel = sourceChannel,
        };
        db.Tickets.Add(ticket);

        db.CustomerInteractions.Add(new CustomerInteraction
        {
            CustomerId = customer.Id,
            TicketId = ticket.Id,
            OccurredAt = DateTime.UtcNow,
            InteractionType = $"{sourceChannel.ToLowerInvariant()}_inbound",
            FromAddress = customerPhone,
            ExternalMessageId = "inbound-msg-1",
            CreatedAt = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();
        return (ticket, agent.Id);
    }

    private static IChannelMessageDispatcher DispatcherReturning(ChannelSendResult result) =>
        new ChannelMessageDispatcher([new FakeChannelMessageSender("WhatsApp", result), new FakeChannelMessageSender("Sms", result)]);

    [Fact]
    public async Task SendReplyAsync_on_success_persists_exactly_one_outbound_interaction_and_no_new_ticket()
    {
        await using var db = CreateDb();
        var (ticket, agentId) = await SeedChannelTicketAsync(db, "WhatsApp");
        var dispatcher = DispatcherReturning(ChannelSendResult.Succeeded("wa-out-1"));
        var service = new TicketChannelReplyService(db, dispatcher, new TicketsService(db, new TicketHistoryService(db), new SlaService(db, NullLogger<SlaService>.Instance), new TicketAssignmentService(db, new TicketHistoryService(db), NullLogger<TicketAssignmentService>.Instance)), new SlaService(db, NullLogger<SlaService>.Instance));

        var result = await service.SendReplyAsync(ticket.Id, "Thanks, please try again.", agentId);

        Assert.Equal(TicketChannelReplyOutcome.Success, result.Outcome);
        Assert.Equal(1, await db.Tickets.CountAsync());

        var outbound = await db.CustomerInteractions.SingleAsync(i => i.InteractionType == "whatsapp_outbound");
        Assert.Equal(ticket.Id, outbound.TicketId);
        Assert.Equal("wa-out-1", outbound.ExternalMessageId);
        Assert.Equal(agentId, outbound.UserId);
        Assert.Equal("+201001234567", outbound.ToAddress);
    }

    /// <summary>Story 22, plan item 14: the channel reply path is one of the "first outbound agent message" triggers <see cref="ISlaService.MarkFirstResponseAsync"/> is wired into.</summary>
    [Fact]
    public async Task SendReplyAsync_on_success_marks_first_response_sla_met_exactly_once()
    {
        await using var db = CreateDb();
        var (ticket, agentId) = await SeedChannelTicketAsync(db, "WhatsApp");
        var now = DateTimeOffset.UtcNow;
        var policy = new SlaPolicy { PriorityId = null, Name = "Default SLA", FirstResponseMinutes = 30, ResolutionMinutes = 240 };
        db.SlaPolicies.Add(policy);
        db.TicketSlas.Add(new TicketSla
        {
            TicketId = ticket.Id,
            SlaPolicyId = policy.Id,
            StartedAt = now,
            FirstResponseDueAt = now.AddMinutes(30),
            ResolutionDueAt = now.AddMinutes(240),
        });
        await db.SaveChangesAsync();

        var dispatcher = DispatcherReturning(ChannelSendResult.Succeeded("wa-out-1"));
        var service = new TicketChannelReplyService(db, dispatcher, new TicketsService(db, new TicketHistoryService(db), new SlaService(db, NullLogger<SlaService>.Instance), new TicketAssignmentService(db, new TicketHistoryService(db), NullLogger<TicketAssignmentService>.Instance)), new SlaService(db, NullLogger<SlaService>.Instance));

        await service.SendReplyAsync(ticket.Id, "Thanks, please try again.", agentId);

        var sla = await db.TicketSlas.SingleAsync(s => s.TicketId == ticket.Id);
        Assert.Equal(SlaStatuses.Met, sla.FirstResponseStatus);
        Assert.NotNull(sla.FirstResponseAt);

        // A second reply on the same ticket must not touch the already-terminal state.
        var firstResponseAt = sla.FirstResponseAt;
        await service.SendReplyAsync(ticket.Id, "Following up.", agentId);
        var slaAfterSecondReply = await db.TicketSlas.SingleAsync(s => s.TicketId == ticket.Id);
        Assert.Equal(firstResponseAt, slaAfterSecondReply.FirstResponseAt);
    }

    [Fact]
    public async Task SendReplyAsync_for_sms_writes_an_sms_outbound_interaction()
    {
        await using var db = CreateDb();
        var (ticket, agentId) = await SeedChannelTicketAsync(db, "Sms");
        var dispatcher = DispatcherReturning(ChannelSendResult.Succeeded("sms-out-1"));
        var service = new TicketChannelReplyService(db, dispatcher, new TicketsService(db, new TicketHistoryService(db), new SlaService(db, NullLogger<SlaService>.Instance), new TicketAssignmentService(db, new TicketHistoryService(db), NullLogger<TicketAssignmentService>.Instance)), new SlaService(db, NullLogger<SlaService>.Instance));

        await service.SendReplyAsync(ticket.Id, "Body", agentId);

        Assert.True(await db.CustomerInteractions.AnyAsync(i => i.InteractionType == "sms_outbound"));
    }

    [Fact]
    public async Task SendReplyAsync_when_the_sender_fails_persists_no_interaction_and_returns_SendFailed()
    {
        await using var db = CreateDb();
        var (ticket, agentId) = await SeedChannelTicketAsync(db, "WhatsApp");
        var dispatcher = DispatcherReturning(ChannelSendResult.Failed("provider timeout"));
        var service = new TicketChannelReplyService(db, dispatcher, new TicketsService(db, new TicketHistoryService(db), new SlaService(db, NullLogger<SlaService>.Instance), new TicketAssignmentService(db, new TicketHistoryService(db), NullLogger<TicketAssignmentService>.Instance)), new SlaService(db, NullLogger<SlaService>.Instance));

        var result = await service.SendReplyAsync(ticket.Id, "Body", agentId);

        Assert.Equal(TicketChannelReplyOutcome.SendFailed, result.Outcome);
        Assert.Equal(0, await db.CustomerInteractions.CountAsync(i => i.InteractionType == "whatsapp_outbound"));
    }

    [Fact]
    public async Task SendReplyAsync_on_an_email_ticket_returns_NotSendableChannel()
    {
        await using var db = CreateDb();
        var (ticket, agentId) = await SeedChannelTicketAsync(db, "Email");
        var dispatcher = DispatcherReturning(ChannelSendResult.Succeeded("x"));
        var service = new TicketChannelReplyService(db, dispatcher, new TicketsService(db, new TicketHistoryService(db), new SlaService(db, NullLogger<SlaService>.Instance), new TicketAssignmentService(db, new TicketHistoryService(db), NullLogger<TicketAssignmentService>.Instance)), new SlaService(db, NullLogger<SlaService>.Instance));

        var result = await service.SendReplyAsync(ticket.Id, "Body", agentId);

        Assert.Equal(TicketChannelReplyOutcome.NotSendableChannel, result.Outcome);
    }

    [Fact]
    public async Task SendReplyAsync_on_a_manual_ticket_returns_NotSendableChannel()
    {
        await using var db = CreateDb();
        var customer = new Customer { FirstName = "Jane", LastName = "Doe" };
        var category = new TicketCategory { Name = "Billing", NormalizedName = "BILLING" };
        var priority = new TicketPriority { Name = "Low", NormalizedName = "LOW", SortOrder = 10 };
        var agent = new User { Email = "agent@local.test", DisplayName = "Agent", PasswordHash = "x" };
        db.AddRange(customer, category, priority, agent);
        await db.SaveChangesAsync();
        var ticket = new Ticket { CustomerId = customer.Id, Subject = "S", Description = "D", CategoryId = category.Id, PriorityId = priority.Id, CreatedByUserId = agent.Id };
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();

        var dispatcher = DispatcherReturning(ChannelSendResult.Succeeded("x"));
        var service = new TicketChannelReplyService(db, dispatcher, new TicketsService(db, new TicketHistoryService(db), new SlaService(db, NullLogger<SlaService>.Instance), new TicketAssignmentService(db, new TicketHistoryService(db), NullLogger<TicketAssignmentService>.Instance)), new SlaService(db, NullLogger<SlaService>.Instance));

        var result = await service.SendReplyAsync(ticket.Id, "Body", agent.Id);

        Assert.Equal(TicketChannelReplyOutcome.NotSendableChannel, result.Outcome);
    }

    [Fact]
    public async Task SendReplyAsync_when_no_recipient_phone_exists_returns_NoRecipient()
    {
        await using var db = CreateDb();
        var (ticket, agentId) = await SeedChannelTicketAsync(db, "WhatsApp", customerPhone: null);
        // Remove the seeded inbound interaction's FromAddress fallback too.
        var inbound = await db.CustomerInteractions.SingleAsync();
        inbound.FromAddress = null;
        await db.SaveChangesAsync();

        var dispatcher = DispatcherReturning(ChannelSendResult.Succeeded("x"));
        var service = new TicketChannelReplyService(db, dispatcher, new TicketsService(db, new TicketHistoryService(db), new SlaService(db, NullLogger<SlaService>.Instance), new TicketAssignmentService(db, new TicketHistoryService(db), NullLogger<TicketAssignmentService>.Instance)), new SlaService(db, NullLogger<SlaService>.Instance));

        var result = await service.SendReplyAsync(ticket.Id, "Body", agentId);

        Assert.Equal(TicketChannelReplyOutcome.NoRecipient, result.Outcome);
    }

    [Fact]
    public async Task SendReplyAsync_with_a_whitespace_only_body_returns_InvalidBody()
    {
        await using var db = CreateDb();
        var (ticket, agentId) = await SeedChannelTicketAsync(db, "WhatsApp");
        var dispatcher = DispatcherReturning(ChannelSendResult.Succeeded("x"));
        var service = new TicketChannelReplyService(db, dispatcher, new TicketsService(db, new TicketHistoryService(db), new SlaService(db, NullLogger<SlaService>.Instance), new TicketAssignmentService(db, new TicketHistoryService(db), NullLogger<TicketAssignmentService>.Instance)), new SlaService(db, NullLogger<SlaService>.Instance));

        var result = await service.SendReplyAsync(ticket.Id, "   ", agentId);

        Assert.Equal(TicketChannelReplyOutcome.InvalidBody, result.Outcome);
    }

    [Fact]
    public async Task SendReplyAsync_on_an_unknown_ticket_returns_TicketNotFound()
    {
        await using var db = CreateDb();
        var dispatcher = DispatcherReturning(ChannelSendResult.Succeeded("x"));
        var service = new TicketChannelReplyService(db, dispatcher, new TicketsService(db, new TicketHistoryService(db), new SlaService(db, NullLogger<SlaService>.Instance), new TicketAssignmentService(db, new TicketHistoryService(db), NullLogger<TicketAssignmentService>.Instance)), new SlaService(db, NullLogger<SlaService>.Instance));

        var result = await service.SendReplyAsync(Guid.NewGuid(), "Body", Guid.NewGuid());

        Assert.Equal(TicketChannelReplyOutcome.TicketNotFound, result.Outcome);
    }
}
