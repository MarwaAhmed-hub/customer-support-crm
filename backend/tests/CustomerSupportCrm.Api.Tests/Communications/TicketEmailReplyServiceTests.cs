using CustomerSupportCrm.Api.Communications.Email;
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

/// <summary>Records every call and returns a configurable canned result — never touches anything real.</summary>
public sealed class FakeEmailSender(EmailSendResult result) : IEmailSender
{
    public List<OutgoingEmail> SentMessages { get; } = [];

    public Task<EmailSendResult> SendAsync(OutgoingEmail message, CancellationToken cancellationToken = default)
    {
        SentMessages.Add(message);
        return Task.FromResult(result);
    }
}

public class TicketEmailReplyServiceTests
{
    private static CrmDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<(Ticket Ticket, Guid AgentId)> SeedEmailTicketAsync(CrmDbContext db, string? customerEmail = "jane@example.com")
    {
        var customer = new Customer { FirstName = "Jane", LastName = "Doe", Email = customerEmail };
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
            SourceChannel = "Email",
        };
        db.Tickets.Add(ticket);

        db.CustomerInteractions.Add(new CustomerInteraction
        {
            CustomerId = customer.Id,
            TicketId = ticket.Id,
            OccurredAt = DateTime.UtcNow,
            InteractionType = "email_inbound",
            ExternalMessageId = "inbound-msg-1",
            CreatedAt = DateTime.UtcNow,
        });

