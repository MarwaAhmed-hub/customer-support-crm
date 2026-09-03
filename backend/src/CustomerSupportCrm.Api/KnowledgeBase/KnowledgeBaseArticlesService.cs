using System.Linq.Expressions;
using CustomerSupportCrm.Domain.KnowledgeBase;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupportCrm.Api.KnowledgeBase;

public sealed class KnowledgeBaseArticlesService(CrmDbContext db) : IKnowledgeBaseArticlesService
{
    private static readonly Expression<Func<KnowledgeBaseArticle, KnowledgeBaseArticleDto>> ToDtoExpression = a =>
        new KnowledgeBaseArticleDto(
            a.Id, a.ContentType, a.Audience, a.Status, a.Title, a.Body, a.CategoryId, a.Category!.Name,
            a.CreatedAtUtc, a.UpdatedAtUtc, a.PublishedAtUtc);

    public async Task<IReadOnlyList<KnowledgeBaseArticleDto>> ListAsync(
        KnowledgeBaseContentType? contentType, Guid? categoryId, KnowledgeBaseAudience? audience, KnowledgeBasePublicationStatus? status,
        bool canManage, bool canSeeInternal, CancellationToken cancellationToken = default)
    {
        var (effectiveStatus, effectiveAudience) = ApplyVisibility(status, audience, canManage, canSeeInternal);

        var query = db.KnowledgeBaseArticles.AsNoTracking().AsQueryable();

        if (contentType.HasValue)
        {
            query = query.Where(a => a.ContentType == contentType.Value);
        }

        if (categoryId.HasValue)
        {
            query = query.Where(a => a.CategoryId == categoryId.Value);
        }

        if (effectiveStatus.HasValue)
        {
            query = query.Where(a => a.Status == effectiveStatus.Value);
        }

        if (effectiveAudience.HasValue)
        {
            query = query.Where(a => a.Audience == effectiveAudience.Value);
        }

        return await query
            .OrderByDescending(a => a.CreatedAtUtc)
            .Select(ToDtoExpression)
            .ToListAsync(cancellationToken);
    }

    public async Task<KnowledgeBaseArticleDto?> GetAsync(Guid id, bool canManage, bool canSeeInternal, CancellationToken cancellationToken = default)
    {
        var article = await db.KnowledgeBaseArticles
            .AsNoTracking()
            .Where(a => a.Id == id)
            .Select(ToDtoExpression)
            .SingleOrDefaultAsync(cancellationToken);

        if (article is null)
        {
            return null;
        }

        if (canManage)
        {
            return article;
        }

        // Leak-safe: a Draft or an Internal item (without ViewInternal) is reported exactly like a
        // nonexistent one — never a 403, which would confirm the id is real.
        if (article.Status != KnowledgeBasePublicationStatus.Published)
        {
            return null;
        }

        if (article.Audience == KnowledgeBaseAudience.Internal && !canSeeInternal)
        {
            return null;
        }

        return article;
    }

    public async Task<KnowledgeBaseArticleResult> CreateAsync(CreateKnowledgeBaseArticleRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var title = request.Title.Trim();
        if (title.Length == 0)
        {
            return KnowledgeBaseArticleResult.InvalidTitle;
        }

        var body = request.Body.Trim();
        if (body.Length == 0)
        {
            return KnowledgeBaseArticleResult.InvalidBody;
        }

        if (!await db.KnowledgeBaseCategories.AnyAsync(c => c.Id == request.CategoryId, cancellationToken))
        {
            return KnowledgeBaseArticleResult.CategoryNotFound;
        }

        var article = new KnowledgeBaseArticle
        {
            ContentType = request.ContentType,
            Audience = request.Audience,
            Status = KnowledgeBasePublicationStatus.Draft,
            Title = title,
            Body = body,
            CategoryId = request.CategoryId,
            CreatedByUserId = actorUserId,
        };

        db.KnowledgeBaseArticles.Add(article);
        await db.SaveChangesAsync(cancellationToken);

        return KnowledgeBaseArticleResult.Success((await LoadDtoAsync(article.Id, cancellationToken))!);
    }

