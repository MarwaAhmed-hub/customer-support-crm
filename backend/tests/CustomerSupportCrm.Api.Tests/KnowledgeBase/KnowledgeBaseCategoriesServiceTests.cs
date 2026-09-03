using CustomerSupportCrm.Api.KnowledgeBase;
using CustomerSupportCrm.Api.KnowledgeBase.Guides;
using CustomerSupportCrm.Api.KnowledgeBase.Solutions;
using CustomerSupportCrm.Domain.KnowledgeBase;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CustomerSupportCrm.Api.Tests.KnowledgeBase;

public class KnowledgeBaseCategoriesServiceTests
{
    private static CrmDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task Create_succeeds_with_a_valid_name()
    {
        await using var db = CreateDb();
        var service = new KnowledgeBaseCategoriesService(db);

        var result = await service.CreateAsync(new CreateKnowledgeBaseCategoryRequest("Billing"));

        Assert.Equal(KnowledgeBaseCategoryOperationOutcome.Success, result.Outcome);
        Assert.Equal("Billing", result.Category!.Name);
        Assert.True(result.Category.IsActive);
    }

    [Fact]
    public async Task Create_rejects_an_empty_name()
    {
        await using var db = CreateDb();
        var service = new KnowledgeBaseCategoriesService(db);

        var result = await service.CreateAsync(new CreateKnowledgeBaseCategoryRequest("   "));

        Assert.Equal(KnowledgeBaseCategoryOperationOutcome.InvalidName, result.Outcome);
    }

    [Fact]
    public async Task Create_rejects_a_duplicate_name_case_insensitively()
    {
        await using var db = CreateDb();
        var service = new KnowledgeBaseCategoriesService(db);
        await service.CreateAsync(new CreateKnowledgeBaseCategoryRequest("Billing"));

        var result = await service.CreateAsync(new CreateKnowledgeBaseCategoryRequest("billing"));

        Assert.Equal(KnowledgeBaseCategoryOperationOutcome.DuplicateName, result.Outcome);
    }

    [Fact]
    public async Task Update_can_rename_and_deactivate()
    {
        await using var db = CreateDb();
        var service = new KnowledgeBaseCategoriesService(db);
        var created = await service.CreateAsync(new CreateKnowledgeBaseCategoryRequest("General"));

        var result = await service.UpdateAsync(created.Category!.Id, new UpdateKnowledgeBaseCategoryRequest("General FAQs", false));

        Assert.Equal(KnowledgeBaseCategoryOperationOutcome.Success, result.Outcome);
        Assert.Equal("General FAQs", result.Category!.Name);
        Assert.False(result.Category.IsActive);
    }

    [Fact]
    public async Task Update_rejects_an_unknown_id()
    {
        await using var db = CreateDb();
        var service = new KnowledgeBaseCategoriesService(db);

        var result = await service.UpdateAsync(Guid.NewGuid(), new UpdateKnowledgeBaseCategoryRequest("General", true));

        Assert.Equal(KnowledgeBaseCategoryOperationOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task Update_rejects_renaming_to_another_categorys_name()
    {
        await using var db = CreateDb();
        var service = new KnowledgeBaseCategoriesService(db);
        await service.CreateAsync(new CreateKnowledgeBaseCategoryRequest("Billing"));
        var created = await service.CreateAsync(new CreateKnowledgeBaseCategoryRequest("Account"));

        var result = await service.UpdateAsync(created.Category!.Id, new UpdateKnowledgeBaseCategoryRequest("billing", true));

        Assert.Equal(KnowledgeBaseCategoryOperationOutcome.DuplicateName, result.Outcome);
    }

    [Fact]
    public async Task Delete_succeeds_when_no_article_references_the_category()
    {
        await using var db = CreateDb();
        var service = new KnowledgeBaseCategoriesService(db);
        var created = await service.CreateAsync(new CreateKnowledgeBaseCategoryRequest("General"));

        var result = await service.DeleteAsync(created.Category!.Id);

        Assert.Equal(KnowledgeBaseCategoryOperationOutcome.Success, result.Outcome);
        Assert.Null(await service.GetAsync(created.Category.Id));
    }

    [Fact]
    public async Task Delete_is_blocked_when_an_article_references_the_category()
    {
        await using var db = CreateDb();
        var categories = new KnowledgeBaseCategoriesService(db);
        var articles = new KnowledgeBaseArticlesService(db);
        var category = await categories.CreateAsync(new CreateKnowledgeBaseCategoryRequest("General"));
        await articles.CreateAsync(
            new CreateKnowledgeBaseArticleRequest(
                KnowledgeBaseContentType.Faq,
                KnowledgeBaseAudience.CustomerFacing,
                "How do I reset my password?", "Click forgot password.", category.Category!.Id),
            Guid.NewGuid());

        var result = await categories.DeleteAsync(category.Category.Id);

        Assert.Equal(KnowledgeBaseCategoryOperationOutcome.ReferencedByContent, result.Outcome);
    }

    [Fact]
    public async Task Delete_rejects_an_unknown_id()
    {
        await using var db = CreateDb();
        var service = new KnowledgeBaseCategoriesService(db);

        var result = await service.DeleteAsync(Guid.NewGuid());

        Assert.Equal(KnowledgeBaseCategoryOperationOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task Delete_is_blocked_when_a_solution_references_the_category()
    {
        await using var db = CreateDb();
        var categories = new KnowledgeBaseCategoriesService(db);
        var solutions = new KbSolutionsService(db);
        var category = await categories.CreateAsync(new CreateKnowledgeBaseCategoryRequest("General"));
        await solutions.CreateAsync(
            new CreateKbSolutionRequest("Title", "Problem", "Fix", category.Category!.Id, KnowledgeBaseAudience.CustomerFacing),
            Guid.NewGuid());

        var result = await categories.DeleteAsync(category.Category.Id);

        Assert.Equal(KnowledgeBaseCategoryOperationOutcome.ReferencedByContent, result.Outcome);
    }

    [Fact]
    public async Task Delete_is_blocked_when_a_guide_references_the_category()
    {
        await using var db = CreateDb();
        var categories = new KnowledgeBaseCategoriesService(db);
        var guides = new KbGuidesService(db);
        var category = await categories.CreateAsync(new CreateKnowledgeBaseCategoryRequest("General"));
        await guides.CreateAsync(
            new CreateKbGuideRequest("Title", "Description", category.Category!.Id, KnowledgeBaseAudience.CustomerFacing, [new KbGuideStepInput("Step one")]),
            Guid.NewGuid());

        var result = await categories.DeleteAsync(category.Category.Id);

        Assert.Equal(KnowledgeBaseCategoryOperationOutcome.ReferencedByContent, result.Outcome);
    }
}
