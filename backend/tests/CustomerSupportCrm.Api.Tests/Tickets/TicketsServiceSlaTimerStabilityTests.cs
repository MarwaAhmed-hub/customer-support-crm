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

namespace CustomerSupportCrm.Api.Tests.Tickets;

/// <summary>
/// Story 24, Acceptance 5: SLA timers are anchored to <c>TicketSla.StartedAt</c> (== the ticket's own
/// <c>CreatedAt</c>) and must never be reset by assignment, reassignment, or a category change — the
/// escalation evaluator (Story 24) depends on those due-at values staying stable for the ticket's
/// entire lifetime, or a late assign/reassign/re-categorize would silently un-breach an already-late
/// ticket.
/// </summary>
public class TicketsServiceSlaTimerStabilityTests
{
    private static CrmDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static TicketsService CreateService(CrmDbContext db) =>
        new(db, new TicketHistoryService(db), new SlaService(db, NullLogger<SlaService>.Instance),
            new TicketAssignmentService(db, new TicketHistoryService(db), NullLogger<TicketAssignmentService>.Instance));

    private static async Task<T> AddAsync<T>(CrmDbContext db, T entity) where T : class
    {
        db.Set<T>().Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }

    private static async Task<(CrmDbContext db, TicketsService service, Customer customer, TicketCategory categoryA, TicketCategory categoryB, TicketPriority priority, User creator, User agentA, User agentB)> SeedAsync()
    {
        var db = CreateDb();
        await AddAsync(db, new SlaPolicy { PriorityId = null, Name = "Default", FirstResponseMinutes = 30, ResolutionMinutes = 240 });
        var customer = await AddAsync(db, new Customer { FirstName = "Jane", LastName = "Doe" });
        var categoryA = await AddAsync(db, new TicketCategory { Name = "General Inquiry", NormalizedName = "GENERAL INQUIRY" });
        var categoryB = await AddAsync(db, new TicketCategory { Name = "Billing", NormalizedName = "BILLING" });
        var priority = await AddAsync(db, new TicketPriority { Name = "Low", NormalizedName = "LOW", SortOrder = 10 });
        var creator = await AddAsync(db, new User { Email = "creator@local.test", DisplayName = "Creator", PasswordHash = "x" });
        var agentA = await AddAsync(db, new User { Email = "agent-a@local.test", DisplayName = "Agent A", PasswordHash = "x" });
        var agentB = await AddAsync(db, new User { Email = "agent-b@local.test", DisplayName = "Agent B", PasswordHash = "x" });
        return (db, CreateService(db), customer, categoryA, categoryB, priority, creator, agentA, agentB);
    }

    [Fact]
    public async Task Assigning_a_ticket_does_not_change_its_sla_due_at_values()
    {
        var (db, service, customer, categoryA, _, priority, creator, agentA, _) = await SeedAsync();
        var created = await service.CreateAsync(new CreateTicketRequest(customer.Id, "Subject", "Description", categoryA.Id, priority.Id), creator.Id);
        var ticketId = created.Ticket!.Id;
        var before = await db.TicketSlas.AsNoTracking().SingleAsync(s => s.TicketId == ticketId);

        await service.UpdateAssignmentAsync(ticketId, agentA.Id, creator.Id);

        var after = await db.TicketSlas.AsNoTracking().SingleAsync(s => s.TicketId == ticketId);
        Assert.Equal(before.StartedAt, after.StartedAt);
        Assert.Equal(before.FirstResponseDueAt, after.FirstResponseDueAt);
        Assert.Equal(before.ResolutionDueAt, after.ResolutionDueAt);
    }

    [Fact]
    public async Task Reassigning_a_ticket_does_not_change_its_sla_due_at_values()
    {
        var (db, service, customer, categoryA, _, priority, creator, agentA, agentB) = await SeedAsync();
        var created = await service.CreateAsync(new CreateTicketRequest(customer.Id, "Subject", "Description", categoryA.Id, priority.Id), creator.Id);
        var ticketId = created.Ticket!.Id;
        await service.UpdateAssignmentAsync(ticketId, agentA.Id, creator.Id);
        var before = await db.TicketSlas.AsNoTracking().SingleAsync(s => s.TicketId == ticketId);

        await service.UpdateAssignmentAsync(ticketId, agentB.Id, creator.Id);

        var after = await db.TicketSlas.AsNoTracking().SingleAsync(s => s.TicketId == ticketId);
        Assert.Equal(before.StartedAt, after.StartedAt);
        Assert.Equal(before.FirstResponseDueAt, after.FirstResponseDueAt);
        Assert.Equal(before.ResolutionDueAt, after.ResolutionDueAt);
    }

    [Fact]
    public async Task Changing_category_does_not_change_its_sla_due_at_values()
    {
        var (db, service, customer, categoryA, categoryB, priority, creator, _, _) = await SeedAsync();
        var created = await service.CreateAsync(new CreateTicketRequest(customer.Id, "Subject", "Description", categoryA.Id, priority.Id), creator.Id);
        var ticketId = created.Ticket!.Id;
        var before = await db.TicketSlas.AsNoTracking().SingleAsync(s => s.TicketId == ticketId);

        await service.UpdateAsync(ticketId, new UpdateTicketRequest("Subject", "Description", categoryB.Id, priority.Id), creator.Id);

        var after = await db.TicketSlas.AsNoTracking().SingleAsync(s => s.TicketId == ticketId);
        Assert.Equal(before.StartedAt, after.StartedAt);
        Assert.Equal(before.FirstResponseDueAt, after.FirstResponseDueAt);
        Assert.Equal(before.ResolutionDueAt, after.ResolutionDueAt);
    }
}
