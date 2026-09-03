using CustomerSupportCrm.Api.KnowledgeBase;
using CustomerSupportCrm.Api.KnowledgeBase.Guides;
using CustomerSupportCrm.Domain.KnowledgeBase;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CustomerSupportCrm.Api.Tests.KnowledgeBase.Guides;

public class KbGuidesServiceTests
{
    private static CrmDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<Guid> CreateCategoryAsync(CrmDbContext db, string name = "General") =>
        (await new KnowledgeBaseCategoriesService(db).CreateAsync(new CreateKnowledgeBaseCategoryRequest(name))).Category!.Id;

    private static CreateKbGuideRequest Request(Guid categoryId, KnowledgeBaseAudience audience, params string[] steps) =>
        new("Title", "Description", categoryId, audience, steps.Select(s => new KbGuideStepInput(s)).ToList());

    [Fact]
    public async Task Create_forces_draft_status()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var service = new KbGuidesService(db);

        var result = await service.CreateAsync(Request(categoryId, KnowledgeBaseAudience.CustomerFacing, "Step one", "Step two"), Guid.NewGuid());

        Assert.Equal(KbGuideOperationOutcome.Success, result.Outcome);
        Assert.Equal(KnowledgeBasePublicationStatus.Draft, result.Guide!.Status);
    }

    [Fact]
    public async Task Create_persists_steps_in_order()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var service = new KbGuidesService(db);

        var result = await service.CreateAsync(Request(categoryId, KnowledgeBaseAudience.CustomerFacing, "First", "Second", "Third"), Guid.NewGuid());

        Assert.Equal(3, result.Guide!.Steps.Count);
        Assert.Equal(["First", "Second", "Third"], result.Guide.Steps.OrderBy(s => s.Order).Select(s => s.Instruction).ToArray());
        Assert.Equal([0, 1, 2], result.Guide.Steps.OrderBy(s => s.Order).Select(s => s.Order).ToArray());
    }

    [Fact]
    public async Task Create_rejects_an_empty_steps_list()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var service = new KbGuidesService(db);

        var result = await service.CreateAsync(Request(categoryId, KnowledgeBaseAudience.CustomerFacing), Guid.NewGuid());

        Assert.Equal(KbGuideOperationOutcome.InvalidSteps, result.Outcome);
    }

    [Fact]
    public async Task Create_rejects_a_steps_list_that_is_entirely_whitespace()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var service = new KbGuidesService(db);

        var result = await service.CreateAsync(Request(categoryId, KnowledgeBaseAudience.CustomerFacing, "   ", "  "), Guid.NewGuid());

        Assert.Equal(KbGuideOperationOutcome.InvalidSteps, result.Outcome);
    }

    [Fact]
    public async Task Update_replaces_steps_and_preserves_new_order()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var service = new KbGuidesService(db);
        var created = await service.CreateAsync(Request(categoryId, KnowledgeBaseAudience.CustomerFacing, "A", "B", "C"), Guid.NewGuid());

        var updateRequest = new UpdateKbGuideRequest(
            "Title", "Description", categoryId, KnowledgeBaseAudience.CustomerFacing,
            [new KbGuideStepInput("C"), new KbGuideStepInput("A")]);
        var updated = await service.UpdateAsync(created.Guide!.Id, updateRequest, Guid.NewGuid());

        Assert.Equal(KbGuideOperationOutcome.Success, updated.Outcome);
        Assert.Equal(["C", "A"], updated.Guide!.Steps.OrderBy(s => s.Order).Select(s => s.Instruction).ToArray());
        Assert.Equal([0, 1], updated.Guide.Steps.OrderBy(s => s.Order).Select(s => s.Order).ToArray());
    }

    [Fact]
    public async Task Update_rejects_replacing_with_an_empty_steps_list()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var service = new KbGuidesService(db);
        var created = await service.CreateAsync(Request(categoryId, KnowledgeBaseAudience.CustomerFacing, "A"), Guid.NewGuid());

        var result = await service.UpdateAsync(
            created.Guide!.Id,
            new UpdateKbGuideRequest("Title", "Description", categoryId, KnowledgeBaseAudience.CustomerFacing, []),
            Guid.NewGuid());

        Assert.Equal(KbGuideOperationOutcome.InvalidSteps, result.Outcome);
    }

    [Fact]
    public async Task Publish_sets_published_at_and_unpublish_clears_it()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var service = new KbGuidesService(db);
        var created = await service.CreateAsync(Request(categoryId, KnowledgeBaseAudience.CustomerFacing, "A"), Guid.NewGuid());

        var published = await service.PublishAsync(created.Guide!.Id, Guid.NewGuid());
        Assert.NotNull(published.Guide!.PublishedAtUtc);

        var unpublished = await service.UnpublishAsync(created.Guide.Id, Guid.NewGuid());
        Assert.Equal(KnowledgeBasePublicationStatus.Draft, unpublished.Guide!.Status);
        Assert.Null(unpublished.Guide.PublishedAtUtc);
    }

    [Fact]
    public async Task GetAsync_returns_404_shape_when_agent_requests_a_draft()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var service = new KbGuidesService(db);
        var created = await service.CreateAsync(Request(categoryId, KnowledgeBaseAudience.CustomerFacing, "A"), Guid.NewGuid());

        var asAgent = await service.GetAsync(created.Guide!.Id, canManage: false, canSeeInternal: true);

        Assert.Null(asAgent);
    }

    [Fact]
    public async Task List_excludes_draft_and_internal_only_for_a_customer()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var service = new KbGuidesService(db);

        await service.CreateAsync(Request(categoryId, KnowledgeBaseAudience.CustomerFacing, "A"), Guid.NewGuid());
        var internalOnly = await service.CreateAsync(Request(categoryId, KnowledgeBaseAudience.Internal, "A"), Guid.NewGuid());
        await service.PublishAsync(internalOnly.Guide!.Id, Guid.NewGuid());
        var customerFacing = await service.CreateAsync(Request(categoryId, KnowledgeBaseAudience.CustomerFacing, "A"), Guid.NewGuid());
        await service.PublishAsync(customerFacing.Guide!.Id, Guid.NewGuid());

        var result = await service.ListAsync(categoryId: null, audience: null, status: null, canManage: false, canSeeInternal: false);

        Assert.Single(result);
        Assert.Equal(customerFacing.Guide.Id, result[0].Id);
    }

    [Fact]
    public async Task Create_rejects_an_unknown_category()
    {
        await using var db = CreateDb();
        var service = new KbGuidesService(db);

        var result = await service.CreateAsync(Request(Guid.NewGuid(), KnowledgeBaseAudience.CustomerFacing, "A"), Guid.NewGuid());

        Assert.Equal(KbGuideOperationOutcome.CategoryNotFound, result.Outcome);
    }

    [Fact]
    public async Task Delete_removes_the_guide_and_its_steps()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var service = new KbGuidesService(db);
        var created = await service.CreateAsync(Request(categoryId, KnowledgeBaseAudience.CustomerFacing, "A", "B"), Guid.NewGuid());

        var deleted = await service.DeleteAsync(created.Guide!.Id);

        Assert.True(deleted);
        Assert.Null(await service.GetAsync(created.Guide.Id, canManage: true, canSeeInternal: true));
        Assert.False(await db.KbGuideSteps.AnyAsync(s => s.GuideId == created.Guide.Id));
    }
}
