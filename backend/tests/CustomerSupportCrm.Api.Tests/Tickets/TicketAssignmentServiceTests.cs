using CustomerSupportCrm.Api.Tickets.Assignment;
using CustomerSupportCrm.Api.Tickets.History;
using CustomerSupportCrm.Domain.Customers;
using CustomerSupportCrm.Domain.Departments;
using CustomerSupportCrm.Domain.Tickets;
using CustomerSupportCrm.Domain.Users;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace CustomerSupportCrm.Api.Tests.Tickets;

/// <summary>
/// Story 23: <see cref="TicketAssignmentService.TryAutoAssignAsync"/> — the department-scoped,
/// lowest-workload-then-round-robin agent picker triggered from <c>TicketsService.UpdateAsync</c>
/// when an admin reclassifies a still-unassigned ticket into a non-default business category.
/// </summary>
public class TicketAssignmentServiceTests
{
    private static CrmDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static TicketAssignmentService CreateService(CrmDbContext db) =>
        new(db, new TicketHistoryService(db), NullLogger<TicketAssignmentService>.Instance);

    private static async Task<T> AddAsync<T>(CrmDbContext db, T entity) where T : class
    {
        db.Set<T>().Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }

    private static User NewAgent(string email, Guid? departmentId, bool isActive = true, int? maxActiveTickets = null) =>
        new() { Email = email, DisplayName = email, PasswordHash = "x", DepartmentId = departmentId, IsActive = isActive, MaxActiveTickets = maxActiveTickets };

    private static Ticket NewTicket(Customer customer, TicketCategory category, TicketPriority priority, Guid createdByUserId, Guid? assignedUserId = null, string status = "open") =>
        new()
        {
            CustomerId = customer.Id,
            Subject = "Subject",
            Description = "Description",
            CategoryId = category.Id,
            PriorityId = priority.Id,
            CreatedByUserId = createdByUserId,
            AssignedUserId = assignedUserId,
            Status = status,
        };

    private static async Task<(CrmDbContext db, Customer customer, TicketPriority priority, User creator, Department department)> SeedAsync()
    {
        var db = CreateDb();
        var customer = await AddAsync(db, new Customer { FirstName = "Jane", LastName = "Doe" });
        var priority = await AddAsync(db, new TicketPriority { Name = "Low", NormalizedName = "LOW", SortOrder = 10 });
        var department = await AddAsync(db, new Department { Name = "Finance", NormalizedName = "FINANCE" });
        var creator = await AddAsync(db, NewAgent("creator@local.test", departmentId: null));
        return (db, customer, priority, creator, department);
    }

    [Fact]
    public async Task DefaultCategory_DoesNotAssign()
    {
        var (db, customer, priority, creator, department) = await SeedAsync();
        var generalInquiry = await AddAsync(db, new TicketCategory { Name = "General Inquiry", NormalizedName = "GENERAL INQUIRY", DepartmentId = department.Id });
        await AddAsync(db, NewAgent("agent@local.test", department.Id));
        var ticket = await AddAsync(db, NewTicket(customer, generalInquiry, priority, creator.Id));
        var service = CreateService(db);

        var result = await service.TryAutoAssignAsync(ticket);

        Assert.False(result.Assigned);
        Assert.Equal("default_or_no_department", result.Reason);
        Assert.Null(ticket.AssignedUserId);
    }

    [Fact]
    public async Task CategoryWithNoDepartment_DoesNotAssign()
    {
        var (db, customer, priority, creator, _) = await SeedAsync();
        var category = await AddAsync(db, new TicketCategory { Name = "Feature Request", NormalizedName = "FEATURE REQUEST" });
        var ticket = await AddAsync(db, NewTicket(customer, category, priority, creator.Id));
        var service = CreateService(db);

        var result = await service.TryAutoAssignAsync(ticket);

        Assert.False(result.Assigned);
        Assert.Equal("default_or_no_department", result.Reason);
    }

