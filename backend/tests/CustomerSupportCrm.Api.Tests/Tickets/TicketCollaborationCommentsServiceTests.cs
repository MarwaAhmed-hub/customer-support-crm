using CustomerSupportCrm.Api.Tickets.Collaboration;
using CustomerSupportCrm.Domain.Customers;
using CustomerSupportCrm.Domain.Tickets;
using CustomerSupportCrm.Domain.Users;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CustomerSupportCrm.Api.Tests.Tickets;

public class TicketCollaborationCommentsServiceTests
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

    private static async Task<Ticket> AddTicketAsync(CrmDbContext db, Guid createdByUserId)
    {
        var customer = new Customer { FirstName = "Jane", LastName = "Doe" };
        var category = new TicketCategory { Name = $"Cat-{Guid.NewGuid()}", NormalizedName = "CAT" };
        var priority = new TicketPriority { Name = "Low", NormalizedName = "LOW", SortOrder = 10 };
        db.AddRange(customer, category, priority);
        await db.SaveChangesAsync();

        var ticket = new Ticket
        {
            CustomerId = customer.Id,
            Subject = "Cannot log in",
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
    public async Task CreateAsync_persists_comment_with_author_and_timestamp()
    {
        await using var db = CreateDb();
        var author = await AddUserAsync(db);
        var ticket = await AddTicketAsync(db, author.Id);
        var service = new TicketCollaborationCommentsService(db);

        var result = await service.CreateAsync(ticket.Id, author.Id, new CreateTicketCollaborationCommentRequest("Let's escalate this to billing."));

        Assert.Equal(TicketCollaborationCommentOperationOutcome.Success, result.Outcome);
        Assert.Equal("Let's escalate this to billing.", result.Comment!.Body);
        Assert.Equal(author.Id, result.Comment.AuthorUserId);
        Assert.Equal(author.DisplayName, result.Comment.AuthorDisplayName);
        Assert.NotEqual(default, result.Comment.CreatedAt);
    }

    [Fact]
    public async Task CreateAsync_trims_body()
    {
        await using var db = CreateDb();
        var author = await AddUserAsync(db);
        var ticket = await AddTicketAsync(db, author.Id);
        var service = new TicketCollaborationCommentsService(db);

        var result = await service.CreateAsync(ticket.Id, author.Id, new CreateTicketCollaborationCommentRequest("  padded  "));

        Assert.Equal("padded", result.Comment!.Body);
    }

    [Fact]
    public async Task CreateAsync_rejects_whitespace_only_body()
    {
        await using var db = CreateDb();
        var author = await AddUserAsync(db);
        var ticket = await AddTicketAsync(db, author.Id);
        var service = new TicketCollaborationCommentsService(db);

        var result = await service.CreateAsync(ticket.Id, author.Id, new CreateTicketCollaborationCommentRequest("   "));

        Assert.Equal(TicketCollaborationCommentOperationOutcome.InvalidBody, result.Outcome);
    }

    [Fact]
    public async Task CreateAsync_with_an_unknown_ticket_returns_TicketNotFound()
    {
        await using var db = CreateDb();
        var author = await AddUserAsync(db);
        var service = new TicketCollaborationCommentsService(db);

        var result = await service.CreateAsync(Guid.NewGuid(), author.Id, new CreateTicketCollaborationCommentRequest("Hello"));

        Assert.Equal(TicketCollaborationCommentOperationOutcome.TicketNotFound, result.Outcome);
    }

    [Fact]
    public async Task CreateAsync_does_not_modify_ticket_status_or_assignee_or_updatedAt()
    {
        await using var db = CreateDb();
        var author = await AddUserAsync(db);
        var ticket = await AddTicketAsync(db, author.Id);
        var originalStatus = ticket.Status;
        var originalAssignedUserId = ticket.AssignedUserId;
        var originalUpdatedAt = ticket.UpdatedAt;
        var service = new TicketCollaborationCommentsService(db);

        await service.CreateAsync(ticket.Id, author.Id, new CreateTicketCollaborationCommentRequest("Internal note"));

        var reloaded = await db.Tickets.AsNoTracking().SingleAsync(t => t.Id == ticket.Id);
        Assert.Equal(originalStatus, reloaded.Status);
        Assert.Equal(originalAssignedUserId, reloaded.AssignedUserId);
        Assert.Equal(originalUpdatedAt, reloaded.UpdatedAt);
    }

    [Fact]
    public async Task ListAsync_returns_comments_in_created_at_ascending_order()
    {
        await using var db = CreateDb();
        var author = await AddUserAsync(db);
        var ticket = await AddTicketAsync(db, author.Id);
        var service = new TicketCollaborationCommentsService(db);

        db.TicketCollaborationComments.Add(new TicketCollaborationComment
        {
            TicketId = ticket.Id, AuthorUserId = author.Id, Body = "First",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-10),
        });
        db.TicketCollaborationComments.Add(new TicketCollaborationComment
        {
            TicketId = ticket.Id, AuthorUserId = author.Id, Body = "Second",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
        });
        await db.SaveChangesAsync();

        var result = await service.ListAsync(ticket.Id);

        Assert.NotNull(result);
        Assert.Equal(["First", "Second"], result!.Select(c => c.Body));
    }

    [Fact]
    public async Task ListAsync_returns_empty_when_no_comments()
    {
        await using var db = CreateDb();
        var author = await AddUserAsync(db);
        var ticket = await AddTicketAsync(db, author.Id);
        var service = new TicketCollaborationCommentsService(db);

        var result = await service.ListAsync(ticket.Id);

        Assert.NotNull(result);
        Assert.Empty(result!);
    }

    [Fact]
    public async Task ListAsync_returns_null_when_ticket_not_found()
    {
        await using var db = CreateDb();
        var service = new TicketCollaborationCommentsService(db);

        var result = await service.ListAsync(Guid.NewGuid());

        Assert.Null(result);
    }
}
