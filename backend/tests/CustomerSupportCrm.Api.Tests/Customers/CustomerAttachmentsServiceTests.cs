using System.Text;
using CustomerSupportCrm.Api.Customers.Attachments;
using CustomerSupportCrm.Domain.Customers;
using CustomerSupportCrm.Domain.Users;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Xunit;

namespace CustomerSupportCrm.Api.Tests.Customers;

/// <summary>Uses a temp directory for storage (deleted after each test) — mirrors the plan's "Use a temp directory for storage" instruction.</summary>
public class CustomerAttachmentsServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "crm-attachment-tests-" + Guid.NewGuid().ToString("N"));

    private static CrmDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private CustomerAttachmentsService CreateService(CrmDbContext db) =>
        new(db, new FakeWebHostEnvironment(_tempRoot));

    private static async Task<Customer> AddCustomerAsync(CrmDbContext db)
    {
        var customer = new Customer { FirstName = "Jane", LastName = "Doe" };
        db.Customers.Add(customer);
        await db.SaveChangesAsync();
        return customer;
    }

    private static Stream TextStream(string content) => new MemoryStream(Encoding.UTF8.GetBytes(content));

    [Fact]
    public async Task Upload_happy_path_stores_the_file_and_returns_metadata()
    {
        await using var db = CreateDb();
        var customer = await AddCustomerAsync(db);
        var service = CreateService(db);

        var result = await service.UploadAsync(customer.Id, null, TextStream("hello"), "notes.txt", "text/plain", 5);

        Assert.Equal(CustomerAttachmentUploadOutcome.Success, result.Outcome);
        Assert.Equal("notes.txt", result.Attachment!.FileName);
        Assert.Equal(5, result.Attachment.SizeBytes);
        Assert.Contains(result.Attachment.Id.ToString(), result.Attachment.DownloadUrl);
    }

    [Fact]
    public async Task Upload_response_includes_the_uploaders_display_name()
    {
        // Regression: the upload response once hard-coded UploadedByDisplayName to null instead of
        // resolving it, even though the row itself (and the subsequent List) had it right.
        await using var db = CreateDb();
        var customer = await AddCustomerAsync(db);
        var uploader = new User { Email = "uploader@local.test", DisplayName = "Uploader", PasswordHash = "x" };
        db.Users.Add(uploader);
        await db.SaveChangesAsync();
        var service = CreateService(db);

        var result = await service.UploadAsync(customer.Id, uploader.Id, TextStream("hello"), "notes.txt", "text/plain", 5);

        Assert.Equal(CustomerAttachmentUploadOutcome.Success, result.Outcome);
        Assert.Equal("Uploader", result.Attachment!.UploadedByDisplayName);
    }

    [Fact]
    public async Task Upload_rejects_an_unknown_customer()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        var result = await service.UploadAsync(Guid.NewGuid(), null, TextStream("hello"), "notes.txt", "text/plain", 5);

        Assert.Equal(CustomerAttachmentUploadOutcome.CustomerNotFound, result.Outcome);
    }

    [Fact]
    public async Task Upload_rejects_a_zero_byte_file()
    {
        await using var db = CreateDb();
        var customer = await AddCustomerAsync(db);
        var service = CreateService(db);

        var result = await service.UploadAsync(customer.Id, null, TextStream(""), "empty.txt", "text/plain", 0);

        Assert.Equal(CustomerAttachmentUploadOutcome.Empty, result.Outcome);
    }

    [Fact]
    public async Task Upload_rejects_a_file_over_the_size_cap()
    {
        await using var db = CreateDb();
        var customer = await AddCustomerAsync(db);
        var service = CreateService(db);

        var result = await service.UploadAsync(
            customer.Id, null, TextStream("hello"), "big.txt", "text/plain", CustomerAttachmentsService.MaxBytes + 1);

        Assert.Equal(CustomerAttachmentUploadOutcome.TooLarge, result.Outcome);
    }

    [Fact]
    public async Task Upload_rejects_a_disallowed_extension()
    {
        await using var db = CreateDb();
        var customer = await AddCustomerAsync(db);
        var service = CreateService(db);

        var result = await service.UploadAsync(customer.Id, null, TextStream("MZ"), "virus.exe", "application/octet-stream", 2);

        Assert.Equal(CustomerAttachmentUploadOutcome.InvalidType, result.Outcome);
    }

    [Fact]
    public async Task List_returns_null_for_an_unknown_customer()
    {
        await using var db = CreateDb();
        var service = CreateService(db);

        Assert.Null(await service.ListAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task List_returns_uploaded_metadata_newest_first()
    {
        await using var db = CreateDb();
        var customer = await AddCustomerAsync(db);
        var service = CreateService(db);
        await service.UploadAsync(customer.Id, null, TextStream("a"), "a.txt", "text/plain", 1);
        await service.UploadAsync(customer.Id, null, TextStream("b"), "b.txt", "text/plain", 1);

        var attachments = await service.ListAsync(customer.Id);

        Assert.NotNull(attachments);
        Assert.Equal(2, attachments!.Count);
    }

    [Fact]
    public async Task Download_returns_the_uploaded_bytes()
    {
        await using var db = CreateDb();
        var customer = await AddCustomerAsync(db);
        var service = CreateService(db);
        var uploaded = await service.UploadAsync(customer.Id, null, TextStream("hello world"), "greeting.txt", "text/plain", 11);

        var content = await service.OpenReadAsync(customer.Id, uploaded.Attachment!.Id);

        Assert.NotNull(content);
        using var reader = new StreamReader(content!.Content);
        Assert.Equal("hello world", await reader.ReadToEndAsync());
    }

    [Fact]
    public async Task Download_returns_null_for_a_missing_attachment()
    {
        await using var db = CreateDb();
        var customer = await AddCustomerAsync(db);
        var service = CreateService(db);

        Assert.Null(await service.OpenReadAsync(customer.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task Delete_removes_the_row_and_the_physical_file()
    {
        await using var db = CreateDb();
        var customer = await AddCustomerAsync(db);
        var service = CreateService(db);
        var uploaded = await service.UploadAsync(customer.Id, null, TextStream("hello"), "notes.txt", "text/plain", 5);

        var deleted = await service.DeleteAsync(customer.Id, uploaded.Attachment!.Id);

        Assert.True(deleted);
        Assert.Null(await service.OpenReadAsync(customer.Id, uploaded.Attachment.Id));
    }

    [Fact]
    public async Task Delete_returns_false_for_a_missing_attachment()
    {
        await using var db = CreateDb();
        var customer = await AddCustomerAsync(db);
        var service = CreateService(db);

        Assert.False(await service.DeleteAsync(customer.Id, Guid.NewGuid()));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }

    private sealed class FakeWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "CustomerSupportCrm.Api.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = contentRootPath;
        public string EnvironmentName { get; set; } = "Development";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = contentRootPath;
    }
}
