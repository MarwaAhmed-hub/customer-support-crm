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
/// Story 23: integration coverage for the actual trigger point — <see cref="TicketsService.UpdateAsync"/>
/// invoking <see cref="TicketAssignmentService"/> when an admin reclassifies a still-unassigned ticket.
/// <see cref="TicketAssignmentServiceTests"/> covers the assignment algorithm itself in isolation; these
/// tests only confirm the hook fires (or doesn't) at the right moments and that the whole edit — category
/// change, assignment, and history — commits together.
/// </summary>
public class TicketsServiceCategoryUpdateAssignmentTests
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

    private static async Task<(CrmDbContext db, Customer customer, TicketPriority priority, User creator,
        Department department, TicketCategory generalInquiry, TicketCategory billing, User agent)> SeedAsync()
    {
        var db = CreateDb();
        var customer = await AddAsync(db, new Customer { FirstName = "Jane", LastName = "Doe" });
        var priority = await AddAsync(db, new TicketPriority { Name = "Low", NormalizedName = "LOW", SortOrder = 10 });
        var department = await AddAsync(db, new Department { Name = "Finance", NormalizedName = "FINANCE" });
        var generalInquiry = await AddAsync(db, new TicketCategory { Name = "General Inquiry", NormalizedName = "GENERAL INQUIRY" });
        var billing = await AddAsync(db, new TicketCategory { Name = "Billing", NormalizedName = "BILLING", DepartmentId = department.Id });
        var creator = await AddAsync(db, new User { Email = "creator@local.test", DisplayName = "Creator", PasswordHash = "x" });
        var agent = await AddAsync(db, new User { Email = "agent@local.test", DisplayName = "Agent", PasswordHash = "x", DepartmentId = department.Id });
        return (db, customer, priority, creator, department, generalInquiry, billing, agent);
    }

    private static Ticket NewTicket(Customer customer, TicketCategory category, TicketPriority priority, Guid createdByUserId, Guid? assignedUserId = null) =>
        new()
        {
            CustomerId = customer.Id,
            Subject = "Subject",
            Description = "Description",
            CategoryId = category.Id,
            PriorityId = priority.Id,
            CreatedByUserId = createdByUserId,
            AssignedUserId = assignedUserId,
        };

    [Fact]
    public async Task Saving_a_business_category_on_an_unassigned_ticket_triggers_auto_assignment()
    {
        var (db, customer, priority, creator, _, generalInquiry, billing, agent) = await SeedAsync();
        var ticket = await AddAsync(db, NewTicket(customer, generalInquiry, priority, creator.Id));
        var service = CreateService(db);

        var result = await service.UpdateAsync(
            ticket.Id, new UpdateTicketRequest(ticket.Subject, ticket.Description, billing.Id, priority.Id), creator.Id);

        Assert.Equal(TicketOperationOutcome.Success, result.Outcome);
        Assert.Equal(agent.Id, result.Ticket!.AssignedUserId);
    }

    [Fact]
    public async Task Saving_the_default_category_never_triggers_auto_assignment()
    {
        var (db, customer, priority, creator, department, generalInquiry, billing, _) = await SeedAsync();
        // Start the ticket under Billing, then move it BACK to General Inquiry — still an unassigned
        // ticket with a real category change, but the target is the default, so it must stay unassigned.
        var ticket = await AddAsync(db, NewTicket(customer, billing, priority, creator.Id));
        var service = CreateService(db);

        var result = await service.UpdateAsync(
            ticket.Id, new UpdateTicketRequest(ticket.Subject, ticket.Description, generalInquiry.Id, priority.Id), creator.Id);

        Assert.Equal(TicketOperationOutcome.Success, result.Outcome);
        Assert.Null(result.Ticket!.AssignedUserId);
    }

    [Fact]
    public async Task Saving_the_same_category_again_does_not_trigger_auto_assignment()
    {
        var (db, customer, priority, creator, _, generalInquiry, billing, agent) = await SeedAsync();
        var ticket = await AddAsync(db, NewTicket(customer, generalInquiry, priority, creator.Id));
        var service = CreateService(db);

        // First save actually changes the category (into Billing) and DOES auto-assign...
        var first = await service.UpdateAsync(
            ticket.Id, new UpdateTicketRequest(ticket.Subject, "Updated once", billing.Id, priority.Id), creator.Id);
        Assert.Equal(agent.Id, first.Ticket!.AssignedUserId);

        // ...unassign it by hand to isolate the "re-saving the same category" case from the
        // "already assigned" case (both must no-op, but for different reasons).
        ticket.AssignedUserId = null;
        await db.SaveChangesAsync();

        var second = await service.UpdateAsync(
            ticket.Id, new UpdateTicketRequest(ticket.Subject, "Updated twice", billing.Id, priority.Id), creator.Id);

        Assert.Equal(TicketOperationOutcome.Success, second.Outcome);
        Assert.Null(second.Ticket!.AssignedUserId);
    }

    [Fact]
    public async Task Changing_the_category_on_an_already_assigned_ticket_does_not_reassign()
    {
        var (db, customer, priority, creator, department, generalInquiry, billing, agent) = await SeedAsync();
        var otherAgent = await AddAsync(db, new User { Email = "other@local.test", DisplayName = "Other", PasswordHash = "x", DepartmentId = department.Id });
        var ticket = await AddAsync(db, NewTicket(customer, generalInquiry, priority, creator.Id, assignedUserId: otherAgent.Id));
        var service = CreateService(db);

        var result = await service.UpdateAsync(
            ticket.Id, new UpdateTicketRequest(ticket.Subject, ticket.Description, billing.Id, priority.Id), creator.Id);

        Assert.Equal(TicketOperationOutcome.Success, result.Outcome);
        Assert.Equal(otherAgent.Id, result.Ticket!.AssignedUserId);
    }

    [Fact]
    public async Task Auto_assignment_and_the_category_change_history_row_commit_in_the_same_save()
    {
        var (db, customer, priority, creator, _, generalInquiry, billing, agent) = await SeedAsync();
        var ticket = await AddAsync(db, NewTicket(customer, generalInquiry, priority, creator.Id));
        var service = CreateService(db);

        await service.UpdateAsync(
            ticket.Id, new UpdateTicketRequest(ticket.Subject, ticket.Description, billing.Id, priority.Id), creator.Id);

        var historyRows = await db.TicketHistories.Where(h => h.TicketId == ticket.Id).ToListAsync();
        Assert.Contains(historyRows, h => h.EventType == "CategoryChanged");
        Assert.Contains(historyRows, h => h.EventType == "Assigned" && h.PerformedByUserId == null);
    }
}
