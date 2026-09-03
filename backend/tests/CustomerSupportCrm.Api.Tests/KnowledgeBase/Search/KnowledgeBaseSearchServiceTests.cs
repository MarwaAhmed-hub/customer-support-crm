using CustomerSupportCrm.Api.KnowledgeBase;
using CustomerSupportCrm.Api.KnowledgeBase.Guides;
using CustomerSupportCrm.Api.KnowledgeBase.Search;
using CustomerSupportCrm.Api.KnowledgeBase.Solutions;
using CustomerSupportCrm.Domain.KnowledgeBase;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CustomerSupportCrm.Api.Tests.KnowledgeBase.Search;

public class KnowledgeBaseSearchServiceTests
{
    private static readonly KnowledgeBaseSearchVisibility InternalVisibility =
        new(ArticlesView: true, ArticlesViewInternal: true, SolutionsView: true, SolutionsViewInternal: true, GuidesView: true, GuidesViewInternal: true);

    private static readonly KnowledgeBaseSearchVisibility CustomerVisibility =
        new(ArticlesView: true, ArticlesViewInternal: false, SolutionsView: true, SolutionsViewInternal: false, GuidesView: true, GuidesViewInternal: false);

    private static CrmDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static async Task<Guid> CreateCategoryAsync(CrmDbContext db, string name = "General") =>
        (await new KnowledgeBaseCategoriesService(db).CreateAsync(new CreateKnowledgeBaseCategoryRequest(name))).Category!.Id;

    private static KnowledgeBaseSearchQuery Query(string? q = null, IReadOnlyCollection<KnowledgeBaseSearchContentType>? types = null, Guid? categoryId = null, int page = 1, int pageSize = 20) =>
        new(q, types, categoryId, page, pageSize);

    [Fact]
    public async Task Search_with_empty_query_and_no_filters_returns_no_results()
    {
        await using var db = CreateDb();
        var service = new KnowledgeBaseSearchService(db);

        var result = await service.SearchAsync(Query(q: "   "), InternalVisibility);

        Assert.Equal(0, result.Total);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Search_with_no_query_but_a_filter_still_browses()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var articles = new KnowledgeBaseArticlesService(db);
        var published = await articles.CreateAsync(new CreateKnowledgeBaseArticleRequest(KnowledgeBaseContentType.Faq, KnowledgeBaseAudience.CustomerFacing, "Q", "A", categoryId), Guid.NewGuid());
        await articles.PublishAsync(published.Article!.Id, Guid.NewGuid());
        var service = new KnowledgeBaseSearchService(db);

        var result = await service.SearchAsync(Query(categoryId: categoryId), InternalVisibility);

        Assert.Equal(1, result.Total);
    }

    [Fact]
    public async Task Search_returns_faq_matches_on_question_or_answer()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var articles = new KnowledgeBaseArticlesService(db);
        var byQuestion = await articles.CreateAsync(new CreateKnowledgeBaseArticleRequest(KnowledgeBaseContentType.Faq, KnowledgeBaseAudience.CustomerFacing, "How do I reset my password?", "Click forgot password.", categoryId), Guid.NewGuid());
        await articles.PublishAsync(byQuestion.Article!.Id, Guid.NewGuid());
        var byAnswer = await articles.CreateAsync(new CreateKnowledgeBaseArticleRequest(KnowledgeBaseContentType.Faq, KnowledgeBaseAudience.CustomerFacing, "Billing question", "Update your password on the billing page.", categoryId), Guid.NewGuid());
        await articles.PublishAsync(byAnswer.Article!.Id, Guid.NewGuid());
        var service = new KnowledgeBaseSearchService(db);

        var result = await service.SearchAsync(Query(q: "password"), InternalVisibility);

