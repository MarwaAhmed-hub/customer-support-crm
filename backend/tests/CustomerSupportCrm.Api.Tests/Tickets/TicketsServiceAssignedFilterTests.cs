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

/// <summary>
/// Story 15 (Agent Dashboard): the <c>assignedUserId</c> filter on <see cref="TicketsService.ListAsync"/>
/// is the mechanism <c>GET /api/tickets/mine</c> relies on to scope results to the caller. These tests
/// exercise the service directly, the same way <c>Roles/RolesServiceTests.cs</c> does — no HTTP pipeline
/// needed since the filter is plain LINQ over an EF InMemory context.
/// </summary>
public class TicketsServiceAssignedFilterTests
{
    private static CrmDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<User> AddUserAsync(CrmDbContext db, string email)
    {
        var user = new User { Email = email, DisplayName = email, PasswordHash = "x" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    private static async Task<Ticket> AddTicketAsync(CrmDbContext db, Customer customer, TicketCategory category, TicketPriority priority, Guid createdByUserId, Guid? assignedUserId)
    {
        var ticket = new Ticket
        {
            CustomerId = customer.Id,
            Subject = "Subject",
            Description = "Description",
            CategoryId = category.Id,
            PriorityId = priority.Id,
            CreatedByUserId = createdByUserId,
            AssignedUserId = assignedUserId,
        };
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();
        return ticket;
    }

    /// <summary>Seeds one customer/category/priority and two agents, then four tickets: two assigned to agent A, one to agent B, one unassigned.</summary>
    private static async Task<(TicketsService service, User agentA, User agentB)> SeedAsync(CrmDbContext db)
    {
        var customer = new Customer { FirstName = "Jane", LastName = "Doe" };
        db.Customers.Add(customer);

        var category = new TicketCategory { Name = "Billing", NormalizedName = "BILLING" };
        db.TicketCategories.Add(category);

        var priority = new TicketPriority { Name = "Low", NormalizedName = "LOW", SortOrder = 10 };
        db.TicketPriorities.Add(priority);

        await db.SaveChangesAsync();

        var agentA = await AddUserAsync(db, "agent-a@local.test");
        var agentB = await AddUserAsync(db, "agent-b@local.test");
        var creator = await AddUserAsync(db, "creator@local.test");

        await AddTicketAsync(db, customer, category, priority, creator.Id, agentA.Id);
        await AddTicketAsync(db, customer, category, priority, creator.Id, agentA.Id);
        await AddTicketAsync(db, customer, category, priority, creator.Id, agentB.Id);
        await AddTicketAsync(db, customer, category, priority, creator.Id, assignedUserId: null);

        return (new TicketsService(db, new TicketHistoryService(db), new SlaService(db, NullLogger<SlaService>.Instance), new TicketAssignmentService(db, new TicketHistoryService(db), NullLogger<TicketAssignmentService>.Instance)), agentA, agentB);
    }

    [Fact]
    public async Task ListAsync_filters_to_only_the_given_agents_tickets()
    {
        await using var db = CreateDb();
        var (service, agentA, _) = await SeedAsync(db);

        var page = await service.ListAsync(
            customerId: null, categoryId: null, priorityId: null, assignedUserId: agentA.Id,
            status: null, isEscalated: null, search: null, page: 1, pageSize: 20);

        Assert.Equal(2, page.Total);
        Assert.All(page.Items, item => Assert.Equal(agentA.Id, item.AssignedUserId));
    }

    [Fact]
    public async Task ListAsync_with_a_different_agents_id_does_not_return_the_first_agents_tickets()
    {
        await using var db = CreateDb();
        var (service, agentA, agentB) = await SeedAsync(db);

        var page = await service.ListAsync(
            customerId: null, categoryId: null, priorityId: null, assignedUserId: agentB.Id,
            status: null, isEscalated: null, search: null, page: 1, pageSize: 20);

        Assert.Equal(1, page.Total);
        Assert.DoesNotContain(page.Items, item => item.AssignedUserId == agentA.Id);
    }

    [Fact]
    public async Task ListAsync_without_an_assignedUserId_returns_every_ticket_including_unassigned()
    {
        await using var db = CreateDb();
        var (service, _, _) = await SeedAsync(db);

        var page = await service.ListAsync(
            customerId: null, categoryId: null, priorityId: null, assignedUserId: null,
            status: null, isEscalated: null, search: null, page: 1, pageSize: 20);

        Assert.Equal(4, page.Total);
    }

    [Fact]
    public async Task ListAsync_with_unassignedOnly_returns_only_the_ticket_with_no_agent()
    {
        await using var db = CreateDb();
        var (service, _, _) = await SeedAsync(db);

        var page = await service.ListAsync(
            customerId: null, categoryId: null, priorityId: null, assignedUserId: null,
            status: null, isEscalated: null, search: null, page: 1, pageSize: 20, unassignedOnly: true);

        Assert.Equal(1, page.Total);
        Assert.Null(Assert.Single(page.Items).AssignedUserId);
    }
}
