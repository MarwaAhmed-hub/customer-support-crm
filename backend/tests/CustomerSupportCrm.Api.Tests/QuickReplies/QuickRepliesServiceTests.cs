using CustomerSupportCrm.Api.QuickReplies;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CustomerSupportCrm.Api.Tests.QuickReplies;

public class QuickRepliesServiceTests
{
    private static CrmDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task Create_persists_and_returns_dto()
    {
        await using var db = CreateDb();
        var service = new QuickRepliesService(db);

        var result = await service.CreateAsync(new CreateQuickReplyRequest("Greeting", "Hello, thanks for reaching out!"));

        Assert.Equal(QuickReplyOperationOutcome.Success, result.Outcome);
        Assert.Equal("Greeting", result.QuickReply!.Title);
        Assert.Equal("Hello, thanks for reaching out!", result.QuickReply.Body);
        Assert.True(result.QuickReply.IsActive);
    }

    [Fact]
    public async Task Create_trims_title_and_body()
    {
        await using var db = CreateDb();
        var service = new QuickRepliesService(db);

        var result = await service.CreateAsync(new CreateQuickReplyRequest("  Greeting  ", "  Hello there  "));

        Assert.Equal(QuickReplyOperationOutcome.Success, result.Outcome);
        Assert.Equal("Greeting", result.QuickReply!.Title);
        Assert.Equal("Hello there", result.QuickReply.Body);
    }

    [Fact]
    public async Task Create_with_whitespace_only_title_returns_InvalidTitle()
    {
        await using var db = CreateDb();
        var service = new QuickRepliesService(db);

        var result = await service.CreateAsync(new CreateQuickReplyRequest("   ", "Body text"));

        Assert.Equal(QuickReplyOperationOutcome.InvalidTitle, result.Outcome);
    }

    [Fact]
    public async Task Create_with_whitespace_only_body_returns_InvalidBody()
    {
        await using var db = CreateDb();
        var service = new QuickRepliesService(db);

        var result = await service.CreateAsync(new CreateQuickReplyRequest("Title", "   "));

        Assert.Equal(QuickReplyOperationOutcome.InvalidBody, result.Outcome);
    }

    [Fact]
    public async Task Create_with_duplicate_title_case_insensitive_returns_DuplicateTitle()
    {
        await using var db = CreateDb();
        var service = new QuickRepliesService(db);
        await service.CreateAsync(new CreateQuickReplyRequest("Greeting", "Hello"));

        var result = await service.CreateAsync(new CreateQuickReplyRequest("  greeting  ", "Different body"));

        Assert.Equal(QuickReplyOperationOutcome.DuplicateTitle, result.Outcome);
    }

    [Fact]
    public async Task List_excludes_inactive_by_default()
    {
        await using var db = CreateDb();
        var service = new QuickRepliesService(db);
        var created = await service.CreateAsync(new CreateQuickReplyRequest("Greeting", "Hello"));
        await service.UpdateAsync(created.QuickReply!.Id, new UpdateQuickReplyRequest("Greeting", "Hello", false));

        var results = await service.ListAsync(includeInactive: false, search: null);

        Assert.Empty(results);
    }

    [Fact]
    public async Task List_includes_inactive_when_requested()
    {
        await using var db = CreateDb();
        var service = new QuickRepliesService(db);
        var created = await service.CreateAsync(new CreateQuickReplyRequest("Greeting", "Hello"));
        await service.UpdateAsync(created.QuickReply!.Id, new UpdateQuickReplyRequest("Greeting", "Hello", false));

        var results = await service.ListAsync(includeInactive: true, search: null);

        Assert.Single(results);
        Assert.False(results[0].IsActive);
    }

