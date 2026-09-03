using CustomerSupportCrm.Api.Customers.Notes;
using CustomerSupportCrm.Domain.Customers;
using CustomerSupportCrm.Domain.Users;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CustomerSupportCrm.Api.Tests.Customers;

public class CustomerNotesServiceTests
{
    private static CrmDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<Customer> AddCustomerAsync(CrmDbContext db)
    {
        var customer = new Customer { FirstName = "Jane", LastName = "Doe" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return customer;
    }

    [Fact]
    public async Task List_returns_null_for_an_unknown_customer()
    {
        await using var db = CreateDb();
        var service = new CustomerNotesService(db);

        Assert.Null(await service.ListAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task List_returns_an_empty_non_null_list_for_a_customer_with_no_notes()
    {
        await using var db = CreateDb();
        var customer = await AddCustomerAsync(db);
        var service = new CustomerNotesService(db);

        var notes = await service.ListAsync(customer.Id);

        Assert.NotNull(notes);
        Assert.Empty(notes);
    }

    [Fact]
    public async Task List_orders_notes_by_CreatedAt_descending()
    {
        await using var db = CreateDb();
        var customer = await AddCustomerAsync(db);
        db.CustomerNotes.Add(new CustomerNote { CustomerId = customer.Id, Body = "First", CreatedAt = DateTime.UtcNow.AddDays(-2) });
        db.CustomerNotes.Add(new CustomerNote { CustomerId = customer.Id, Body = "Second", CreatedAt = DateTime.UtcNow.AddDays(-1) });
        db.CustomerNotes.Add(new CustomerNote { CustomerId = customer.Id, Body = "Third", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var service = new CustomerNotesService(db);

        var notes = await service.ListAsync(customer.Id);

        Assert.Equal(["Third", "Second", "First"], notes!.Select(n => n.Body));
    }

    [Fact]
    public async Task Create_stamps_CreatedAt_and_CreatedByUserId()
    {
        await using var db = CreateDb();
        var customer = await AddCustomerAsync(db);
        var author = new User { Email = "author@local.test", DisplayName = "Author", PasswordHash = "x" };
        db.Users.Add(author);
        await db.SaveChangesAsync();
        var service = new CustomerNotesService(db);

        var result = await service.CreateAsync(customer.Id, author.Id, new CreateCustomerNoteRequest("Called back, left a message."));

        Assert.Equal(CustomerNoteOperationOutcome.Success, result.Outcome);
        Assert.Equal(author.Id, result.Note!.CreatedByUserId);
        Assert.Equal("Author", result.Note.CreatedByDisplayName);
        Assert.Null(result.Note.UpdatedAt);
        Assert.True(result.Note.CreatedAt > DateTime.UtcNow.AddMinutes(-1));
    }

    [Fact]
    public async Task Create_rejects_a_whitespace_only_body()
    {
        await using var db = CreateDb();
        var customer = await AddCustomerAsync(db);
        var service = new CustomerNotesService(db);

        var result = await service.CreateAsync(customer.Id, null, new CreateCustomerNoteRequest("   "));

        Assert.Equal(CustomerNoteOperationOutcome.InvalidBody, result.Outcome);
    }

    [Fact]
    public async Task Create_on_an_unknown_customer_returns_CustomerNotFound()
    {
        await using var db = CreateDb();
        var service = new CustomerNotesService(db);

        var result = await service.CreateAsync(Guid.NewGuid(), null, new CreateCustomerNoteRequest("Hello"));

        Assert.Equal(CustomerNoteOperationOutcome.CustomerNotFound, result.Outcome);
    }

    [Fact]
    public async Task Update_stamps_UpdatedAt_and_changes_the_body()
    {
        await using var db = CreateDb();
        var customer = await AddCustomerAsync(db);
        var service = new CustomerNotesService(db);
        var created = await service.CreateAsync(customer.Id, null, new CreateCustomerNoteRequest("Original"));

        var result = await service.UpdateAsync(customer.Id, created.Note!.Id, new UpdateCustomerNoteRequest("Revised"));

        Assert.Equal(CustomerNoteOperationOutcome.Success, result.Outcome);
        Assert.Equal("Revised", result.Note!.Body);
        Assert.NotNull(result.Note.UpdatedAt);
    }

    [Fact]
    public async Task Update_on_a_missing_note_returns_NoteNotFound()
    {
        await using var db = CreateDb();
        var customer = await AddCustomerAsync(db);
        var service = new CustomerNotesService(db);

        var result = await service.UpdateAsync(customer.Id, Guid.NewGuid(), new UpdateCustomerNoteRequest("Revised"));

        Assert.Equal(CustomerNoteOperationOutcome.NoteNotFound, result.Outcome);
    }

    [Fact]
    public async Task Delete_returns_false_when_the_note_is_missing()
    {
        await using var db = CreateDb();
        var customer = await AddCustomerAsync(db);
        var service = new CustomerNotesService(db);

        Assert.False(await service.DeleteAsync(customer.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task Delete_removes_an_existing_note()
    {
        await using var db = CreateDb();
        var customer = await AddCustomerAsync(db);
        var service = new CustomerNotesService(db);
        var created = await service.CreateAsync(customer.Id, null, new CreateCustomerNoteRequest("Temp"));

        var deleted = await service.DeleteAsync(customer.Id, created.Note!.Id);

        Assert.True(deleted);
        Assert.Null(await service.GetAsync(customer.Id, created.Note.Id));
    }
}
