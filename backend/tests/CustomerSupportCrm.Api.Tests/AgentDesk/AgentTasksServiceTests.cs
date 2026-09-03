using CustomerSupportCrm.Api.AgentDesk.Tasks;
using CustomerSupportCrm.Domain.Customers;
using CustomerSupportCrm.Domain.Tickets;
using CustomerSupportCrm.Domain.Users;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CustomerSupportCrm.Api.Tests.AgentDesk;

public class AgentTasksServiceTests
{
    private static CrmDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<User> AddUserAsync(CrmDbContext db, string email = "agent@local.test")
    {
        var user = new User { Email = email, DisplayName = email, PasswordHash = "x" };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    /// <summary>Minimal valid Ticket graph (Customer + Category + Priority + creator) — only the ticket itself and its Subject matter to these tests.</summary>
    private static async Task<Ticket> AddTicketAsync(CrmDbContext db, Guid createdByUserId, string subject = "Cannot log in")
    {
        var customer = new Customer { FirstName = "Jane", LastName = "Doe" };
        var category = new TicketCategory { Name = $"Cat-{Guid.NewGuid()}", NormalizedName = "CAT" };
        var priority = new TicketPriority { Name = "Low", NormalizedName = "LOW", SortOrder = 10 };
        db.AddRange(customer, category, priority);
        await db.SaveChangesAsync();

        var ticket = new Ticket
        {
            CustomerId = customer.Id,
            Subject = subject,
            Description = "desc",
            CategoryId = category.Id,
            PriorityId = priority.Id,
            CreatedByUserId = createdByUserId,
        };
        db.Tickets.Add(ticket);
        await db.SaveChangesAsync();
        return ticket;
    }

    [Fact]
    public async Task Create_persists_and_returns_dto_with_state_Pending_when_no_reminder()
    {
        await using var db = CreateDb();
        var owner = await AddUserAsync(db);
        var service = new AgentTasksService(db);

        var result = await service.CreateAsync(owner.Id, new CreateAgentTaskRequest("Call back customer", null, null));

        Assert.Equal(AgentTaskOperationOutcome.Success, result.Outcome);
        Assert.Equal(AgentTaskState.Pending, result.Task!.State);
        Assert.Null(result.Task.ReminderAt);
    }

    [Fact]
    public async Task Create_returns_Upcoming_when_reminder_in_future()
    {
        await using var db = CreateDb();
        var owner = await AddUserAsync(db);
        var service = new AgentTasksService(db);

        var result = await service.CreateAsync(owner.Id, new CreateAgentTaskRequest("Follow up", null, DateTime.UtcNow.AddHours(1)));

        Assert.Equal(AgentTaskState.Upcoming, result.Task!.State);
    }

    [Fact]
    public async Task Create_returns_Overdue_when_reminder_in_past()
    {
        await using var db = CreateDb();
        var owner = await AddUserAsync(db);
        var service = new AgentTasksService(db);

        var result = await service.CreateAsync(owner.Id, new CreateAgentTaskRequest("Overdue thing", null, DateTime.UtcNow.AddHours(-1)));

        Assert.Equal(AgentTaskState.Overdue, result.Task!.State);
    }

    [Fact]
    public async Task Create_rejects_a_whitespace_only_title()
    {
        await using var db = CreateDb();
        var owner = await AddUserAsync(db);
        var service = new AgentTasksService(db);

        var result = await service.CreateAsync(owner.Id, new CreateAgentTaskRequest("   ", null, null));

        Assert.Equal(AgentTaskOperationOutcome.InvalidTitle, result.Outcome);
    }

    [Fact]
    public async Task List_only_returns_tasks_owned_by_current_user()
    {
        await using var db = CreateDb();
        var owner = await AddUserAsync(db, "owner@local.test");
        var other = await AddUserAsync(db, "other@local.test");
        var service = new AgentTasksService(db);
        await service.CreateAsync(owner.Id, new CreateAgentTaskRequest("Mine 1", null, null));
        await service.CreateAsync(owner.Id, new CreateAgentTaskRequest("Mine 2", null, null));
        await service.CreateAsync(other.Id, new CreateAgentTaskRequest("Not mine", null, null));

        var tasks = await service.ListAsync(owner.Id, includeCompleted: null, state: null);

        Assert.Equal(2, tasks.Count);
        Assert.DoesNotContain(tasks, t => t.Title == "Not mine");
    }

    [Fact]
    public async Task List_with_includeCompleted_false_excludes_completed_tasks()
    {
        await using var db = CreateDb();
        var owner = await AddUserAsync(db);
        var service = new AgentTasksService(db);
        var pending = await service.CreateAsync(owner.Id, new CreateAgentTaskRequest("Pending", null, null));
        var done = await service.CreateAsync(owner.Id, new CreateAgentTaskRequest("Done", null, null));
        await service.CompleteAsync(owner.Id, done.Task!.Id, completed: true);

        var tasks = await service.ListAsync(owner.Id, includeCompleted: false, state: null);

        Assert.Single(tasks);
        Assert.Equal(pending.Task!.Id, tasks[0].Id);
    }

    [Fact]
    public async Task Update_rejects_blank_title()
    {
        await using var db = CreateDb();
        var owner = await AddUserAsync(db);
        var service = new AgentTasksService(db);
        var created = await service.CreateAsync(owner.Id, new CreateAgentTaskRequest("Original", null, null));

        var result = await service.UpdateAsync(owner.Id, created.Task!.Id, new UpdateAgentTaskRequest("  ", null, null));

        Assert.Equal(AgentTaskOperationOutcome.InvalidTitle, result.Outcome);
    }

    [Fact]
    public async Task Complete_sets_completedAt_and_transitions_state()
    {
        await using var db = CreateDb();
        var owner = await AddUserAsync(db);
        var service = new AgentTasksService(db);
        var created = await service.CreateAsync(owner.Id, new CreateAgentTaskRequest("Task", null, null));

        var completed = await service.CompleteAsync(owner.Id, created.Task!.Id, completed: true);

        Assert.NotNull(completed!.CompletedAt);
        Assert.Equal(AgentTaskState.Completed, completed.State);
    }

    [Fact]
    public async Task Complete_is_idempotent()
    {
        await using var db = CreateDb();
        var owner = await AddUserAsync(db);
        var service = new AgentTasksService(db);
        var created = await service.CreateAsync(owner.Id, new CreateAgentTaskRequest("Task", null, null));
        var firstComplete = await service.CompleteAsync(owner.Id, created.Task!.Id, completed: true);

        var secondComplete = await service.CompleteAsync(owner.Id, created.Task.Id, completed: true);

        Assert.Equal(firstComplete!.CompletedAt, secondComplete!.CompletedAt);
    }

    [Fact]
    public async Task Reopen_clears_completedAt()
    {
        await using var db = CreateDb();
        var owner = await AddUserAsync(db);
        var service = new AgentTasksService(db);
        var created = await service.CreateAsync(owner.Id, new CreateAgentTaskRequest("Task", null, null));
        await service.CompleteAsync(owner.Id, created.Task!.Id, completed: true);

        var reopened = await service.CompleteAsync(owner.Id, created.Task.Id, completed: false);

        Assert.Null(reopened!.CompletedAt);
        Assert.NotEqual(AgentTaskState.Completed, reopened.State);
    }

    [Fact]
    public async Task Reopen_a_task_that_was_never_completed_is_a_no_op()
    {
        await using var db = CreateDb();
        var owner = await AddUserAsync(db);
        var service = new AgentTasksService(db);
        var created = await service.CreateAsync(owner.Id, new CreateAgentTaskRequest("Task", null, null));

        var reopened = await service.CompleteAsync(owner.Id, created.Task!.Id, completed: false);

        Assert.NotNull(reopened);
        Assert.Null(reopened!.CompletedAt);
    }

    [Fact]
    public async Task Delete_returns_false_for_task_owned_by_other_user()
    {
        await using var db = CreateDb();
        var owner = await AddUserAsync(db, "owner@local.test");
        var other = await AddUserAsync(db, "other@local.test");
        var service = new AgentTasksService(db);
        var created = await service.CreateAsync(owner.Id, new CreateAgentTaskRequest("Task", null, null));

        var deleted = await service.DeleteAsync(other.Id, created.Task!.Id);

        Assert.False(deleted);
        Assert.NotNull(await service.GetAsync(owner.Id, created.Task.Id));
    }

    [Fact]
    public async Task Get_returns_null_for_task_owned_by_other_user()
    {
        await using var db = CreateDb();
        var owner = await AddUserAsync(db, "owner@local.test");
        var other = await AddUserAsync(db, "other@local.test");
        var service = new AgentTasksService(db);
        var created = await service.CreateAsync(owner.Id, new CreateAgentTaskRequest("Task", null, null));

        Assert.Null(await service.GetAsync(other.Id, created.Task!.Id));
    }

    // --- Ticket-linking correction ---

    [Fact]
    public async Task Create_with_a_valid_ticketId_links_the_task_and_surfaces_the_ticket_subject()
    {
        await using var db = CreateDb();
        var owner = await AddUserAsync(db);
        var ticket = await AddTicketAsync(db, owner.Id, "Cannot log in");
        var service = new AgentTasksService(db);

        var result = await service.CreateAsync(owner.Id, new CreateAgentTaskRequest("Follow up", null, null, ticket.Id));

        Assert.Equal(AgentTaskOperationOutcome.Success, result.Outcome);
        Assert.Equal(ticket.Id, result.Task!.TicketId);
        Assert.Equal("Cannot log in", result.Task.TicketSubject);
    }

    [Fact]
    public async Task Create_with_no_ticketId_leaves_the_task_unlinked()
    {
        await using var db = CreateDb();
        var owner = await AddUserAsync(db);
        var service = new AgentTasksService(db);

        var result = await service.CreateAsync(owner.Id, new CreateAgentTaskRequest("General task", null, null));

        Assert.Null(result.Task!.TicketId);
        Assert.Null(result.Task.TicketSubject);
    }

    [Fact]
    public async Task Create_with_an_unknown_ticketId_returns_TicketNotFound()
    {
        await using var db = CreateDb();
        var owner = await AddUserAsync(db);
        var service = new AgentTasksService(db);

        var result = await service.CreateAsync(owner.Id, new CreateAgentTaskRequest("Task", null, null, Guid.NewGuid()));

        Assert.Equal(AgentTaskOperationOutcome.TicketNotFound, result.Outcome);
    }

    [Fact]
    public async Task Update_can_link_an_existing_unlinked_task_to_a_ticket()
    {
        await using var db = CreateDb();
        var owner = await AddUserAsync(db);
        var ticket = await AddTicketAsync(db, owner.Id);
        var service = new AgentTasksService(db);
        var created = await service.CreateAsync(owner.Id, new CreateAgentTaskRequest("Task", null, null));

        var result = await service.UpdateAsync(owner.Id, created.Task!.Id, new UpdateAgentTaskRequest("Task", null, null, ticket.Id));

        Assert.Equal(AgentTaskOperationOutcome.Success, result.Outcome);
        Assert.Equal(ticket.Id, result.Task!.TicketId);
    }

    [Fact]
    public async Task List_with_a_ticketId_filter_returns_only_tasks_linked_to_that_ticket()
    {
        await using var db = CreateDb();
        var owner = await AddUserAsync(db);
        var ticketA = await AddTicketAsync(db, owner.Id, "Ticket A");
        var ticketB = await AddTicketAsync(db, owner.Id, "Ticket B");
        var service = new AgentTasksService(db);
        await service.CreateAsync(owner.Id, new CreateAgentTaskRequest("Linked to A", null, null, ticketA.Id));
        await service.CreateAsync(owner.Id, new CreateAgentTaskRequest("Linked to B", null, null, ticketB.Id));
        await service.CreateAsync(owner.Id, new CreateAgentTaskRequest("Unlinked", null, null));

        var tasks = await service.ListAsync(owner.Id, includeCompleted: null, state: null, ticketId: ticketA.Id);

        Assert.Single(tasks);
        Assert.Equal("Linked to A", tasks[0].Title);
    }

    [Fact]
    public async Task List_without_a_ticketId_filter_still_returns_both_linked_and_unlinked_tasks()
    {
        await using var db = CreateDb();
        var owner = await AddUserAsync(db);
        var ticket = await AddTicketAsync(db, owner.Id);
        var service = new AgentTasksService(db);
        await service.CreateAsync(owner.Id, new CreateAgentTaskRequest("Linked", null, null, ticket.Id));
        await service.CreateAsync(owner.Id, new CreateAgentTaskRequest("Unlinked", null, null));

        var tasks = await service.ListAsync(owner.Id, includeCompleted: null, state: null);

        Assert.Equal(2, tasks.Count);
    }
}