    [Fact]
    public async Task BusinessCategory_AssignsLowestWorkloadAgentInDepartment()
    {
        // Mirrors the intake example: three Technical Support agents with different current
        // workloads — the one with the fewest active tickets wins, no tie-break needed.
        var (db, customer, priority, creator, department) = await SeedAsync();
        var category = await AddAsync(db, new TicketCategory { Name = "Technical Support", NormalizedName = "TECHNICAL SUPPORT", DepartmentId = department.Id });
        var busyAgent = await AddAsync(db, NewAgent("busy@local.test", department.Id));
        var mediumAgent = await AddAsync(db, NewAgent("medium@local.test", department.Id));
        var freeAgent = await AddAsync(db, NewAgent("free@local.test", department.Id));

        for (var i = 0; i < 6; i++) await AddAsync(db, NewTicket(customer, category, priority, creator.Id, busyAgent.Id));
        for (var i = 0; i < 4; i++) await AddAsync(db, NewTicket(customer, category, priority, creator.Id, mediumAgent.Id));
        for (var i = 0; i < 3; i++) await AddAsync(db, NewTicket(customer, category, priority, creator.Id, freeAgent.Id));

        var ticket = await AddAsync(db, NewTicket(customer, category, priority, creator.Id));
        var service = CreateService(db);

        var result = await service.TryAutoAssignAsync(ticket);

        Assert.True(result.Assigned);
        Assert.Equal(freeAgent.Id, result.AssignedUserId);
        Assert.Equal(freeAgent.Id, ticket.AssignedUserId);
    }

    [Fact]
    public async Task BusinessCategory_ExcludesAgentsFromOtherDepartments()
    {
        var (db, customer, priority, creator, financeDept) = await SeedAsync();
        var itDept = await AddAsync(db, new Department { Name = "IT", NormalizedName = "IT" });
        var category = await AddAsync(db, new TicketCategory { Name = "Billing", NormalizedName = "BILLING", DepartmentId = financeDept.Id });

        // The IT agent has zero workload — the lowest possible — but is in the wrong department and
        // must never be chosen, even with no Finance agent to compete against.
        await AddAsync(db, NewAgent("it-agent@local.test", itDept.Id));

        var ticket = await AddAsync(db, NewTicket(customer, category, priority, creator.Id));
        var service = CreateService(db);

        var result = await service.TryAutoAssignAsync(ticket);

        Assert.False(result.Assigned);
        Assert.Equal("no_eligible_agent", result.Reason);
        Assert.Null(ticket.AssignedUserId);
    }

    [Fact]
    public async Task BusinessCategory_ExcludesInactiveAgents()
    {
        var (db, customer, priority, creator, department) = await SeedAsync();
        var category = await AddAsync(db, new TicketCategory { Name = "Billing", NormalizedName = "BILLING", DepartmentId = department.Id });
        await AddAsync(db, NewAgent("inactive@local.test", department.Id, isActive: false));

        var ticket = await AddAsync(db, NewTicket(customer, category, priority, creator.Id));
        var service = CreateService(db);

        var result = await service.TryAutoAssignAsync(ticket);

        Assert.False(result.Assigned);
        Assert.Equal("no_eligible_agent", result.Reason);
    }

    [Fact]
    public async Task BusinessCategory_ExcludesCapacityExceededAgents()
    {
        var (db, customer, priority, creator, department) = await SeedAsync();
        var category = await AddAsync(db, new TicketCategory { Name = "Billing", NormalizedName = "BILLING", DepartmentId = department.Id });
        var cappedAgent = await AddAsync(db, NewAgent("capped@local.test", department.Id, maxActiveTickets: 2));
        var uncappedAgent = await AddAsync(db, NewAgent("uncapped@local.test", department.Id));

        // cappedAgent is already at their max (2) with a lower raw workload than uncappedAgent (3) —
        // without the capacity filter, cappedAgent would win on workload alone. The capped agent must
        // be excluded entirely, leaving the uncapped one as the only eligible candidate.
        for (var i = 0; i < 2; i++) await AddAsync(db, NewTicket(customer, category, priority, creator.Id, cappedAgent.Id));
        for (var i = 0; i < 3; i++) await AddAsync(db, NewTicket(customer, category, priority, creator.Id, uncappedAgent.Id));

        var ticket = await AddAsync(db, NewTicket(customer, category, priority, creator.Id));
        var service = CreateService(db);

        var result = await service.TryAutoAssignAsync(ticket);

        Assert.True(result.Assigned);
        Assert.Equal(uncappedAgent.Id, result.AssignedUserId);
    }

    [Fact]
    public async Task Tie_RoundRobinPicksNextAgentThenWrapsAround()
    {
        var (db, customer, priority, creator, department) = await SeedAsync();
        var category = await AddAsync(db, new TicketCategory { Name = "Billing", NormalizedName = "BILLING", DepartmentId = department.Id });
        var agents = new List<User>();
        for (var i = 0; i < 2; i++) agents.Add(await AddAsync(db, NewAgent($"agent-{i}@local.test", department.Id)));
        var ordered = agents.OrderBy(a => a.Id).ToList();
        var service = CreateService(db);

        var ticket1 = await AddAsync(db, NewTicket(customer, category, priority, creator.Id));
        var result1 = await service.TryAutoAssignAsync(ticket1);
        await db.SaveChangesAsync();

        var ticket2 = await AddAsync(db, NewTicket(customer, category, priority, creator.Id));
        var result2 = await service.TryAutoAssignAsync(ticket2);
        await db.SaveChangesAsync();

        var ticket3 = await AddAsync(db, NewTicket(customer, category, priority, creator.Id));
        var result3 = await service.TryAutoAssignAsync(ticket3);
        await db.SaveChangesAsync();

        // Both agents are tied at 0 workload for ticket1 — the cursor starts empty, so the lowest
        // sorting id wins first, then the cycle alternates: ordered[0] -> ordered[1] -> ordered[0].
        Assert.Equal(ordered[0].Id, result1.AssignedUserId);
        Assert.Equal(ordered[1].Id, result2.AssignedUserId);
        Assert.Equal(ordered[0].Id, result3.AssignedUserId);
    }