    [Fact]
    public async Task List_with_search_matches_title_or_body_case_insensitively()
    {
        await using var db = CreateDb();
        var service = new QuickRepliesService(db);
        await service.CreateAsync(new CreateQuickReplyRequest("Refund policy", "We process refunds within 5 days"));
        await service.CreateAsync(new CreateQuickReplyRequest("Shipping delay", "Your order is delayed"));

        var byTitle = await service.ListAsync(includeInactive: false, search: "refund");
        var byBody = await service.ListAsync(includeInactive: false, search: "delayed");

        Assert.Single(byTitle);
        Assert.Equal("Refund policy", byTitle[0].Title);
        Assert.Single(byBody);
        Assert.Equal("Shipping delay", byBody[0].Title);
    }

    [Fact]
    public async Task List_with_no_matching_search_returns_empty()
    {
        await using var db = CreateDb();
        var service = new QuickRepliesService(db);
        await service.CreateAsync(new CreateQuickReplyRequest("Greeting", "Hello"));

        var results = await service.ListAsync(includeInactive: false, search: "nonexistent");

        Assert.Empty(results);
    }

    [Fact]
    public async Task Update_changes_title_body_and_isActive()
    {
        await using var db = CreateDb();
        var service = new QuickRepliesService(db);
        var created = await service.CreateAsync(new CreateQuickReplyRequest("Greeting", "Hello"));

        var result = await service.UpdateAsync(created.QuickReply!.Id, new UpdateQuickReplyRequest("Updated greeting", "Hi there", false));

        Assert.Equal(QuickReplyOperationOutcome.Success, result.Outcome);
        Assert.Equal("Updated greeting", result.QuickReply!.Title);
        Assert.Equal("Hi there", result.QuickReply.Body);
        Assert.False(result.QuickReply.IsActive);
    }

    [Fact]
    public async Task Update_with_unknown_id_returns_NotFound()
    {
        await using var db = CreateDb();
        var service = new QuickRepliesService(db);

        var result = await service.UpdateAsync(Guid.NewGuid(), new UpdateQuickReplyRequest("Title", "Body", true));

        Assert.Equal(QuickReplyOperationOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task Update_to_a_title_already_used_by_another_quick_reply_returns_DuplicateTitle()
    {
        await using var db = CreateDb();
        var service = new QuickRepliesService(db);
        await service.CreateAsync(new CreateQuickReplyRequest("Greeting", "Hello"));
        var other = await service.CreateAsync(new CreateQuickReplyRequest("Farewell", "Goodbye"));

        var result = await service.UpdateAsync(other.QuickReply!.Id, new UpdateQuickReplyRequest("greeting", "Goodbye", true));

        Assert.Equal(QuickReplyOperationOutcome.DuplicateTitle, result.Outcome);
    }

    [Fact]
    public async Task Update_keeping_its_own_title_unchanged_still_succeeds()
    {
        await using var db = CreateDb();
        var service = new QuickRepliesService(db);
        var created = await service.CreateAsync(new CreateQuickReplyRequest("Greeting", "Hello"));

        var result = await service.UpdateAsync(created.QuickReply!.Id, new UpdateQuickReplyRequest("Greeting", "Hello again", true));

        Assert.Equal(QuickReplyOperationOutcome.Success, result.Outcome);
        Assert.Equal("Hello again", result.QuickReply!.Body);
    }

    [Fact]
    public async Task Get_returns_null_for_unknown_id()
    {
        await using var db = CreateDb();
        var service = new QuickRepliesService(db);

        var result = await service.GetAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task Delete_removes_the_quick_reply_and_returns_true()
    {
        await using var db = CreateDb();
        var service = new QuickRepliesService(db);
        var created = await service.CreateAsync(new CreateQuickReplyRequest("Greeting", "Hello"));

        var deleted = await service.DeleteAsync(created.QuickReply!.Id);

        Assert.True(deleted);
        Assert.Null(await service.GetAsync(created.QuickReply.Id));
    }

    [Fact]
    public async Task Delete_with_unknown_id_returns_false()
    {
        await using var db = CreateDb();
        var service = new QuickRepliesService(db);

        var deleted = await service.DeleteAsync(Guid.NewGuid());

        Assert.False(deleted);
    }
}
