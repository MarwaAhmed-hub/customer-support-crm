using CustomerSupportCrm.Api.Notifications;
using CustomerSupportCrm.Api.Sla.Escalations;
using CustomerSupportCrm.Domain.Customers;
using CustomerSupportCrm.Domain.Notifications;
using CustomerSupportCrm.Domain.Tickets;
using CustomerSupportCrm.Domain.Users;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CustomerSupportCrm.Api.Tests.Notifications;

/// <summary>
/// Story 25: <see cref="NotificationService"/> — creation from the two events Stories 23/24 already
/// produce (a ticket newly assigned, a new <c>TicketEscalation</c> row), idempotency by
/// <c>DedupeKey</c>, and the caller-scoped inbox/mark-read surface.
/// </summary>
public class NotificationServiceTests
{
    private static CrmDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static NotificationService CreateService(CrmDbContext db) =>
        new(db, NullLogger<NotificationService>.Instance);

    private static async Task<T> AddAsync<T>(CrmDbContext db, T entity) where T : class
    {
        db.Set<T>().Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }

    private static async Task<(CrmDbContext db, Ticket ticket, User agent)> SeedTicketAsync()
    {
        var db = CreateDb();
        var customer = await AddAsync(db, new Customer { FirstName = "Jane", LastName = "Doe" });
        var category = await AddAsync(db, new TicketCategory { Name = "General", NormalizedName = "GENERAL" });
        var priority = await AddAsync(db, new TicketPriority { Name = "Low", NormalizedName = "LOW", SortOrder = 10 });
        var creator = await AddAsync(db, new User { Email = "creator@local.test", DisplayName = "Creator", PasswordHash = "x" });
        var agent = await AddAsync(db, new User { Email = "agent@local.test", DisplayName = "Agent", PasswordHash = "x" });
        var ticket = await AddAsync(db, new Ticket
        {
            CustomerId = customer.Id, Subject = "Cannot log in", Description = "D",
            CategoryId = category.Id, PriorityId = priority.Id, CreatedByUserId = creator.Id,
        });
        return (db, ticket, agent);
    }

    private static TicketEscalationDto NewEscalationDto(Guid ticketId, Guid? targetUserId, EscalationMilestone milestone = EscalationMilestone.Warning, SlaType slaType = SlaType.FirstResponse) =>
        new(Guid.NewGuid(), ticketId, slaType, milestone,
            targetUserId is null ? EscalationTargetRole.Administrator : EscalationTargetRole.Agent,
            targetUserId, DateTime.UtcNow, DateTime.UtcNow, WasUnassigned: targetUserId is null, Notes: null);

    [Fact]
    public async Task NotifyTicketAssignedAsync_creates_a_notification_for_the_assignee()
    {
        var (db, ticket, agent) = await SeedTicketAsync();
        var service = CreateService(db);

        await service.NotifyTicketAssignedAsync(ticket.Id, agent.Id);

        var notification = await db.Notifications.SingleAsync(n => n.TicketId == ticket.Id);
        Assert.Equal(NotificationEventType.TicketAssigned, notification.EventType);
        Assert.Equal(agent.Id, notification.RecipientUserId);
        Assert.Null(notification.SlaType);
        Assert.Null(notification.ReadAtUtc);
        Assert.Contains("Cannot log in", notification.Body);
    }