    [Fact]
    public async Task NoEligibleAgent_LeavesTicketUnassigned()
    {
        var (db, customer, priority, creator, department) = await SeedAsync();
        var category = await AddAsync(db, new TicketCategory { Name = "Billing", NormalizedName = "BILLING", DepartmentId = department.Id });
        var ticket = await AddAsync(db, NewTicket(customer, category, priority, creator.Id));
        var service = CreateService(db);

        var result = await service.TryAutoAssignAsync(ticket);

        Assert.False(result.Assigned);
        Assert.Null(ticket.AssignedUserId);
        Assert.Equal("open", ticket.Status);
    }

    [Fact]
    public async Task SuccessfulAssignment_WritesTicketHistoryEntry()
    {
        var (db, customer, priority, creator, department) = await SeedAsync();
        var category = await AddAsync(db, new TicketCategory { Name = "Billing", NormalizedName = "BILLING", DepartmentId = department.Id });
        var agent = await AddAsync(db, NewAgent("agent@local.test", department.Id));
        var ticket = await AddAsync(db, NewTicket(customer, category, priority, creator.Id));
        var service = CreateService(db);

        await service.TryAutoAssignAsync(ticket);
        await db.SaveChangesAsync();

        var historyRow = await db.TicketHistories.SingleAsync(h => h.TicketId == ticket.Id);
        Assert.Equal("Assigned", historyRow.EventType);
        Assert.Equal("AssignedUserId", historyRow.Field);
        Assert.Null(historyRow.PreviousValue);
        Assert.Equal(agent.Id.ToString(), historyRow.NewValue);
        Assert.Null(historyRow.PerformedByUserId);
    }

    [Fact]
    public async Task AlreadyAssignedTicket_IsNotReassigned()
    {
        var (db, customer, priority, creator, department) = await SeedAsync();
        var category = await AddAsync(db, new TicketCategory { Name = "Billing", NormalizedName = "BILLING", DepartmentId = department.Id });
        var originalAgent = await AddAsync(db, NewAgent("original@local.test", department.Id));
        await AddAsync(db, NewAgent("other@local.test", department.Id));
        var ticket = await AddAsync(db, NewTicket(customer, category, priority, creator.Id, assignedUserId: originalAgent.Id));
        var service = CreateService(db);

        var result = await service.TryAutoAssignAsync(ticket);

        Assert.False(result.Assigned);
        Assert.Equal("already_assigned", result.Reason);
        Assert.Equal(originalAgent.Id, ticket.AssignedUserId);
    }

    [Fact]
    public async Task ResolvedAndClosedTickets_DoNotCountTowardWorkload()
    {
        var (db, customer, priority, creator, department) = await SeedAsync();
        var category = await AddAsync(db, new TicketCategory { Name = "Billing", NormalizedName = "BILLING", DepartmentId = department.Id });
        var quietOnPaper = await AddAsync(db, NewAgent("quiet@local.test", department.Id));
        var trulyFree = await AddAsync(db, NewAgent("free@local.test", department.Id));

        // quietOnPaper has 5 tickets assigned, but all Resolved/Closed — 0 *active* workload, tying
        // with trulyFree's genuine 0. The tie-break (lowest sorting id) decides between them.
        for (var i = 0; i < 3; i++) await AddAsync(db, NewTicket(customer, category, priority, creator.Id, quietOnPaper.Id, status: "resolved"));
        for (var i = 0; i < 2; i++) await AddAsync(db, NewTicket(customer, category, priority, creator.Id, quietOnPaper.Id, status: "closed"));

        var ordered = new[] { quietOnPaper.Id, trulyFree.Id }.OrderBy(id => id).ToList();
        var ticket = await AddAsync(db, NewTicket(customer, category, priority, creator.Id));
        var service = CreateService(db);

        var result = await service.TryAutoAssignAsync(ticket);

        Assert.True(result.Assigned);
        Assert.Equal(ordered[0], result.AssignedUserId);
    }
}