        Assert.Equal(2, result.Total);
        Assert.All(result.Items, item => Assert.Equal(KnowledgeBaseSearchContentType.Faq, item.Type));
    }

    [Fact]
    public async Task Search_returns_article_matches_on_title_or_content()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var articles = new KnowledgeBaseArticlesService(db);
        var created = await articles.CreateAsync(new CreateKnowledgeBaseArticleRequest(KnowledgeBaseContentType.HelpArticle, KnowledgeBaseAudience.CustomerFacing, "Onboarding checklist", "Step through account setup.", categoryId), Guid.NewGuid());
        await articles.PublishAsync(created.Article!.Id, Guid.NewGuid());
        var service = new KnowledgeBaseSearchService(db);

        var result = await service.SearchAsync(Query(q: "onboarding"), InternalVisibility);

        var item = Assert.Single(result.Items);
        Assert.Equal(KnowledgeBaseSearchContentType.Article, item.Type);
    }

    [Fact]
    public async Task Search_returns_solution_matches_on_title_problem_or_solution_body()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var solutions = new KbSolutionsService(db);
        var created = await solutions.CreateAsync(new CreateKbSolutionRequest("Printer offline", "The printer shows offline", "Restart the print spooler service", categoryId, KnowledgeBaseAudience.CustomerFacing), Guid.NewGuid());
        await solutions.PublishAsync(created.Solution!.Id, Guid.NewGuid());
        var service = new KnowledgeBaseSearchService(db);

        Assert.Single((await service.SearchAsync(Query(q: "printer"), InternalVisibility)).Items);
        Assert.Single((await service.SearchAsync(Query(q: "offline"), InternalVisibility)).Items);
        Assert.Single((await service.SearchAsync(Query(q: "spooler"), InternalVisibility)).Items);
    }

    [Fact]
    public async Task Search_returns_guide_matches_on_title_description_or_step_text()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var guides = new KbGuidesService(db);
        var created = await guides.CreateAsync(
            new CreateKbGuideRequest("Set up two-factor auth", "Enable 2FA on your account", categoryId, KnowledgeBaseAudience.CustomerFacing, [new KbGuideStepInput("Open the security settings page"), new KbGuideStepInput("Scan the QR code")]),
            Guid.NewGuid());
        await guides.PublishAsync(created.Guide!.Id, Guid.NewGuid());
        var service = new KnowledgeBaseSearchService(db);

        Assert.Single((await service.SearchAsync(Query(q: "two-factor"), InternalVisibility)).Items);
        Assert.Single((await service.SearchAsync(Query(q: "2FA"), InternalVisibility)).Items);
        Assert.Single((await service.SearchAsync(Query(q: "QR code"), InternalVisibility)).Items);
    }

    [Fact]
    public async Task Search_excludes_draft_items_across_all_content_types()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        await new KnowledgeBaseArticlesService(db).CreateAsync(new CreateKnowledgeBaseArticleRequest(KnowledgeBaseContentType.Faq, KnowledgeBaseAudience.CustomerFacing, "widget question", "widget answer", categoryId), Guid.NewGuid());
        await new KbSolutionsService(db).CreateAsync(new CreateKbSolutionRequest("widget solution", "widget problem", "widget fix", categoryId, KnowledgeBaseAudience.CustomerFacing), Guid.NewGuid());
        await new KbGuidesService(db).CreateAsync(new CreateKbGuideRequest("widget guide", "widget description", categoryId, KnowledgeBaseAudience.CustomerFacing, [new KbGuideStepInput("widget step")]), Guid.NewGuid());
        var service = new KnowledgeBaseSearchService(db);

        var result = await service.SearchAsync(Query(q: "widget"), InternalVisibility);

        Assert.Equal(0, result.Total);
        Assert.Empty(result.Items);
    }

    [Fact]
    public async Task Search_excludes_internal_only_items_for_a_caller_without_view_internal()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var articles = new KnowledgeBaseArticlesService(db);
        var internalOnly = await articles.CreateAsync(new CreateKnowledgeBaseArticleRequest(KnowledgeBaseContentType.Faq, KnowledgeBaseAudience.Internal, "gadget question", "gadget answer", categoryId), Guid.NewGuid());
        await articles.PublishAsync(internalOnly.Article!.Id, Guid.NewGuid());
        var service = new KnowledgeBaseSearchService(db);

        var asCustomer = await service.SearchAsync(Query(q: "gadget"), CustomerVisibility);
        var asInternal = await service.SearchAsync(Query(q: "gadget"), InternalVisibility);

        Assert.Equal(0, asCustomer.Total);
        Assert.Empty(asCustomer.Items);
        Assert.Equal(1, asInternal.Total);
    }

    [Fact]
    public async Task Search_includes_customer_facing_and_internal_for_an_internal_caller()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var articles = new KnowledgeBaseArticlesService(db);
        var internalOnly = await articles.CreateAsync(new CreateKnowledgeBaseArticleRequest(KnowledgeBaseContentType.Faq, KnowledgeBaseAudience.Internal, "gizmo internal", "gizmo answer", categoryId), Guid.NewGuid());
        await articles.PublishAsync(internalOnly.Article!.Id, Guid.NewGuid());
        var customerFacing = await articles.CreateAsync(new CreateKnowledgeBaseArticleRequest(KnowledgeBaseContentType.Faq, KnowledgeBaseAudience.CustomerFacing, "gizmo public", "gizmo answer", categoryId), Guid.NewGuid());
        await articles.PublishAsync(customerFacing.Article!.Id, Guid.NewGuid());
        var service = new KnowledgeBaseSearchService(db);

        var result = await service.SearchAsync(Query(q: "gizmo"), InternalVisibility);

        Assert.Equal(2, result.Total);
    }

    [Fact]
    public async Task Search_type_filter_limits_to_the_requested_types()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var articles = new KnowledgeBaseArticlesService(db);
        var faq = await articles.CreateAsync(new CreateKnowledgeBaseArticleRequest(KnowledgeBaseContentType.Faq, KnowledgeBaseAudience.CustomerFacing, "sprocket question", "sprocket answer", categoryId), Guid.NewGuid());
        await articles.PublishAsync(faq.Article!.Id, Guid.NewGuid());
        var solutions = new KbSolutionsService(db);
        var solution = await solutions.CreateAsync(new CreateKbSolutionRequest("sprocket solution", "sprocket problem", "sprocket fix", categoryId, KnowledgeBaseAudience.CustomerFacing), Guid.NewGuid());
        await solutions.PublishAsync(solution.Solution!.Id, Guid.NewGuid());
        var service = new KnowledgeBaseSearchService(db);

        var result = await service.SearchAsync(Query(q: "sprocket", types: [KnowledgeBaseSearchContentType.Solution]), InternalVisibility);

        var item = Assert.Single(result.Items);
        Assert.Equal(KnowledgeBaseSearchContentType.Solution, item.Type);
    }

    [Fact]
    public async Task Search_category_filter_limits_to_the_requested_category()
    {
        await using var db = CreateDb();
        var categoryA = await CreateCategoryAsync(db, "Billing");
        var categoryB = await CreateCategoryAsync(db, "Account");
        var articles = new KnowledgeBaseArticlesService(db);
        var inA = await articles.CreateAsync(new CreateKnowledgeBaseArticleRequest(KnowledgeBaseContentType.Faq, KnowledgeBaseAudience.CustomerFacing, "flange question", "flange answer", categoryA), Guid.NewGuid());
        await articles.PublishAsync(inA.Article!.Id, Guid.NewGuid());
        var inB = await articles.CreateAsync(new CreateKnowledgeBaseArticleRequest(KnowledgeBaseContentType.Faq, KnowledgeBaseAudience.CustomerFacing, "flange question two", "flange answer two", categoryB), Guid.NewGuid());
        await articles.PublishAsync(inB.Article!.Id, Guid.NewGuid());
        var service = new KnowledgeBaseSearchService(db);

        var result = await service.SearchAsync(Query(q: "flange", categoryId: categoryA), InternalVisibility);

        var item = Assert.Single(result.Items);
        Assert.Equal(categoryA, item.CategoryId);
    }

    [Fact]
    public async Task Search_page_size_is_clamped_to_fifty()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var articles = new KnowledgeBaseArticlesService(db);
        for (var i = 0; i < 3; i++)
        {
            var created = await articles.CreateAsync(new CreateKnowledgeBaseArticleRequest(KnowledgeBaseContentType.Faq, KnowledgeBaseAudience.CustomerFacing, $"cog question {i}", "cog answer", categoryId), Guid.NewGuid());
            await articles.PublishAsync(created.Article!.Id, Guid.NewGuid());
        }
        var service = new KnowledgeBaseSearchService(db);

        var result = await service.SearchAsync(Query(q: "cog", pageSize: 500), InternalVisibility);

        Assert.Equal(50, result.PageSize);
        Assert.Equal(3, result.Total);
    }

    [Fact]
    public async Task Search_page_is_clamped_to_at_least_one()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var articles = new KnowledgeBaseArticlesService(db);
        var created = await articles.CreateAsync(new CreateKnowledgeBaseArticleRequest(KnowledgeBaseContentType.Faq, KnowledgeBaseAudience.CustomerFacing, "lever question", "lever answer", categoryId), Guid.NewGuid());
        await articles.PublishAsync(created.Article!.Id, Guid.NewGuid());
        var service = new KnowledgeBaseSearchService(db);

        var result = await service.SearchAsync(Query(q: "lever", page: -5), InternalVisibility);

        Assert.Equal(1, result.Page);
        Assert.Single(result.Items);
    }

    [Fact]
    public async Task Search_is_case_insensitive()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var articles = new KnowledgeBaseArticlesService(db);
        var created = await articles.CreateAsync(new CreateKnowledgeBaseArticleRequest(KnowledgeBaseContentType.Faq, KnowledgeBaseAudience.CustomerFacing, "PASSWORD RESET", "answer", categoryId), Guid.NewGuid());
        await articles.PublishAsync(created.Article!.Id, Guid.NewGuid());
        var service = new KnowledgeBaseSearchService(db);

        var result = await service.SearchAsync(Query(q: "password reset"), InternalVisibility);

        Assert.Single(result.Items);
    }

    [Fact]
    public async Task Search_treats_percent_and_underscore_as_literal_characters_not_wildcards()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var articles = new KnowledgeBaseArticlesService(db);
        var literalMatch = await articles.CreateAsync(new CreateKnowledgeBaseArticleRequest(KnowledgeBaseContentType.Faq, KnowledgeBaseAudience.CustomerFacing, "Discount is 10%_off today", "answer", categoryId), Guid.NewGuid());
        await articles.PublishAsync(literalMatch.Article!.Id, Guid.NewGuid());
        var unrelated = await articles.CreateAsync(new CreateKnowledgeBaseArticleRequest(KnowledgeBaseContentType.Faq, KnowledgeBaseAudience.CustomerFacing, "Something entirely different", "answer text here", categoryId), Guid.NewGuid());
        await articles.PublishAsync(unrelated.Article!.Id, Guid.NewGuid());
        var service = new KnowledgeBaseSearchService(db);

        var result = await service.SearchAsync(Query(q: "10%_off"), InternalVisibility);

        Assert.Single(result.Items);
    }

    [Fact]
    public async Task Search_skips_a_content_type_entirely_when_the_caller_lacks_its_view_permission()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var solutions = new KbSolutionsService(db);
        var created = await solutions.CreateAsync(new CreateKbSolutionRequest("cable solution", "cable problem", "cable fix", categoryId, KnowledgeBaseAudience.CustomerFacing), Guid.NewGuid());
        await solutions.PublishAsync(created.Solution!.Id, Guid.NewGuid());
        var service = new KnowledgeBaseSearchService(db);
        var noSolutionsView = InternalVisibility with { SolutionsView = false };

        var result = await service.SearchAsync(Query(q: "cable"), noSolutionsView);

        Assert.Equal(0, result.Total);
    }

    [Fact]
    public async Task Search_orders_by_published_date_descending_then_title()
    {
        await using var db = CreateDb();
        var categoryId = await CreateCategoryAsync(db);
        var articles = new KnowledgeBaseArticlesService(db);
        var first = await articles.CreateAsync(new CreateKnowledgeBaseArticleRequest(KnowledgeBaseContentType.Faq, KnowledgeBaseAudience.CustomerFacing, "valve question A", "valve answer", categoryId), Guid.NewGuid());
        await articles.PublishAsync(first.Article!.Id, Guid.NewGuid());
        await Task.Delay(10);
        var second = await articles.CreateAsync(new CreateKnowledgeBaseArticleRequest(KnowledgeBaseContentType.Faq, KnowledgeBaseAudience.CustomerFacing, "valve question B", "valve answer", categoryId), Guid.NewGuid());
        await articles.PublishAsync(second.Article!.Id, Guid.NewGuid());
        var service = new KnowledgeBaseSearchService(db);

        var result = await service.SearchAsync(Query(q: "valve"), InternalVisibility);

        Assert.Equal(2, result.Items.Count);
        Assert.Equal("valve question B", result.Items[0].Title);
        Assert.Equal("valve question A", result.Items[1].Title);
    }
}