        await db.SaveChangesAsync();
        return (ticket, agent.Id);
    }

    [Fact]
    public async Task SendReplyAsync_on_success_persists_exactly_one_outbound_interaction_and_no_new_ticket()
    {
        await using var db = CreateDb();
        var (ticket, agentId) = await SeedEmailTicketAsync(db);
        var sender = new FakeEmailSender(EmailSendResult.Succeeded("outbound-msg-1"));
        var service = new TicketEmailReplyService(db, sender, new TicketsService(db, new TicketHistoryService(db), new SlaService(db, NullLogger<SlaService>.Instance), new TicketAssignmentService(db, new TicketHistoryService(db), NullLogger<TicketAssignmentService>.Instance)), new SlaService(db, NullLogger<SlaService>.Instance));

        var result = await service.SendReplyAsync(ticket.Id, "Thanks, please try again.", agentId);

        Assert.Equal(TicketEmailReplyOutcome.Success, result.Outcome);
        Assert.Equal(1, await db.Tickets.CountAsync());

        var outbound = await db.CustomerInteractions.SingleAsync(i => i.InteractionType == "email_outbound");
        Assert.Equal(ticket.Id, outbound.TicketId);
        Assert.Equal("outbound-msg-1", outbound.ExternalMessageId);
        Assert.Equal(agentId, outbound.UserId);
        Assert.Equal("Thanks, please try again.", outbound.Details);

        Assert.Single(sender.SentMessages);
        Assert.Equal("jane@example.com", sender.SentMessages[0].ToAddress);
        Assert.Equal("inbound-msg-1", sender.SentMessages[0].InReplyToMessageId);
    }

    /// <summary>Story 22, plan item 15: the email reply path is one of the "first outbound agent message" triggers <see cref="ISlaService.MarkFirstResponseAsync"/> is wired into.</summary>
    [Fact]
    public async Task SendReplyAsync_on_success_marks_first_response_sla_met_exactly_once()
    {
        await using var db = CreateDb();
        var (ticket, agentId) = await SeedEmailTicketAsync(db);
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

        var sender = new FakeEmailSender(EmailSendResult.Succeeded("outbound-msg-1"));
        var service = new TicketEmailReplyService(db, sender, new TicketsService(db, new TicketHistoryService(db), new SlaService(db, NullLogger<SlaService>.Instance), new TicketAssignmentService(db, new TicketHistoryService(db), NullLogger<TicketAssignmentService>.Instance)), new SlaService(db, NullLogger<SlaService>.Instance));

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
    public async Task SendReplyAsync_when_the_sender_fails_persists_no_interaction_and_returns_SendFailed()
    {
        await using var db = CreateDb();
        var (ticket, agentId) = await SeedEmailTicketAsync(db);
        var sender = new FakeEmailSender(EmailSendResult.Failed("smtp timeout"));
        var service = new TicketEmailReplyService(db, sender, new TicketsService(db, new TicketHistoryService(db), new SlaService(db, NullLogger<SlaService>.Instance), new TicketAssignmentService(db, new TicketHistoryService(db), NullLogger<TicketAssignmentService>.Instance)), new SlaService(db, NullLogger<SlaService>.Instance));

        var result = await service.SendReplyAsync(ticket.Id, "Thanks, please try again.", agentId);

        Assert.Equal(TicketEmailReplyOutcome.SendFailed, result.Outcome);
        Assert.Equal(0, await db.CustomerInteractions.CountAsync(i => i.InteractionType == "email_outbound"));
    }

    [Fact]
    public async Task SendReplyAsync_on_a_non_email_ticket_returns_NotEmailChannel_and_never_calls_the_sender()
    {
        await using var db = CreateDb();
        var (ticket, agentId) = await SeedEmailTicketAsync(db);
        ticket.SourceChannel = null;
        await db.SaveChangesAsync();
        var sender = new FakeEmailSender(EmailSendResult.Succeeded("x"));
        var service = new TicketEmailReplyService(db, sender, new TicketsService(db, new TicketHistoryService(db), new SlaService(db, NullLogger<SlaService>.Instance), new TicketAssignmentService(db, new TicketHistoryService(db), NullLogger<TicketAssignmentService>.Instance)), new SlaService(db, NullLogger<SlaService>.Instance));

        var result = await service.SendReplyAsync(ticket.Id, "Body", agentId);

        Assert.Equal(TicketEmailReplyOutcome.NotEmailChannel, result.Outcome);
        Assert.Empty(sender.SentMessages);
    }

    [Fact]
    public async Task SendReplyAsync_when_the_customer_has_no_email_returns_CustomerHasNoEmail()
    {
        await using var db = CreateDb();
        var (ticket, agentId) = await SeedEmailTicketAsync(db, customerEmail: null);
        var sender = new FakeEmailSender(EmailSendResult.Succeeded("x"));
        var service = new TicketEmailReplyService(db, sender, new TicketsService(db, new TicketHistoryService(db), new SlaService(db, NullLogger<SlaService>.Instance), new TicketAssignmentService(db, new TicketHistoryService(db), NullLogger<TicketAssignmentService>.Instance)), new SlaService(db, NullLogger<SlaService>.Instance));

        var result = await service.SendReplyAsync(ticket.Id, "Body", agentId);

        Assert.Equal(TicketEmailReplyOutcome.CustomerHasNoEmail, result.Outcome);
    }

    [Fact]
    public async Task SendReplyAsync_with_a_whitespace_only_body_returns_InvalidBody()
    {
        await using var db = CreateDb();
        var (ticket, agentId) = await SeedEmailTicketAsync(db);
        var sender = new FakeEmailSender(EmailSendResult.Succeeded("x"));
        var service = new TicketEmailReplyService(db, sender, new TicketsService(db, new TicketHistoryService(db), new SlaService(db, NullLogger<SlaService>.Instance), new TicketAssignmentService(db, new TicketHistoryService(db), NullLogger<TicketAssignmentService>.Instance)), new SlaService(db, NullLogger<SlaService>.Instance));

        var result = await service.SendReplyAsync(ticket.Id, "   ", agentId);

        Assert.Equal(TicketEmailReplyOutcome.InvalidBody, result.Outcome);
    }

    [Fact]
    public async Task SendReplyAsync_uses_the_most_recent_inbound_message_for_InReplyTo()
    {
        await using var db = CreateDb();
        var (ticket, agentId) = await SeedEmailTicketAsync(db);
        db.CustomerInteractions.Add(new CustomerInteraction
        {
            CustomerId = ticket.CustomerId,
            TicketId = ticket.Id,
            OccurredAt = DateTime.UtcNow.AddMinutes(5),
            InteractionType = "email_inbound",
            ExternalMessageId = "inbound-msg-2-latest",
            CreatedAt = DateTime.UtcNow.AddMinutes(5),
        });
        await db.SaveChangesAsync();
        var sender = new FakeEmailSender(EmailSendResult.Succeeded("x"));
        var service = new TicketEmailReplyService(db, sender, new TicketsService(db, new TicketHistoryService(db), new SlaService(db, NullLogger<SlaService>.Instance), new TicketAssignmentService(db, new TicketHistoryService(db), NullLogger<TicketAssignmentService>.Instance)), new SlaService(db, NullLogger<SlaService>.Instance));

        await service.SendReplyAsync(ticket.Id, "Body", agentId);

        Assert.Equal("inbound-msg-2-latest", sender.SentMessages[0].InReplyToMessageId);
    }

    [Fact]
    public async Task SendReplyAsync_on_an_unknown_ticket_returns_TicketNotFound()
    {
        await using var db = CreateDb();
        var sender = new FakeEmailSender(EmailSendResult.Succeeded("x"));
        var service = new TicketEmailReplyService(db, sender, new TicketsService(db, new TicketHistoryService(db), new SlaService(db, NullLogger<SlaService>.Instance), new TicketAssignmentService(db, new TicketHistoryService(db), NullLogger<TicketAssignmentService>.Instance)), new SlaService(db, NullLogger<SlaService>.Instance));

        var result = await service.SendReplyAsync(Guid.NewGuid(), "Body", Guid.NewGuid());

        Assert.Equal(TicketEmailReplyOutcome.TicketNotFound, result.Outcome);
    }
}
