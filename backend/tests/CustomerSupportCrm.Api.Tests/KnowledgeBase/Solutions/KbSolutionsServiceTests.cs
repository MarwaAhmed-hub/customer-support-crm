using CustomerSupportCrm.Api.KnowledgeBase;
using CustomerSupportCrm.Api.KnowledgeBase.Solutions;
using CustomerSupportCrm.Domain.KnowledgeBase;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CustomerSupportCrm.Api.Tests.KnowledgeBase.Solutions;

public class KbSolutionsServiceTests
{
    private static CrmDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<Guid> CreateCategoryAsync(CrmDbContext db, string name = "General") =>
        (await new KnowledgeBaseCategoriesService(db).CreateAsync(new CreateKnowledgeBaseCategoryRequest(name))).Category!.Id;

    [Fact]
    public async Task Create_forces_draft_status_even_though_the_request_has_no_status_field()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var service = new KbSolutionsService(db);

        var result = await service.CreateAsync(
            new CreateKbSolutionRequest("Printer offline", "Printer shows offline in the tray", "Restart the print spooler service", categoryId, KnowledgeBaseAudience.CustomerFacing),
            Guid.NewGuid());

        Assert.Equal(KbSolutionOperationOutcome.Success, result.Outcome);
        Assert.Equal(KnowledgeBasePublicationStatus.Draft, result.Solution!.Status);
        Assert.Null(result.Solution.PublishedAtUtc);
    }

    [Fact]
    public async Task Update_does_not_change_status()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var service = new KbSolutionsService(db);
        var created = await service.CreateAsync(
            new CreateKbSolutionRequest("Title", "Problem", "Fix", categoryId, KnowledgeBaseAudience.CustomerFacing), Guid.NewGuid());
        await service.PublishAsync(created.Solution!.Id, Guid.NewGuid());

        var updated = await service.UpdateAsync(
            created.Solution.Id,
            new UpdateKbSolutionRequest("New title", "New problem", "New fix", categoryId, KnowledgeBaseAudience.Internal),
            Guid.NewGuid());

        Assert.Equal(KbSolutionOperationOutcome.Success, updated.Outcome);
        Assert.Equal(KnowledgeBasePublicationStatus.Published, updated.Solution!.Status);
        Assert.Equal("New title", updated.Solution.Title);
    }

    [Fact]
    public async Task Publish_sets_published_at_and_flips_status()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var service = new KbSolutionsService(db);
        var created = await service.CreateAsync(
            new CreateKbSolutionRequest("Title", "Problem", "Fix", categoryId, KnowledgeBaseAudience.CustomerFacing), Guid.NewGuid());

        var result = await service.PublishAsync(created.Solution!.Id, Guid.NewGuid());

        Assert.Equal(KnowledgeBasePublicationStatus.Published, result.Solution!.Status);
        Assert.NotNull(result.Solution.PublishedAtUtc);
    }

    [Fact]
    public async Task Publish_is_idempotent_and_keeps_the_original_published_at()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var service = new KbSolutionsService(db);
        var created = await service.CreateAsync(
            new CreateKbSolutionRequest("Title", "Problem", "Fix", categoryId, KnowledgeBaseAudience.CustomerFacing), Guid.NewGuid());
        var first = await service.PublishAsync(created.Solution!.Id, Guid.NewGuid());

        var second = await service.PublishAsync(created.Solution.Id, Guid.NewGuid());

        Assert.Equal(first.Solution!.PublishedAtUtc, second.Solution!.PublishedAtUtc);
    }

    [Fact]
    public async Task Unpublish_clears_published_at()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var service = new KbSolutionsService(db);
        var created = await service.CreateAsync(
            new CreateKbSolutionRequest("Title", "Problem", "Fix", categoryId, KnowledgeBaseAudience.CustomerFacing), Guid.NewGuid());
        await service.PublishAsync(created.Solution!.Id, Guid.NewGuid());

        var result = await service.UnpublishAsync(created.Solution.Id, Guid.NewGuid());

        Assert.Equal(KnowledgeBasePublicationStatus.Draft, result.Solution!.Status);
        Assert.Null(result.Solution.PublishedAtUtc);
    }

    [Fact]
    public async Task List_hides_drafts_from_a_caller_without_manage()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var service = new KbSolutionsService(db);
        await service.CreateAsync(new CreateKbSolutionRequest("Draft one", "P", "F", categoryId, KnowledgeBaseAudience.CustomerFacing), Guid.NewGuid());
        var published = await service.CreateAsync(new CreateKbSolutionRequest("Published one", "P", "F", categoryId, KnowledgeBaseAudience.CustomerFacing), Guid.NewGuid());
        await service.PublishAsync(published.Solution!.Id, Guid.NewGuid());

        var result = await service.ListAsync(categoryId: null, audience: null, status: null, canManage: false, canSeeInternal: true);

        var item = Assert.Single(result);
        Assert.Equal("Published one", item.Title);
    }

    [Fact]
    public async Task GetAsync_returns_404_shape_when_agent_requests_a_draft()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var service = new KbSolutionsService(db);
        var created = await service.CreateAsync(new CreateKbSolutionRequest("Title", "P", "F", categoryId, KnowledgeBaseAudience.CustomerFacing), Guid.NewGuid());

        var asAgent = await service.GetAsync(created.Solution!.Id, canManage: false, canSeeInternal: true);
        var asManager = await service.GetAsync(created.Solution.Id, canManage: true, canSeeInternal: false);

        Assert.Null(asAgent);
        Assert.NotNull(asManager);
    }

    [Fact]
    public async Task List_excludes_draft_and_internal_only_for_a_customer()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var service = new KbSolutionsService(db);

        var draft = await service.CreateAsync(new CreateKbSolutionRequest("Draft", "P", "F", categoryId, KnowledgeBaseAudience.CustomerFacing), Guid.NewGuid());
        var internalOnly = await service.CreateAsync(new CreateKbSolutionRequest("Internal", "P", "F", categoryId, KnowledgeBaseAudience.Internal), Guid.NewGuid());
        await service.PublishAsync(internalOnly.Solution!.Id, Guid.NewGuid());
        var customerFacing = await service.CreateAsync(new CreateKbSolutionRequest("Public", "P", "F", categoryId, KnowledgeBaseAudience.CustomerFacing), Guid.NewGuid());
        await service.PublishAsync(customerFacing.Solution!.Id, Guid.NewGuid());
        _ = draft;

        var result = await service.ListAsync(categoryId: null, audience: null, status: null, canManage: false, canSeeInternal: false);

        var item = Assert.Single(result);
        Assert.Equal("Public", item.Title);
    }

    [Fact]
    public async Task GetAsync_returns_404_shape_for_a_customer_requesting_an_internal_only_item_by_id()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var service = new KbSolutionsService(db);
        var created = await service.CreateAsync(new CreateKbSolutionRequest("Title", "P", "F", categoryId, KnowledgeBaseAudience.Internal), Guid.NewGuid());
        await service.PublishAsync(created.Solution!.Id, Guid.NewGuid());

        var asCustomer = await service.GetAsync(created.Solution.Id, canManage: false, canSeeInternal: false);

        Assert.Null(asCustomer);
    }

    [Fact]
    public async Task Create_rejects_an_unknown_category()
    {
        await using var db = CreateDb();
        var service = new KbSolutionsService(db);

        var result = await service.CreateAsync(
            new CreateKbSolutionRequest("Title", "P", "F", Guid.NewGuid(), KnowledgeBaseAudience.CustomerFacing), Guid.NewGuid());

        Assert.Equal(KbSolutionOperationOutcome.CategoryNotFound, result.Outcome);
    }

    [Fact]
    public async Task Create_rejects_an_empty_title_problem_or_solution_body()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var service = new KbSolutionsService(db);

        Assert.Equal(KbSolutionOperationOutcome.InvalidTitle,
            (await service.CreateAsync(new CreateKbSolutionRequest("   ", "P", "F", categoryId, KnowledgeBaseAudience.CustomerFacing), Guid.NewGuid())).Outcome);
        Assert.Equal(KbSolutionOperationOutcome.InvalidProblem,
            (await service.CreateAsync(new CreateKbSolutionRequest("Title", "   ", "F", categoryId, KnowledgeBaseAudience.CustomerFacing), Guid.NewGuid())).Outcome);
        Assert.Equal(KbSolutionOperationOutcome.InvalidSolutionBody,
            (await service.CreateAsync(new CreateKbSolutionRequest("Title", "P", "   ", categoryId, KnowledgeBaseAudience.CustomerFacing), Guid.NewGuid())).Outcome);
    }
}