    public async Task<KnowledgeBaseArticleResult> UpdateAsync(Guid id, UpdateKnowledgeBaseArticleRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var article = await db.KnowledgeBaseArticles.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (article is null)
        {
            return KnowledgeBaseArticleResult.NotFound;
        }

        var title = request.Title.Trim();
        if (title.Length == 0)
        {
            return KnowledgeBaseArticleResult.InvalidTitle;
        }

        var body = request.Body.Trim();
        if (body.Length == 0)
        {
            return KnowledgeBaseArticleResult.InvalidBody;
        }

        if (!await db.KnowledgeBaseCategories.AnyAsync(c => c.Id == request.CategoryId, cancellationToken))
        {
            return KnowledgeBaseArticleResult.CategoryNotFound;
        }

        // ContentType is deliberately never assigned here — see UpdateKnowledgeBaseArticleRequest's
        // remarks; it has no ContentType property at all, so there is nothing to "ignore".
        article.Audience = request.Audience;
        article.Title = title;
        article.Body = body;
        article.CategoryId = request.CategoryId;
        article.UpdatedByUserId = actorUserId;
        article.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return KnowledgeBaseArticleResult.Success((await LoadDtoAsync(id, cancellationToken))!);
    }

    public async Task<KnowledgeBaseArticleResult> PublishAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var article = await db.KnowledgeBaseArticles.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (article is null)
        {
            return KnowledgeBaseArticleResult.NotFound;
        }

        if (article.Status != KnowledgeBasePublicationStatus.Published)
        {
            article.Status = KnowledgeBasePublicationStatus.Published;
            article.PublishedAtUtc = DateTime.UtcNow;
            article.PublishedByUserId = actorUserId;
            await db.SaveChangesAsync(cancellationToken);
        }
        // Already published: no-op — PublishedAtUtc keeps its original value, not reset to now.

        return KnowledgeBaseArticleResult.Success((await LoadDtoAsync(id, cancellationToken))!);
    }

    public async Task<KnowledgeBaseArticleResult> UnpublishAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var article = await db.KnowledgeBaseArticles.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (article is null)
        {
            return KnowledgeBaseArticleResult.NotFound;
        }

        if (article.Status != KnowledgeBasePublicationStatus.Draft)
        {
            article.Status = KnowledgeBasePublicationStatus.Draft;
            article.PublishedAtUtc = null;
            article.PublishedByUserId = null;
            await db.SaveChangesAsync(cancellationToken);
        }

        return KnowledgeBaseArticleResult.Success((await LoadDtoAsync(id, cancellationToken))!);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var article = await db.KnowledgeBaseArticles.SingleOrDefaultAsync(a => a.Id == id, cancellationToken);
        if (article is null)
        {
            return false;
        }

        db.KnowledgeBaseArticles.Remove(article);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private Task<KnowledgeBaseArticleDto?> LoadDtoAsync(Guid id, CancellationToken cancellationToken) =>
        db.KnowledgeBaseArticles.AsNoTracking().Where(a => a.Id == id).Select(ToDtoExpression).SingleOrDefaultAsync(cancellationToken);

    private static (KnowledgeBasePublicationStatus? Status, KnowledgeBaseAudience? Audience) ApplyVisibility(
        KnowledgeBasePublicationStatus? status, KnowledgeBaseAudience? audience, bool canManage, bool canSeeInternal)
    {
        if (canManage)
        {
            return (status, audience);
        }

        // A non-manager's requested status/audience are never honored beyond what they're allowed to
        // see — status is always forced to Published, and audience is forced to CustomerFacing unless
        // the caller holds ViewInternal.
        return (KnowledgeBasePublicationStatus.Published, canSeeInternal ? audience : KnowledgeBaseAudience.CustomerFacing);
    }
}
