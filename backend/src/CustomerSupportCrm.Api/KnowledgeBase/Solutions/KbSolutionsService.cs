using System.Linq.Expressions;
using CustomerSupportCrm.Domain.KnowledgeBase;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupportCrm.Api.KnowledgeBase.Solutions;

public sealed class KbSolutionsService(CrmDbContext db) : IKbSolutionsService
{
    private static readonly Expression<Func<KbSolution, KbSolutionDto>> ToDtoExpression = s =>
        new KbSolutionDto(
            s.Id, s.Title, s.Problem, s.SolutionBody, s.CategoryId, s.Category!.Name,
            s.Audience, s.Status, s.CreatedAtUtc, s.UpdatedAtUtc, s.PublishedAtUtc);

    public async Task<IReadOnlyList<KbSolutionDto>> ListAsync(
        Guid? categoryId, KnowledgeBaseAudience? audience, KnowledgeBasePublicationStatus? status,
        bool canManage, bool canSeeInternal, CancellationToken cancellationToken = default)
    {
        var (effectiveStatus, effectiveAudience) = ApplyVisibility(status, audience, canManage, canSeeInternal);

        var query = db.KbSolutions.AsNoTracking().AsQueryable();

        if (categoryId.HasValue)
        {
            query = query.Where(s => s.CategoryId == categoryId.Value);
        }

        if (effectiveStatus.HasValue)
        {
            query = query.Where(s => s.Status == effectiveStatus.Value);
        }

        if (effectiveAudience.HasValue)
        {
            query = query.Where(s => s.Audience == effectiveAudience.Value);
        }

        return await query
            .OrderByDescending(s => s.CreatedAtUtc)
            .Select(ToDtoExpression)
            .ToListAsync(cancellationToken);
    }

    public async Task<KbSolutionDto?> GetAsync(Guid id, bool canManage, bool canSeeInternal, CancellationToken cancellationToken = default)
    {
        var solution = await db.KbSolutions
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(ToDtoExpression)
            .SingleOrDefaultAsync(cancellationToken);

        if (solution is null)
        {
            return null;
        }

        if (canManage)
        {
            return solution;
        }

        if (solution.Status != KnowledgeBasePublicationStatus.Published)
        {
            return null;
        }

        if (solution.Audience == KnowledgeBaseAudience.Internal && !canSeeInternal)
        {
            return null;
        }

        return solution;
    }

    public async Task<KbSolutionResult> CreateAsync(CreateKbSolutionRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var title = request.Title.Trim();
        if (title.Length == 0)
        {
            return KbSolutionResult.InvalidTitle;
        }

        var problem = request.Problem.Trim();
        if (problem.Length == 0)
        {
            return KbSolutionResult.InvalidProblem;
        }

        var solutionBody = request.SolutionBody.Trim();
        if (solutionBody.Length == 0)
        {
            return KbSolutionResult.InvalidSolutionBody;
        }

        if (!await db.KnowledgeBaseCategories.AnyAsync(c => c.Id == request.CategoryId, cancellationToken))
        {
            return KbSolutionResult.CategoryNotFound;
        }

        var solution = new KbSolution
        {
            Title = title,
            Problem = problem,
            SolutionBody = solutionBody,
            CategoryId = request.CategoryId,
            Audience = request.Audience,
            Status = KnowledgeBasePublicationStatus.Draft,
            CreatedByUserId = actorUserId,
        };

        db.KbSolutions.Add(solution);
        await db.SaveChangesAsync(cancellationToken);

        return KbSolutionResult.Success((await LoadDtoAsync(solution.Id, cancellationToken))!);
    }

    public async Task<KbSolutionResult> UpdateAsync(Guid id, UpdateKbSolutionRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var solution = await db.KbSolutions.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (solution is null)
        {
            return KbSolutionResult.NotFound;
        }

        var title = request.Title.Trim();
        if (title.Length == 0)
        {
            return KbSolutionResult.InvalidTitle;
        }

        var problem = request.Problem.Trim();
        if (problem.Length == 0)
        {
            return KbSolutionResult.InvalidProblem;
        }

        var solutionBody = request.SolutionBody.Trim();
        if (solutionBody.Length == 0)
        {
            return KbSolutionResult.InvalidSolutionBody;
        }

        if (!await db.KnowledgeBaseCategories.AnyAsync(c => c.Id == request.CategoryId, cancellationToken))
        {
            return KbSolutionResult.CategoryNotFound;
        }

        solution.Title = title;
        solution.Problem = problem;
        solution.SolutionBody = solutionBody;
        solution.CategoryId = request.CategoryId;
        solution.Audience = request.Audience;
        solution.UpdatedByUserId = actorUserId;
        solution.UpdatedAtUtc = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return KbSolutionResult.Success((await LoadDtoAsync(id, cancellationToken))!);
    }

    public async Task<KbSolutionResult> PublishAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var solution = await db.KbSolutions.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (solution is null)
        {
            return KbSolutionResult.NotFound;
        }

        if (solution.Status != KnowledgeBasePublicationStatus.Published)
        {
            solution.Status = KnowledgeBasePublicationStatus.Published;
            solution.PublishedAtUtc = DateTime.UtcNow;
            solution.PublishedByUserId = actorUserId;
            await db.SaveChangesAsync(cancellationToken);
        }

        return KbSolutionResult.Success((await LoadDtoAsync(id, cancellationToken))!);
    }

    public async Task<KbSolutionResult> UnpublishAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var solution = await db.KbSolutions.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (solution is null)
        {
            return KbSolutionResult.NotFound;
        }

        if (solution.Status != KnowledgeBasePublicationStatus.Draft)
        {
            solution.Status = KnowledgeBasePublicationStatus.Draft;
            solution.PublishedAtUtc = null;
            solution.PublishedByUserId = null;
            await db.SaveChangesAsync(cancellationToken);
        }

        return KbSolutionResult.Success((await LoadDtoAsync(id, cancellationToken))!);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var solution = await db.KbSolutions.SingleOrDefaultAsync(s => s.Id == id, cancellationToken);
        if (solution is null)
        {
            return false;
        }

        db.KbSolutions.Remove(solution);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private Task<KbSolutionDto?> LoadDtoAsync(Guid id, CancellationToken cancellationToken) =>
        db.KbSolutions.AsNoTracking().Where(s => s.Id == id).Select(ToDtoExpression).SingleOrDefaultAsync(cancellationToken);

    private static (KnowledgeBasePublicationStatus? Status, KnowledgeBaseAudience? Audience) ApplyVisibility(
        KnowledgeBasePublicationStatus? status, KnowledgeBaseAudience? audience, bool canManage, bool canSeeInternal)
    {
        if (canManage)
        {
            return (status, audience);
        }

        return (KnowledgeBasePublicationStatus.Published, canSeeInternal ? audience : KnowledgeBaseAudience.CustomerFacing);
    }
}
