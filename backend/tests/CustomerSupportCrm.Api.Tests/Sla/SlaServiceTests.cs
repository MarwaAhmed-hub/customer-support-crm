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

namespace CustomerSupportCrm.Api.Tests.Sla;

/// <summary>
/// Story 22: <see cref="SlaService"/> exercised directly against an EF InMemory context, the same way
/// <c>Tickets/TicketsServiceAssignedFilterTests.cs</c> exercises <c>TicketsService</c>.
/// </summary>
public class SlaServiceTests
{
    private static CrmDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static SlaService CreateService(CrmDbContext db) => new(db, NullLogger<SlaService>.Instance);

    private static async Task<(Customer Customer, TicketCategory Category, TicketPriority Priority, User Creator)> SeedTicketDependenciesAsync(CrmDbContext db)
    {
        var customer = new Customer { FirstName = "Jane", LastName = "Doe" };
        var category = new TicketCategory { Name = "General Inquiry", NormalizedName = "GENERAL INQUIRY" };
        var priority = new TicketPriority { Name = "Medium", NormalizedName = "MEDIUM", SortOrder = 20 };
        var creator = new User { Email = "creator@local.test", DisplayName = "Creator", PasswordHash = "x" };
        db.AddRange(customer, category, priority, creator);
        await db.SaveChangesAsync();
        return (customer, category, priority, creator);
    }

    private static async Task<Ticket> AddTicketAsync(
        CrmDbContext db, Customer customer, TicketCategory category, TicketPriority priority, Guid creatorId,
        DateTimeOffset createdAt, Guid? assignedUserId = null)
    {
        var ticket = new Ticket
        {
            CustomerId = customer.Id,
            Subject = "Subject",
            Description = "Description",
            CategoryId = category.Id,
            PriorityId = priority.Id,
            CreatedByUserId = creatorId,
            CreatedAt = createdAt,
            UpdatedAt = createdAt,
            AssignedUserId = assignedUserId,
        };
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();
        return ticket;
    }

    private static async Task<SlaPolicy> AddDefaultPolicyAsync(CrmDbContext db, int firstResponseMinutes = 30, int resolutionMinutes = 240)
    {
        var policy = new SlaPolicy { PriorityId = null, Name = "Default SLA", FirstResponseMinutes = firstResponseMinutes, ResolutionMinutes = resolutionMinutes };
        db.SlaPolicies.Add(policy);
        await db.SaveChangesAsync();
        return policy;
    }

