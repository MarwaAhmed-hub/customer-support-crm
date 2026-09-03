using CustomerSupportCrm.Api.KnowledgeBase;
using CustomerSupportCrm.Domain.KnowledgeBase;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CustomerSupportCrm.Api.Tests.KnowledgeBase;

public class KnowledgeBaseArticlesServiceTests
{
    private static CrmDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<Guid> CreateCategoryAsync(CrmDbContext db, string name = "General") =>
        (await new KnowledgeBaseCategoriesService(db).CreateAsync(new CreateKnowledgeBaseCategoryRequest(name))).Category!.Id;

    [Fact]
    public async Task Create_always_starts_as_draft()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var service = new KnowledgeBaseArticlesService(db);

        var result = await service.CreateAsync(
            new CreateKnowledgeBaseArticleRequest(KnowledgeBaseContentType.Faq, KnowledgeBaseAudience.CustomerFacing, "How do I reset my password?", "Click forgot password.", categoryId),
            Guid.NewGuid());

        Assert.Equal(KnowledgeBaseArticleOperationOutcome.Success, result.Outcome);
        Assert.Equal(KnowledgeBasePublicationStatus.Draft, result.Article!.Status);
        Assert.Null(result.Article.PublishedAtUtc);
    }

    [Fact]
    public async Task Create_rejects_an_empty_title()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var service = new KnowledgeBaseArticlesService(db);

        var result = await service.CreateAsync(
            new CreateKnowledgeBaseArticleRequest(KnowledgeBaseContentType.Faq, KnowledgeBaseAudience.CustomerFacing, "   ", "Body", categoryId),
            Guid.NewGuid());

        Assert.Equal(KnowledgeBaseArticleOperationOutcome.InvalidTitle, result.Outcome);
    }

    [Fact]
    public async Task Create_rejects_an_empty_body()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var service = new KnowledgeBaseArticlesService(db);

        var result = await service.CreateAsync(
            new CreateKnowledgeBaseArticleRequest(KnowledgeBaseContentType.Faq, KnowledgeBaseAudience.CustomerFacing, "Title", "   ", categoryId),
            Guid.NewGuid());

        Assert.Equal(KnowledgeBaseArticleOperationOutcome.InvalidBody, result.Outcome);
    }

    [Fact]
    public async Task Create_rejects_an_unknown_category()
    {
        await using var db = CreateDb();
        var service = new KnowledgeBaseArticlesService(db);

        var result = await service.CreateAsync(
            new CreateKnowledgeBaseArticleRequest(KnowledgeBaseContentType.Faq, KnowledgeBaseAudience.CustomerFacing, "Title", "Body", Guid.NewGuid()),
            Guid.NewGuid());

        Assert.Equal(KnowledgeBaseArticleOperationOutcome.CategoryNotFound, result.Outcome);
    }

    [Fact]
    public async Task Update_never_changes_content_type()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var service = new KnowledgeBaseArticlesService(db);
        var created = await service.CreateAsync(
            new CreateKnowledgeBaseArticleRequest(KnowledgeBaseContentType.Faq, KnowledgeBaseAudience.CustomerFacing, "Title", "Body", categoryId),
            Guid.NewGuid());

        var updated = await service.UpdateAsync(
            created.Article!.Id,
            new UpdateKnowledgeBaseArticleRequest(KnowledgeBaseAudience.Internal, "New title", "New body", categoryId),
            Guid.NewGuid());

        Assert.Equal(KnowledgeBaseArticleOperationOutcome.Success, updated.Outcome);
        Assert.Equal(KnowledgeBaseContentType.Faq, updated.Article!.ContentType);
        Assert.Equal(KnowledgeBaseAudience.Internal, updated.Article.Audience);
        Assert.Equal("New title", updated.Article.Title);
    }

    [Fact]
    public async Task Update_rejects_an_unknown_id()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var service = new KnowledgeBaseArticlesService(db);

        var result = await service.UpdateAsync(
            Guid.NewGuid(),
            new UpdateKnowledgeBaseArticleRequest(KnowledgeBaseAudience.CustomerFacing, "Title", "Body", categoryId),
            Guid.NewGuid());

        Assert.Equal(KnowledgeBaseArticleOperationOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task Update_rejects_an_unknown_category()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var service = new KnowledgeBaseArticlesService(db);
        var created = await service.CreateAsync(
            new CreateKnowledgeBaseArticleRequest(KnowledgeBaseContentType.Faq, KnowledgeBaseAudience.CustomerFacing, "Title", "Body", categoryId),
            Guid.NewGuid());

        var result = await service.UpdateAsync(
            created.Article!.Id,
            new UpdateKnowledgeBaseArticleRequest(KnowledgeBaseAudience.CustomerFacing, "Title", "Body", Guid.NewGuid()),
            Guid.NewGuid());

        Assert.Equal(KnowledgeBaseArticleOperationOutcome.CategoryNotFound, result.Outcome);
    }

    [Fact]
    public async Task Publish_is_idempotent_and_keeps_the_original_published_at()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var service = new KnowledgeBaseArticlesService(db);
        var created = await service.CreateAsync(
            new CreateKnowledgeBaseArticleRequest(KnowledgeBaseContentType.HelpArticle, KnowledgeBaseAudience.CustomerFacing, "Title", "Body", categoryId),
            Guid.NewGuid());

        var firstPublish = await service.PublishAsync(created.Article!.Id, Guid.NewGuid());
        Assert.Equal(KnowledgeBasePublicationStatus.Published, firstPublish.Article!.Status);
        var firstPublishedAt = firstPublish.Article.PublishedAtUtc;
        Assert.NotNull(firstPublishedAt);

        var secondPublish = await service.PublishAsync(created.Article.Id, Guid.NewGuid());

        Assert.Equal(KnowledgeBasePublicationStatus.Published, secondPublish.Article!.Status);
        Assert.Equal(firstPublishedAt, secondPublish.Article.PublishedAtUtc);
    }

    [Fact]
    public async Task Unpublish_is_idempotent_on_an_already_draft_article()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var service = new KnowledgeBaseArticlesService(db);
        var created = await service.CreateAsync(
            new CreateKnowledgeBaseArticleRequest(KnowledgeBaseContentType.Faq, KnowledgeBaseAudience.CustomerFacing, "Title", "Body", categoryId),
            Guid.NewGuid());

        var result = await service.UnpublishAsync(created.Article!.Id, Guid.NewGuid());

        Assert.Equal(KnowledgeBasePublicationStatus.Draft, result.Article!.Status);
        Assert.Null(result.Article.PublishedAtUtc);
    }

    [Fact]
    public async Task Unpublish_clears_published_at_and_reverts_to_draft()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var service = new KnowledgeBaseArticlesService(db);
        var created = await service.CreateAsync(
            new CreateKnowledgeBaseArticleRequest(KnowledgeBaseContentType.Faq, KnowledgeBaseAudience.CustomerFacing, "Title", "Body", categoryId),
            Guid.NewGuid());
        await service.PublishAsync(created.Article!.Id, Guid.NewGuid());

        var result = await service.UnpublishAsync(created.Article.Id, Guid.NewGuid());

        Assert.Equal(KnowledgeBasePublicationStatus.Draft, result.Article!.Status);
        Assert.Null(result.Article.PublishedAtUtc);
    }

    [Fact]
    public async Task Delete_removes_the_article_and_reports_success()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var service = new KnowledgeBaseArticlesService(db);
        var created = await service.CreateAsync(
            new CreateKnowledgeBaseArticleRequest(KnowledgeBaseContentType.Faq, KnowledgeBaseAudience.CustomerFacing, "Title", "Body", categoryId),
            Guid.NewGuid());

        var deleted = await service.DeleteAsync(created.Article!.Id);

        Assert.True(deleted);
        Assert.Null(await service.GetAsync(created.Article.Id, canManage: true, canSeeInternal: true));
    }

    [Fact]
    public async Task Delete_reports_false_for_an_unknown_id()
    {
        await using var db = CreateDb();
        var service = new KnowledgeBaseArticlesService(db);

        Assert.False(await service.DeleteAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Get_hides_a_draft_article_from_a_non_manager_as_a_404_not_a_403()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var service = new KnowledgeBaseArticlesService(db);
        var created = await service.CreateAsync(
            new CreateKnowledgeBaseArticleRequest(KnowledgeBaseContentType.Faq, KnowledgeBaseAudience.CustomerFacing, "Title", "Body", categoryId),
            Guid.NewGuid());

        var asAgent = await service.GetAsync(created.Article!.Id, canManage: false, canSeeInternal: true);
        var asManager = await service.GetAsync(created.Article.Id, canManage: true, canSeeInternal: false);

        Assert.Null(asAgent);
        Assert.NotNull(asManager);
    }

    [Fact]
    public async Task Get_hides_a_published_internal_article_from_a_caller_without_view_internal()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var service = new KnowledgeBaseArticlesService(db);
        var created = await service.CreateAsync(
            new CreateKnowledgeBaseArticleRequest(KnowledgeBaseContentType.HelpArticle, KnowledgeBaseAudience.Internal, "Title", "Body", categoryId),
            Guid.NewGuid());
        await service.PublishAsync(created.Article!.Id, Guid.NewGuid());

        var asCustomer = await service.GetAsync(created.Article.Id, canManage: false, canSeeInternal: false);
        var asAgent = await service.GetAsync(created.Article.Id, canManage: false, canSeeInternal: true);

        Assert.Null(asCustomer);
        Assert.NotNull(asAgent);
    }

    [Fact]
    public async Task Get_exposes_a_published_customer_facing_article_to_everyone()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var service = new KnowledgeBaseArticlesService(db);
        var created = await service.CreateAsync(
            new CreateKnowledgeBaseArticleRequest(KnowledgeBaseContentType.Faq, KnowledgeBaseAudience.CustomerFacing, "Title", "Body", categoryId),
            Guid.NewGuid());
        await service.PublishAsync(created.Article!.Id, Guid.NewGuid());

        var asCustomer = await service.GetAsync(created.Article.Id, canManage: false, canSeeInternal: false);

        Assert.NotNull(asCustomer);
    }

    [Fact]
    public async Task List_only_returns_published_customer_facing_articles_to_a_customer()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var service = new KnowledgeBaseArticlesService(db);

        var draft = await service.CreateAsync(
            new CreateKnowledgeBaseArticleRequest(KnowledgeBaseContentType.Faq, KnowledgeBaseAudience.CustomerFacing, "Draft", "Body", categoryId), Guid.NewGuid());
        var publishedInternal = await service.CreateAsync(
            new CreateKnowledgeBaseArticleRequest(KnowledgeBaseContentType.Faq, KnowledgeBaseAudience.Internal, "Internal", "Body", categoryId), Guid.NewGuid());
        await service.PublishAsync(publishedInternal.Article!.Id, Guid.NewGuid());
        var publishedCustomerFacing = await service.CreateAsync(
            new CreateKnowledgeBaseArticleRequest(KnowledgeBaseContentType.Faq, KnowledgeBaseAudience.CustomerFacing, "Public", "Body", categoryId), Guid.NewGuid());
        await service.PublishAsync(publishedCustomerFacing.Article!.Id, Guid.NewGuid());
        _ = draft;

        var result = await service.ListAsync(contentType: null, categoryId: null, audience: null, status: null, canManage: false, canSeeInternal: false);

        var article = Assert.Single(result);
        Assert.Equal("Public", article.Title);
    }

    [Fact]
    public async Task List_honors_an_explicit_filter_from_a_manager()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var service = new KnowledgeBaseArticlesService(db);
        await service.CreateAsync(
            new CreateKnowledgeBaseArticleRequest(KnowledgeBaseContentType.Faq, KnowledgeBaseAudience.CustomerFacing, "A Faq", "Body", categoryId), Guid.NewGuid());
        await service.CreateAsync(
            new CreateKnowledgeBaseArticleRequest(KnowledgeBaseContentType.HelpArticle, KnowledgeBaseAudience.CustomerFacing, "An Article", "Body", categoryId), Guid.NewGuid());

        var result = await service.ListAsync(
            contentType: KnowledgeBaseContentType.HelpArticle, categoryId: null, audience: null, status: KnowledgeBasePublicationStatus.Draft,
            canManage: true, canSeeInternal: true);

        var article = Assert.Single(result);
        Assert.Equal("An Article", article.Title);
    }
}
