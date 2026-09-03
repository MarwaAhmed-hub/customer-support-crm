using CustomerSupportCrm.Api.Communications.WebForms;
using CustomerSupportCrm.Api.Customers;
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

namespace CustomerSupportCrm.Api.Tests.Communications;

public class WebFormSubmissionServiceTests
{
    private static CrmDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<WebFormSubmissionService> SeedAsync(CrmDbContext db)
    {
        db.TicketCategories.Add(new TicketCategory { Name = "General Inquiry", NormalizedName = "GENERAL INQUIRY" });
        db.TicketPriorities.Add(new TicketPriority { Name = "Medium", NormalizedName = "MEDIUM", SortOrder = 20 });
        // WebFormSubmissionService always attributes the ticket to the seeded system account —
        // mirror that seed row here rather than depending on DbSeeder in a unit test.
        db.Users.Add(new User { Email = DbSeeder.SystemUserEmail, DisplayName = "System (Automated)", PasswordHash = "x", IsActive = false });
        await db.SaveChangesAsync();

        var customersService = new CustomersService(db);
        var ticketsService = new TicketsService(db, new TicketHistoryService(db), new SlaService(db, NullLogger<SlaService>.Instance), new TicketAssignmentService(db, new TicketHistoryService(db), NullLogger<TicketAssignmentService>.Instance));
        return new WebFormSubmissionService(db, customersService, ticketsService);
    }

    [Fact]
    public async Task SubmitAsync_valid_payload_creates_customer_ticket_and_one_web_form_interaction()
    {
        await using var db = CreateDb();
        var service = await SeedAsync(db);

        var result = await service.SubmitAsync(new WebFormSubmissionRequest("Ali Hassan", "ali@example.com", "Feature request", "Add dark mode"));

        Assert.Equal(WebFormSubmissionOutcome.Success, result.Outcome);

        var ticket = await db.Tickets.SingleAsync(t => t.Id == result.TicketId);
        Assert.Equal("WebForm", ticket.SourceChannel);
        Assert.Equal(DbSeeder.SystemUserEmail, (await db.Users.SingleAsync(u => u.Id == ticket.CreatedByUserId)).Email);

        var customer = await db.Customers.SingleAsync(c => c.Id == result.CustomerId);
        Assert.Equal("Ali", customer.FirstName);
        Assert.Equal("Hassan", customer.LastName);
        Assert.Equal("ali@example.com", customer.Email);

        var interactions = await db.CustomerInteractions.Where(i => i.TicketId == result.TicketId).ToListAsync();
        Assert.Single(interactions);
        Assert.Equal("web_form", interactions[0].InteractionType);
    }

    [Fact]
    public async Task SubmitAsync_with_an_existing_email_reuses_the_customer()
    {
        await using var db = CreateDb();
        var service = await SeedAsync(db);

        var first = await service.SubmitAsync(new WebFormSubmissionRequest("Ali Hassan", "ali@example.com", "First", "Body one"));
        var second = await service.SubmitAsync(new WebFormSubmissionRequest("Ali Hassan", "ali@example.com", "Second", "Body two"));

        Assert.Equal(first.CustomerId, second.CustomerId);
        Assert.NotEqual(first.TicketId, second.TicketId);
        Assert.Equal(1, await db.Customers.CountAsync());
    }

    [Fact]
    public async Task SubmitAsync_with_the_honeypot_filled_silently_drops_and_writes_nothing()
    {
        await using var db = CreateDb();
        var service = await SeedAsync(db);

        var result = await service.SubmitAsync(new WebFormSubmissionRequest("Bot", "bot@example.com", "Spam", "Body", Website: "http://spam.example"));

        Assert.Equal(WebFormSubmissionOutcome.HoneypotTriggered, result.Outcome);
        Assert.Null(result.TicketId);
        Assert.Equal(0, await db.Customers.CountAsync());
        Assert.Equal(0, await db.Tickets.CountAsync());
    }

    [Fact]
    public async Task SubmitAsync_with_an_invalid_email_returns_InvalidEmail_and_writes_nothing()
    {
        await using var db = CreateDb();
        var service = await SeedAsync(db);

        var result = await service.SubmitAsync(new WebFormSubmissionRequest("Ali", "not-an-email", "Subject", "Body"));

        Assert.Equal(WebFormSubmissionOutcome.InvalidEmail, result.Outcome);
        Assert.Equal(0, await db.Customers.CountAsync());
        Assert.Equal(0, await db.Tickets.CountAsync());
    }

    /// <summary>
    /// Correction: a customer who first reached out by WhatsApp/SMS (phone on file, no email yet) and
    /// then submits the web form with the same phone number must land on that same record — not a
    /// duplicate — and the previously-blank email gets filled in from the submission.
    /// </summary>
    [Fact]
    public async Task SubmitAsync_with_a_phone_matching_an_existing_phone_only_customer_reuses_it_and_backfills_the_email()
    {
        await using var db = CreateDb();
        var service = await SeedAsync(db);
        var existing = new Customer { FirstName = "+201013840094", LastName = "(via WhatsApp)", Phone = "+201013840094", Email = null };
        db.Customers.Add(existing);
        await db.SaveChangesAsync();

        var result = await service.SubmitAsync(new WebFormSubmissionRequest("Mohamed Ali", "mohamed@example.com", "Subject", "Body", Phone: "01013840094"));

        Assert.Equal(WebFormSubmissionOutcome.Success, result.Outcome);
        Assert.Equal(existing.Id, result.CustomerId);
        Assert.Equal(1, await db.Customers.CountAsync());

        var reloaded = await db.Customers.AsNoTracking().SingleAsync(c => c.Id == existing.Id);
        Assert.Equal("mohamed@example.com", reloaded.Email);
        // Backfill only touches the blank field — the name a WhatsApp-only customer got is left alone.
        Assert.Equal("+201013840094", reloaded.FirstName);
    }

    [Fact]
    public async Task SubmitAsync_does_not_overwrite_an_existing_email_with_a_different_one()
    {
        await using var db = CreateDb();
        var service = await SeedAsync(db);
        var existing = new Customer { FirstName = "Mohamed", LastName = "Ali", Phone = "+201013840094", Email = "original@example.com" };
        db.Customers.Add(existing);
        await db.SaveChangesAsync();

        await service.SubmitAsync(new WebFormSubmissionRequest("Mohamed Ali", "different@example.com", "Subject", "Body", Phone: "01013840094"));

        var reloaded = await db.Customers.AsNoTracking().SingleAsync(c => c.Id == existing.Id);
        Assert.Equal("original@example.com", reloaded.Email);
    }

    [Fact]
    public async Task SubmitAsync_with_no_phone_still_matches_by_email_as_before()
    {
        await using var db = CreateDb();
        var service = await SeedAsync(db);

        var first = await service.SubmitAsync(new WebFormSubmissionRequest("Ali Hassan", "ali@example.com", "First", "Body one", Phone: "01013840094"));
        var second = await service.SubmitAsync(new WebFormSubmissionRequest("Ali Hassan", "ali@example.com", "Second", "Body two"));

        Assert.Equal(first.CustomerId, second.CustomerId);
        Assert.Equal(1, await db.Customers.CountAsync());
    }
}