    [Fact]
    public async Task StartForTicketAsync_uses_createdAt_as_the_start_and_computes_due_times_from_the_policy()
    {
        await using var db = CreateDb();
        var (customer, category, priority, creator) = await SeedTicketDependenciesAsync(db);
        await AddDefaultPolicyAsync(db, firstResponseMinutes: 30, resolutionMinutes: 240);
        var createdAt = new DateTimeOffset(2026, 1, 1, 10, 0, 0, TimeSpan.Zero);
        var ticket = await AddTicketAsync(db, customer, category, priority, creator.Id, createdAt);
        var service = CreateService(db);

        await service.StartForTicketAsync(ticket.Id);

        var sla = await db.TicketSlas.SingleAsync(s => s.TicketId == ticket.Id);
        Assert.Equal(createdAt, sla.StartedAt);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 10, 30, 0, TimeSpan.Zero), sla.FirstResponseDueAt);
        Assert.Equal(new DateTimeOffset(2026, 1, 1, 14, 0, 0, TimeSpan.Zero), sla.ResolutionDueAt);
        Assert.Equal(SlaStatuses.Running, sla.FirstResponseStatus);
        Assert.Equal(SlaStatuses.Running, sla.ResolutionStatus);
    }

    [Fact]
    public async Task StartForTicketAsync_called_twice_for_the_same_ticket_creates_only_one_row()
    {
        await using var db = CreateDb();
        var (customer, category, priority, creator) = await SeedTicketDependenciesAsync(db);
        await AddDefaultPolicyAsync(db);
        var ticket = await AddTicketAsync(db, customer, category, priority, creator.Id, DateTimeOffset.UtcNow);
        var service = CreateService(db);

        await service.StartForTicketAsync(ticket.Id);
        await service.StartForTicketAsync(ticket.Id);

        Assert.Equal(1, await db.TicketSlas.CountAsync(s => s.TicketId == ticket.Id));
    }

    [Fact]
    public async Task StartForTicketAsync_starts_sla_for_an_unassigned_general_inquiry_ticket()
    {
        await using var db = CreateDb();
        var (customer, category, priority, creator) = await SeedTicketDependenciesAsync(db);
        await AddDefaultPolicyAsync(db);
        var ticket = await AddTicketAsync(db, customer, category, priority, creator.Id, DateTimeOffset.UtcNow, assignedUserId: null);
        var service = CreateService(db);

        await service.StartForTicketAsync(ticket.Id);

        Assert.True(await db.TicketSlas.AnyAsync(s => s.TicketId == ticket.Id));
    }

    [Fact]
    public async Task MarkFirstResponseAsync_before_the_due_time_sets_met()
    {
        await using var db = CreateDb();
        var (customer, category, priority, creator) = await SeedTicketDependenciesAsync(db);
        await AddDefaultPolicyAsync(db, firstResponseMinutes: 30);
        var createdAt = DateTimeOffset.UtcNow;
        var ticket = await AddTicketAsync(db, customer, category, priority, creator.Id, createdAt);
        var service = CreateService(db);
        await service.StartForTicketAsync(ticket.Id);

        var respondedAt = createdAt.AddMinutes(10);
        await service.MarkFirstResponseAsync(ticket.Id, respondedAt);

        var sla = await db.TicketSlas.SingleAsync(s => s.TicketId == ticket.Id);
        Assert.Equal(SlaStatuses.Met, sla.FirstResponseStatus);
        Assert.Equal(respondedAt, sla.FirstResponseAt);
        Assert.Null(sla.FirstResponseBreachedAt);
    }

    [Fact]
    public async Task MarkFirstResponseAsync_after_the_due_time_sets_breached()
    {
        await using var db = CreateDb();
        var (customer, category, priority, creator) = await SeedTicketDependenciesAsync(db);
        await AddDefaultPolicyAsync(db, firstResponseMinutes: 30);
        var createdAt = DateTimeOffset.UtcNow;
        var ticket = await AddTicketAsync(db, customer, category, priority, creator.Id, createdAt);
        var service = CreateService(db);
        await service.StartForTicketAsync(ticket.Id);

        var respondedAt = createdAt.AddMinutes(45);
        await service.MarkFirstResponseAsync(ticket.Id, respondedAt);

        var sla = await db.TicketSlas.SingleAsync(s => s.TicketId == ticket.Id);
        Assert.Equal(SlaStatuses.Breached, sla.FirstResponseStatus);
        Assert.Equal(respondedAt, sla.FirstResponseAt);
        Assert.Equal(respondedAt, sla.FirstResponseBreachedAt);
    }

    [Fact]
    public async Task MarkResolvedIfApplicableAsync_transitioning_to_resolved_before_the_due_time_sets_met()
    {
        await using var db = CreateDb();
        var (customer, category, priority, creator) = await SeedTicketDependenciesAsync(db);
        await AddDefaultPolicyAsync(db, resolutionMinutes: 240);
        var createdAt = DateTimeOffset.UtcNow;
        var ticket = await AddTicketAsync(db, customer, category, priority, creator.Id, createdAt);
        var service = CreateService(db);
        await service.StartForTicketAsync(ticket.Id);

        var resolvedAt = createdAt.AddMinutes(60);
        await service.MarkResolvedIfApplicableAsync(ticket.Id, TicketStatuses.Resolved, resolvedAt);

        var sla = await db.TicketSlas.SingleAsync(s => s.TicketId == ticket.Id);
        Assert.Equal(SlaStatuses.Met, sla.ResolutionStatus);
        Assert.Equal(resolvedAt, sla.ResolvedAt);
    }

    [Fact]
    public async Task MarkResolvedIfApplicableAsync_transitioning_to_closed_after_the_due_time_sets_breached()
    {
        await using var db = CreateDb();
        var (customer, category, priority, creator) = await SeedTicketDependenciesAsync(db);
        await AddDefaultPolicyAsync(db, resolutionMinutes: 240);
        var createdAt = DateTimeOffset.UtcNow;
        var ticket = await AddTicketAsync(db, customer, category, priority, creator.Id, createdAt);
        var service = CreateService(db);
        await service.StartForTicketAsync(ticket.Id);

        var closedAt = createdAt.AddMinutes(300);
        await service.MarkResolvedIfApplicableAsync(ticket.Id, TicketStatuses.Closed, closedAt);

        var sla = await db.TicketSlas.SingleAsync(s => s.TicketId == ticket.Id);
        Assert.Equal(SlaStatuses.Breached, sla.ResolutionStatus);
        Assert.Equal(closedAt, sla.ResolutionBreachedAt);
    }

    [Fact]
    public async Task MarkResolvedIfApplicableAsync_for_a_non_terminal_status_is_a_no_op()
    {
        await using var db = CreateDb();
        var (customer, category, priority, creator) = await SeedTicketDependenciesAsync(db);
        await AddDefaultPolicyAsync(db);
        var ticket = await AddTicketAsync(db, customer, category, priority, creator.Id, DateTimeOffset.UtcNow);
        var service = CreateService(db);
        await service.StartForTicketAsync(ticket.Id);

        await service.MarkResolvedIfApplicableAsync(ticket.Id, TicketStatuses.InProgress, DateTimeOffset.UtcNow);

        var sla = await db.TicketSlas.SingleAsync(s => s.TicketId == ticket.Id);
        Assert.Equal(SlaStatuses.Running, sla.ResolutionStatus);
        Assert.Null(sla.ResolvedAt);
    }

    [Fact]
    public async Task EvaluateBreaches_reports_breached_for_a_still_running_clock_past_its_due_time_without_writing_anything()
    {
        await using var db = CreateDb();
        var (customer, category, priority, creator) = await SeedTicketDependenciesAsync(db);
        var policy = await AddDefaultPolicyAsync(db, firstResponseMinutes: 30, resolutionMinutes: 240);
        var createdAt = DateTimeOffset.UtcNow.AddHours(-5);
        var ticket = await AddTicketAsync(db, customer, category, priority, creator.Id, createdAt);
        db.TicketSlas.Add(new TicketSla
        {
            TicketId = ticket.Id,
            SlaPolicyId = policy.Id,
            StartedAt = createdAt,
            FirstResponseDueAt = createdAt.AddMinutes(30),
            ResolutionDueAt = createdAt.AddMinutes(240),
        });
        await db.SaveChangesAsync();
        var service = CreateService(db);
        var row = await db.TicketSlas.AsNoTracking().SingleAsync(s => s.TicketId == ticket.Id);

        var snapshot = service.EvaluateBreaches(row, DateTimeOffset.UtcNow);

        Assert.Equal(SlaStatuses.Breached, snapshot.FirstResponseStatus);
        Assert.Equal(SlaStatuses.Breached, snapshot.ResolutionStatus);
        // Pure — the persisted row must be untouched.
        var persisted = await db.TicketSlas.AsNoTracking().SingleAsync(s => s.TicketId == ticket.Id);
        Assert.Equal(SlaStatuses.Running, persisted.FirstResponseStatus);
        Assert.Equal(SlaStatuses.Running, persisted.ResolutionStatus);
    }

    [Fact]
    public async Task GetForTicketAsync_persists_a_breach_it_discovers_via_EvaluateBreaches()
    {
        await using var db = CreateDb();
        var (customer, category, priority, creator) = await SeedTicketDependenciesAsync(db);
        var policy = await AddDefaultPolicyAsync(db, firstResponseMinutes: 30, resolutionMinutes: 240);
        var createdAt = DateTimeOffset.UtcNow.AddHours(-1);
        var ticket = await AddTicketAsync(db, customer, category, priority, creator.Id, createdAt);
        db.TicketSlas.Add(new TicketSla
        {
            TicketId = ticket.Id,
            SlaPolicyId = policy.Id,
            StartedAt = createdAt,
            FirstResponseDueAt = createdAt.AddMinutes(30),
            ResolutionDueAt = createdAt.AddMinutes(240),
        });
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var snapshot = await service.GetForTicketAsync(ticket.Id);

        Assert.NotNull(snapshot);
        Assert.Equal(SlaStatuses.Breached, snapshot!.FirstResponseStatus);
        Assert.Equal(SlaStatuses.Running, snapshot.ResolutionStatus);

        var persisted = await db.TicketSlas.AsNoTracking().SingleAsync(s => s.TicketId == ticket.Id);
        Assert.Equal(SlaStatuses.Breached, persisted.FirstResponseStatus);
        Assert.NotNull(persisted.FirstResponseBreachedAt);
    }

    [Fact]
    public async Task MarkFirstResponseAsync_does_not_affect_ResolutionStatus_and_vice_versa()
    {
        await using var db = CreateDb();
        var (customer, category, priority, creator) = await SeedTicketDependenciesAsync(db);
        await AddDefaultPolicyAsync(db, firstResponseMinutes: 30, resolutionMinutes: 240);
        var createdAt = DateTimeOffset.UtcNow;
        var ticket = await AddTicketAsync(db, customer, category, priority, creator.Id, createdAt);
        var service = CreateService(db);
        await service.StartForTicketAsync(ticket.Id);

        await service.MarkFirstResponseAsync(ticket.Id, createdAt.AddMinutes(5));
        var afterFirstResponse = await db.TicketSlas.SingleAsync(s => s.TicketId == ticket.Id);
        Assert.Equal(SlaStatuses.Met, afterFirstResponse.FirstResponseStatus);
        Assert.Equal(SlaStatuses.Running, afterFirstResponse.ResolutionStatus);

        await service.MarkResolvedIfApplicableAsync(ticket.Id, TicketStatuses.Resolved, createdAt.AddMinutes(10));
        var afterResolve = await db.TicketSlas.SingleAsync(s => s.TicketId == ticket.Id);
        Assert.Equal(SlaStatuses.Met, afterResolve.FirstResponseStatus);
        Assert.Equal(SlaStatuses.Met, afterResolve.ResolutionStatus);
    }

    [Fact]
    public async Task Assigning_a_ticket_after_sla_started_does_not_change_its_due_times()
    {
        await using var db = CreateDb();
        var (customer, category, priority, creator) = await SeedTicketDependenciesAsync(db);
        await AddDefaultPolicyAsync(db);
        var ticket = await AddTicketAsync(db, customer, category, priority, creator.Id, DateTimeOffset.UtcNow);
        var service = CreateService(db);
        await service.StartForTicketAsync(ticket.Id);
        var before = await db.TicketSlas.AsNoTracking().SingleAsync(s => s.TicketId == ticket.Id);

        // Story 12's assignment mutation, done directly here rather than via TicketsService — the
        // point of this test is that SlaService has no assignment-change hook to call in the first
        // place, not that TicketsService happens not to call one.
        var agent = new User { Email = "agent@local.test", DisplayName = "Agent", PasswordHash = "x" };
        db.Users.Add(agent);
        var tracked = await db.Tickets.SingleAsync(t => t.Id == ticket.Id);
        tracked.AssignedUserId = agent.Id;
        await db.SaveChangesAsync();

        var after = await db.TicketSlas.AsNoTracking().SingleAsync(s => s.TicketId == ticket.Id);
        Assert.Equal(before.StartedAt, after.StartedAt);
        Assert.Equal(before.FirstResponseDueAt, after.FirstResponseDueAt);
        Assert.Equal(before.ResolutionDueAt, after.ResolutionDueAt);
    }

    [Fact]
    public async Task Changing_a_tickets_category_after_sla_started_does_not_change_its_due_times()
    {
        await using var db = CreateDb();
        var (customer, category, priority, creator) = await SeedTicketDependenciesAsync(db);
        await AddDefaultPolicyAsync(db);
        var ticket = await AddTicketAsync(db, customer, category, priority, creator.Id, DateTimeOffset.UtcNow);
        var service = CreateService(db);
        await service.StartForTicketAsync(ticket.Id);
        var before = await db.TicketSlas.AsNoTracking().SingleAsync(s => s.TicketId == ticket.Id);

        var otherCategory = new TicketCategory { Name = "Billing", NormalizedName = "BILLING" };
        db.TicketCategories.Add(otherCategory);
        var tracked = await db.Tickets.SingleAsync(t => t.Id == ticket.Id);
        tracked.CategoryId = otherCategory.Id;
        await db.SaveChangesAsync();

        var after = await db.TicketSlas.AsNoTracking().SingleAsync(s => s.TicketId == ticket.Id);
        Assert.Equal(before.StartedAt, after.StartedAt);
        Assert.Equal(before.FirstResponseDueAt, after.FirstResponseDueAt);
        Assert.Equal(before.ResolutionDueAt, after.ResolutionDueAt);
    }

    [Fact]
    public async Task StartForTicketAsync_prefers_a_priority_specific_active_policy_over_the_default()
    {
        await using var db = CreateDb();
        var (customer, category, priority, creator) = await SeedTicketDependenciesAsync(db);
        await AddDefaultPolicyAsync(db, firstResponseMinutes: 30, resolutionMinutes: 240);
        var priorityPolicy = new SlaPolicy { PriorityId = priority.Id, Name = "Medium priority SLA", FirstResponseMinutes = 10, ResolutionMinutes = 60 };
        db.SlaPolicies.Add(priorityPolicy);
        await db.SaveChangesAsync();
        var createdAt = DateTimeOffset.UtcNow;
        var ticket = await AddTicketAsync(db, customer, category, priority, creator.Id, createdAt);
        var service = CreateService(db);

        await service.StartForTicketAsync(ticket.Id);

        var sla = await db.TicketSlas.SingleAsync(s => s.TicketId == ticket.Id);
        Assert.Equal(priorityPolicy.Id, sla.SlaPolicyId);
        Assert.Equal(createdAt.AddMinutes(10), sla.FirstResponseDueAt);
    }

    /// <summary>Correction: rather than throwing and asking every caller to remember to catch it, a missing policy is logged and the ticket is simply left without an SLA row — see the remarks on <see cref="ISlaService.StartForTicketAsync"/>.</summary>
    [Fact]
    public async Task StartForTicketAsync_with_no_active_policy_creates_no_row_and_does_not_throw()
    {
        await using var db = CreateDb();
        var (customer, category, priority, creator) = await SeedTicketDependenciesAsync(db);
        var ticket = await AddTicketAsync(db, customer, category, priority, creator.Id, DateTimeOffset.UtcNow);
        var service = CreateService(db);

        await service.StartForTicketAsync(ticket.Id);

        Assert.False(await db.TicketSlas.AnyAsync(s => s.TicketId == ticket.Id));
    }

    [Fact]
    public async Task TicketsService_CreateAsync_still_succeeds_when_no_sla_policy_is_active()
    {
        await using var db = CreateDb();
        var (customer, category, priority, creator) = await SeedTicketDependenciesAsync(db);
        var ticketsService = new TicketsService(db, new TicketHistoryService(db), CreateService(db),
            new TicketAssignmentService(db, new TicketHistoryService(db), NullLogger<TicketAssignmentService>.Instance));

        var result = await ticketsService.CreateAsync(
            new CreateTicketRequest(customer.Id, "Subject", "Description", category.Id, priority.Id), creator.Id);

        Assert.Equal(TicketOperationOutcome.Success, result.Outcome);
        Assert.Null(result.Ticket!.Sla);
    }
}
