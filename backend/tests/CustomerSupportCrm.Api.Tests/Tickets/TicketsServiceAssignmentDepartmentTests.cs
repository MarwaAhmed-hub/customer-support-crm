using CustomerSupportCrm.Api.Sla;
using CustomerSupportCrm.Api.Tickets.Assignment;
using CustomerSupportCrm.Api.Tickets.History;
using CustomerSupportCrm.Api.Tickets.Tickets;
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
/// Enforces the Category → Department → User assignment scoping rule directly in
/// <see cref="TicketsService.UpdateAssignmentAsync"/> — not just as a frontend picker filter — so a
/// caller hitting <c>PUT /api/tickets/{id}/assignment</c> directly cannot cross departments either.
/// A category with no department imposes no restriction at all (any active user is eligible).
/// </summary>
public class TicketsServiceAssignmentDepartmentTests
{
    private static CrmDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static TicketsService CreateService(CrmDbContext db) =>
        new(db, new TicketHistoryService(db), new SlaService(db, NullLogger<SlaService>.Instance),
            new TicketAssignmentService(db, new TicketHistoryService(db), NullLogger<TicketAssignmentService>.Instance));

    private static async Task<User> AddUserAsync(CrmDbContext db, string email, Guid? departmentId) =>
        await AddEntityAsync(db, new User { Email = email, DisplayName = email, PasswordHash = "x", DepartmentId = departmentId });

    private static async Task<T> AddEntityAsync<T>(CrmDbContext db, T entity) where T : class
    {
        db.Set<T>().Add(entity);
        await db.SaveChangesAsync();
        return entity;
    }

    private static async Task<Ticket> AddTicketAsync(CrmDbContext db, Customer customer, TicketCategory category, TicketPriority priority, Guid createdByUserId) =>
        await AddEntityAsync(db, new Ticket
        {
            CustomerId = customer.Id,
            Subject = "Subject",
            Description = "Description",
            CategoryId = category.Id,
            PriorityId = priority.Id,
            CreatedByUserId = createdByUserId,
        });

    private static async Task<(CrmDbContext db, TicketsService service, Customer customer, TicketPriority priority, User creator, Department finance, Department it)> SeedAsync()
    {
        var db = CreateDb();
        var customer = await AddEntityAsync(db, new Customer { FirstName = "Jane", LastName = "Doe" });
        var priority = await AddEntityAsync(db, new TicketPriority { Name = "Low", NormalizedName = "LOW", SortOrder = 10 });
        var finance = await AddEntityAsync(db, new Department { Name = "Finance", NormalizedName = "FINANCE" });
        var it = await AddEntityAsync(db, new Department { Name = "IT", NormalizedName = "IT" });
        var creator = await AddUserAsync(db, "creator@local.test", departmentId: null);
        return (db, CreateService(db), customer, priority, creator, finance, it);
    }

    [Fact]
    public async Task Assign_succeeds_for_a_user_in_the_categorys_department()
    {
        var (db, service, customer, priority, creator, finance, _) = await SeedAsync();
        var category = await AddEntityAsync(db, new TicketCategory { Name = "Billing", NormalizedName = "BILLING", DepartmentId = finance.Id });
        var ticket = await AddTicketAsync(db, customer, category, priority, creator.Id);
        var financeAgent = await AddUserAsync(db, "finance-agent@local.test", finance.Id);

        var result = await service.UpdateAssignmentAsync(ticket.Id, financeAgent.Id, creator.Id);

        Assert.Equal(TicketOperationOutcome.Success, result.Outcome);
        Assert.Equal(financeAgent.Id, result.Ticket!.AssignedUserId);
    }

    [Fact]
    public async Task Assign_is_rejected_for_a_user_in_a_different_department()
    {
        var (db, service, customer, priority, creator, finance, it) = await SeedAsync();
        var category = await AddEntityAsync(db, new TicketCategory { Name = "Billing", NormalizedName = "BILLING", DepartmentId = finance.Id });
        var ticket = await AddTicketAsync(db, customer, category, priority, creator.Id);
        var itAgent = await AddUserAsync(db, "it-agent@local.test", it.Id);

        var result = await service.UpdateAssignmentAsync(ticket.Id, itAgent.Id, creator.Id);

        Assert.Equal(TicketOperationOutcome.AssignedUserOutsideDepartment, result.Outcome);
    }

    [Fact]
    public async Task Assign_is_rejected_for_a_user_with_no_department_when_the_category_requires_one()
    {
        var (db, service, customer, priority, creator, finance, _) = await SeedAsync();
        var category = await AddEntityAsync(db, new TicketCategory { Name = "Billing", NormalizedName = "BILLING", DepartmentId = finance.Id });
        var ticket = await AddTicketAsync(db, customer, category, priority, creator.Id);
        var deptlessAgent = await AddUserAsync(db, "no-department-agent@local.test", departmentId: null);

        var result = await service.UpdateAssignmentAsync(ticket.Id, deptlessAgent.Id, creator.Id);

        Assert.Equal(TicketOperationOutcome.AssignedUserOutsideDepartment, result.Outcome);
    }

    [Fact]
    public async Task Assign_allows_any_active_user_when_the_category_has_no_department()
    {
        var (db, service, customer, priority, creator, _, it) = await SeedAsync();
        var category = await AddEntityAsync(db, new TicketCategory { Name = "General", NormalizedName = "GENERAL" });
        var ticket = await AddTicketAsync(db, customer, category, priority, creator.Id);
        var itAgent = await AddUserAsync(db, "it-agent@local.test", it.Id);

        var result = await service.UpdateAssignmentAsync(ticket.Id, itAgent.Id, creator.Id);

        Assert.Equal(TicketOperationOutcome.Success, result.Outcome);
    }

    [Fact]
    public async Task Unassign_always_succeeds_regardless_of_department_scoping()
    {
        var (db, service, customer, priority, creator, finance, it) = await SeedAsync();
        var category = await AddEntityAsync(db, new TicketCategory { Name = "Billing", NormalizedName = "BILLING", DepartmentId = finance.Id });
        var ticket = await AddTicketAsync(db, customer, category, priority, creator.Id);
        var itAgent = await AddUserAsync(db, "it-agent@local.test", it.Id);
        ticket.AssignedUserId = itAgent.Id;
        await db.SaveChangesAsync();

        var result = await service.UpdateAssignmentAsync(ticket.Id, assignedUserId: null, creator.Id);

        Assert.Equal(TicketOperationOutcome.Success, result.Outcome);
        Assert.Null(result.Ticket!.AssignedUserId);
    }

    [Fact]
    public async Task Assign_still_returns_InvalidAssignedUser_for_an_inactive_user_before_the_department_check_runs()
    {
        var (db, service, customer, priority, creator, finance, _) = await SeedAsync();
        var category = await AddEntityAsync(db, new TicketCategory { Name = "Billing", NormalizedName = "BILLING", DepartmentId = finance.Id });
        var ticket = await AddTicketAsync(db, customer, category, priority, creator.Id);
        var inactiveFinanceAgent = await AddUserAsync(db, "inactive-finance-agent@local.test", finance.Id);
        inactiveFinanceAgent.IsActive = false;
        await db.SaveChangesAsync();

        var result = await service.UpdateAssignmentAsync(ticket.Id, inactiveFinanceAgent.Id, creator.Id);

        Assert.Equal(TicketOperationOutcome.InvalidAssignedUser, result.Outcome);
    }
}
