using System.Linq.Expressions;
using CustomerSupportCrm.Domain.KnowledgeBase;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupportCrm.Api.KnowledgeBase.Guides;

public sealed class KbGuidesService(CrmDbContext db) : IKbGuidesService
{
    private static readonly Expression<Func<KbGuide, KbGuideDto>> ToDtoExpression = g =>
        new KbGuideDto(
            g.Id, g.Title, g.Description, g.CategoryId, g.Category!.Name, g.Audience, g.Status,
            g.Steps.OrderBy(step => step.Order).Select(step => new KbGuideStepDto(step.Order, step.Instruction)).ToList(),
            g.CreatedAtUtc, g.UpdatedAtUtc, g.PublishedAtUtc);

    public async Task<IReadOnlyList<KbGuideDto>> ListAsync(
        Guid? categoryId, KnowledgeBaseAudience? audience, KnowledgeBasePublicationStatus? status,
        bool canManage, bool canSeeInternal, CancellationToken cancellationToken = default)
    {
        var (effectiveStatus, effectiveAudience) = ApplyVisibility(status, audience, canManage, canSeeInternal);

        var query = db.KbGuides.AsNoTracking().AsQueryable();

        if (categoryId.HasValue)
        {
            query = query.Where(g => g.CategoryId == categoryId.Value);
        }

        if (effectiveStatus.HasValue)
        {
            query = query.Where(g => g.Status == effectiveStatus.Value);
        }

        if (effectiveAudience.HasValue)
        {
            query = query.Where(g => g.Audience == effectiveAudience.Value);
        }

        return await query
            .OrderByDescending(g => g.CreatedAtUtc)
            .Select(ToDtoExpression)
            .ToListAsync(cancellationToken);
    }

    public async Task<KbGuideDto?> GetAsync(Guid id, bool canManage, bool canSeeInternal, CancellationToken cancellationToken = default)
    {
        var guide = await db.KbGuides
            .AsNoTracking()
            .Where(g => g.Id == id)
            .Select(ToDtoExpression)
            .SingleOrDefaultAsync(cancellationToken);

        if (guide is null)
        {
            return null;
        }

        if (canManage)
        {
            return guide;
        }

        if (guide.Status != KnowledgeBasePublicationStatus.Published)
        {
            return null;
        }

        if (guide.Audience == KnowledgeBaseAudience.Internal && !canSeeInternal)
        {
            return null;
        }

        return guide;
    }

    public async Task<KbGuideResult> CreateAsync(CreateKbGuideRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var title = request.Title.Trim();
        if (title.Length == 0)
        {
            return KbGuideResult.InvalidTitle;
        }

        var description = request.Description.Trim();
        if (description.Length == 0)
        {
            return KbGuideResult.InvalidDescription;
        }

        var steps = TrimSteps(request.Steps);
        if (steps.Count == 0)
        {
            return KbGuideResult.InvalidSteps;
        }

        if (!await db.KnowledgeBaseCategories.AnyAsync(c => c.Id == request.CategoryId, cancellationToken))
        {
            return KbGuideResult.CategoryNotFound;
        }

        var guide = new KbGuide
        {
            Title = title,
            Description = description,
            CategoryId = request.CategoryId,
            Audience = request.Audience,
            Status = KnowledgeBasePublicationStatus.Draft,
            CreatedByUserId = actorUserId,
            Steps = BuildSteps(steps),
        };

        db.KbGuides.Add(guide);
        await db.SaveChangesAsync(cancellationToken);

        return KbGuideResult.Success((await LoadDtoAsync(guide.Id, cancellationToken))!);
    }

    public async Task<KbGuideResult> UpdateAsync(Guid id, UpdateKbGuideRequest request, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var guide = await db.KbGuides.Include(g => g.Steps).SingleOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (guide is null)
        {
            return KbGuideResult.NotFound;
        }

        var title = request.Title.Trim();
        if (title.Length == 0)
        {
            return KbGuideResult.InvalidTitle;
        }

        var description = request.Description.Trim();
        if (description.Length == 0)
        {
            return KbGuideResult.InvalidDescription;
        }

        var steps = TrimSteps(request.Steps);
        if (steps.Count == 0)
        {
            return KbGuideResult.InvalidSteps;
        }

        if (!await db.KnowledgeBaseCategories.AnyAsync(c => c.Id == request.CategoryId, cancellationToken))
        {
            return KbGuideResult.CategoryNotFound;
        }

        guide.Title = title;
        guide.Description = description;
        guide.CategoryId = request.CategoryId;
        guide.Audience = request.Audience;
        guide.UpdatedByUserId = actorUserId;
        guide.UpdatedAtUtc = DateTime.UtcNow;

        // Whole-collection replace, not a diff — matches the interface's documented contract. EF
        // tracks the removed steps for deletion and the newly-added ones for insertion in the same
        // SaveChanges call.
        guide.Steps.Clear();
        foreach (var step in BuildSteps(steps))
        {
            guide.Steps.Add(step);
        }

        await db.SaveChangesAsync(cancellationToken);

        return KbGuideResult.Success((await LoadDtoAsync(id, cancellationToken))!);
    }

    public async Task<KbGuideResult> PublishAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var guide = await db.KbGuides.SingleOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (guide is null)
        {
            return KbGuideResult.NotFound;
        }

        if (guide.Status != KnowledgeBasePublicationStatus.Published)
        {
            guide.Status = KnowledgeBasePublicationStatus.Published;
            guide.PublishedAtUtc = DateTime.UtcNow;
            guide.PublishedByUserId = actorUserId;
            await db.SaveChangesAsync(cancellationToken);
        }

        return KbGuideResult.Success((await LoadDtoAsync(id, cancellationToken))!);
    }

    public async Task<KbGuideResult> UnpublishAsync(Guid id, Guid actorUserId, CancellationToken cancellationToken = default)
    {
        var guide = await db.KbGuides.SingleOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (guide is null)
        {
            return KbGuideResult.NotFound;
        }

        if (guide.Status != KnowledgeBasePublicationStatus.Draft)
        {
            guide.Status = KnowledgeBasePublicationStatus.Draft;
            guide.PublishedAtUtc = null;
            guide.PublishedByUserId = null;
            await db.SaveChangesAsync(cancellationToken);
        }

        return KbGuideResult.Success((await LoadDtoAsync(id, cancellationToken))!);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var guide = await db.KbGuides.SingleOrDefaultAsync(g => g.Id == id, cancellationToken);
        if (guide is null)
        {
            return false;
        }

        db.KbGuides.Remove(guide);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private Task<KbGuideDto?> LoadDtoAsync(Guid id, CancellationToken cancellationToken) =>
        db.KbGuides.AsNoTracking().Where(g => g.Id == id).Select(ToDtoExpression).SingleOrDefaultAsync(cancellationToken);

    /// <summary>Trims every instruction and drops entries that trim to empty — an all-whitespace step is treated the same as a missing one.</summary>
    private static List<string> TrimSteps(IReadOnlyList<KbGuideStepInput> steps) =>
        steps.Select(s => s.Instruction.Trim()).Where(s => s.Length > 0).ToList();

    private static List<KbGuideStep> BuildSteps(List<string> trimmedInstructions) =>
        trimmedInstructions.Select((instruction, index) => new KbGuideStep { Order = index, Instruction = instruction }).ToList();

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