    [Fact]
    public async Task NotifyTicketAssignedAsync_is_a_noop_for_a_ticket_that_does_not_exist()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        await service.NotifyTicketAssignedAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Empty(await db.Notifications.ToListAsync());
    }

    [Fact]
    public async Task NotifySlaMilestoneAsync_skips_when_no_target_user_was_resolved()
    {
        var (db, ticket, _) = await SeedTicketAsync();
        var service = CreateService(db);
        var escalation = NewEscalationDto(ticket.Id, targetUserId: null);

        await service.NotifySlaMilestoneAsync(escalation);

        Assert.Empty(await db.Notifications.ToListAsync());
    }

    [Theory]
    [InlineData(EscalationMilestone.Warning, NotificationEventType.SlaWarning)]
    [InlineData(EscalationMilestone.Breach, NotificationEventType.SlaBreached)]
    public async Task NotifySlaMilestoneAsync_maps_milestone_to_the_matching_event_type(EscalationMilestone milestone, NotificationEventType expectedEventType)
    {
        var (db, ticket, agent) = await SeedTicketAsync();
        var service = CreateService(db);
        var escalation = NewEscalationDto(ticket.Id, agent.Id, milestone);

        await service.NotifySlaMilestoneAsync(escalation);

        var notification = await db.Notifications.SingleAsync(n => n.TicketId == ticket.Id);
        Assert.Equal(expectedEventType, notification.EventType);
        Assert.Equal(SlaType.FirstResponse, notification.SlaType);
        Assert.Equal(agent.Id, notification.RecipientUserId);
    }

    [Fact]
    public async Task NotifySlaMilestoneAsync_is_idempotent_for_the_same_escalation()
    {
        var (db, ticket, agent) = await SeedTicketAsync();
        var service = CreateService(db);
        var escalation = NewEscalationDto(ticket.Id, agent.Id);

        await service.NotifySlaMilestoneAsync(escalation);
        await service.NotifySlaMilestoneAsync(escalation);

        Assert.Single(await db.Notifications.Where(n => n.TicketId == ticket.Id).ToListAsync());
    }

    [Fact]
    public async Task ListForUserAsync_returns_only_the_callers_own_notifications()
    {
        var (db, ticket, agent) = await SeedTicketAsync();
        var otherAgent = await AddAsync(db, new User { Email = "other@local.test", DisplayName = "Other", PasswordHash = "x" });
        var service = CreateService(db);
        await service.NotifyTicketAssignedAsync(ticket.Id, agent.Id);
        await service.NotifyTicketAssignedAsync(ticket.Id, otherAgent.Id);

        var result = await service.ListForUserAsync(agent.Id, unreadOnly: false, page: 1, pageSize: 20);

        Assert.Equal(1, result.Total);
        Assert.Equal(ticket.Id, Assert.Single(result.Items).TicketId);
    }

    [Fact]
    public async Task ListForUserAsync_with_unreadOnly_excludes_already_read_notifications()
    {
        var (db, ticket, agent) = await SeedTicketAsync();
        var service = CreateService(db);
        await service.NotifyTicketAssignedAsync(ticket.Id, agent.Id);
        var notification = await db.Notifications.SingleAsync(n => n.RecipientUserId == agent.Id);
        await service.MarkReadAsync(notification.Id, agent.Id);

        var unread = await service.ListForUserAsync(agent.Id, unreadOnly: true, page: 1, pageSize: 20);
        var all = await service.ListForUserAsync(agent.Id, unreadOnly: false, page: 1, pageSize: 20);

        Assert.Equal(0, unread.Total);
        Assert.Equal(1, all.Total);
    }

    [Fact]
    public async Task MarkReadAsync_sets_ReadAtUtc_and_returns_true_for_the_owning_user()
    {
        var (db, ticket, agent) = await SeedTicketAsync();
        var service = CreateService(db);
        await service.NotifyTicketAssignedAsync(ticket.Id, agent.Id);
        var notification = await db.Notifications.SingleAsync(n => n.RecipientUserId == agent.Id);

        var result = await service.MarkReadAsync(notification.Id, agent.Id);

        Assert.True(result);
        Assert.NotNull((await db.Notifications.SingleAsync(n => n.Id == notification.Id)).ReadAtUtc);
    }

    [Fact]
    public async Task MarkReadAsync_returns_false_for_another_users_notification()
    {
        var (db, ticket, agent) = await SeedTicketAsync();
        var otherAgent = await AddAsync(db, new User { Email = "other@local.test", DisplayName = "Other", PasswordHash = "x" });
        var service = CreateService(db);
        await service.NotifyTicketAssignedAsync(ticket.Id, agent.Id);
        var notification = await db.Notifications.SingleAsync(n => n.RecipientUserId == agent.Id);

        var result = await service.MarkReadAsync(notification.Id, otherAgent.Id);

        Assert.False(result);
        Assert.Null((await db.Notifications.SingleAsync(n => n.Id == notification.Id)).ReadAtUtc);
    }

    [Fact]
    public async Task MarkReadAsync_returns_false_for_an_unknown_id()
    {
        var (db, _, agent) = await SeedTicketAsync();
        var service = CreateService(db);

        Assert.False(await service.MarkReadAsync(Guid.NewGuid(), agent.Id));
    }

    [Fact]
    public async Task MarkAllReadAsync_marks_every_unread_notification_and_returns_the_count()
    {
        var (db, ticket, agent) = await SeedTicketAsync();
        var otherTicket = await AddAsync(db, new Ticket
        {
            CustomerId = ticket.CustomerId, Subject = "Second", Description = "D",
            CategoryId = ticket.CategoryId, PriorityId = ticket.PriorityId, CreatedByUserId = ticket.CreatedByUserId,
        });
        var service = CreateService(db);
        await service.NotifyTicketAssignedAsync(ticket.Id, agent.Id);
        await service.NotifyTicketAssignedAsync(otherTicket.Id, agent.Id);

        var count = await service.MarkAllReadAsync(agent.Id);

        Assert.Equal(2, count);
        Assert.All(await db.Notifications.Where(n => n.RecipientUserId == agent.Id).ToListAsync(), n => Assert.NotNull(n.ReadAtUtc));
    }
}
