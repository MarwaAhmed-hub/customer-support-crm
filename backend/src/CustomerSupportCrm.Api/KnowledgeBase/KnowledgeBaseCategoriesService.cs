using CustomerSupportCrm.Domain.KnowledgeBase;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupportCrm.Api.KnowledgeBase;

public sealed class KnowledgeBaseCategoriesService(CrmDbContext db) : IKnowledgeBaseCategoriesService
{
    public async Task<IReadOnlyList<KnowledgeBaseCategoryDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default)
    {
        var query = db.KnowledgeBaseCategories.AsNoTracking().AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        return await query
            .OrderBy(c => c.Name)
            .Select(c => new KnowledgeBaseCategoryDto(c.Id, c.Name, c.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<KnowledgeBaseCategoryDto?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await db.KnowledgeBaseCategories
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(c => new KnowledgeBaseCategoryDto(c.Id, c.Name, c.IsActive))
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<KnowledgeBaseCategoryResult> CreateAsync(CreateKnowledgeBaseCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        if (name.Length == 0)
        {
            return KnowledgeBaseCategoryResult.InvalidName;
        }

        var normalized = name.ToUpperInvariant();

        if (await db.KnowledgeBaseCategories.AnyAsync(c => c.NormalizedName == normalized, cancellationToken))
        {
            return KnowledgeBaseCategoryResult.DuplicateName;
        }

        var category = new KnowledgeBaseCategory
        {
            Name = name,
            NormalizedName = normalized,
            IsActive = true,
        };

        db.KnowledgeBaseCategories.Add(category);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // A concurrent create with the same normalized name raced past the check above and lost
            // to the unique index — same defense-in-depth pattern as DepartmentsService.
            return KnowledgeBaseCategoryResult.DuplicateName;
        }

        return KnowledgeBaseCategoryResult.Success(new KnowledgeBaseCategoryDto(category.Id, category.Name, category.IsActive));
    }

    public async Task<KnowledgeBaseCategoryResult> UpdateAsync(Guid id, UpdateKnowledgeBaseCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var category = await db.KnowledgeBaseCategories.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category is null)
        {
            return KnowledgeBaseCategoryResult.NotFound;
        }

        var name = request.Name.Trim();
        if (name.Length == 0)
        {
            return KnowledgeBaseCategoryResult.InvalidName;
        }

        var normalized = name.ToUpperInvariant();

        if (normalized != category.NormalizedName &&
            await db.KnowledgeBaseCategories.AnyAsync(c => c.Id != id && c.NormalizedName == normalized, cancellationToken))
        {
            return KnowledgeBaseCategoryResult.DuplicateName;
        }

        category.Name = name;
        category.NormalizedName = normalized;
        category.IsActive = request.IsActive;
        category.UpdatedAtUtc = DateTime.UtcNow;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return KnowledgeBaseCategoryResult.DuplicateName;
        }

        return KnowledgeBaseCategoryResult.Success(new KnowledgeBaseCategoryDto(category.Id, category.Name, category.IsActive));
    }

    public async Task<KnowledgeBaseCategoryResult> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var category = await db.KnowledgeBaseCategories.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category is null)
        {
            return KnowledgeBaseCategoryResult.NotFound;
        }

        var referenced =
            await db.KnowledgeBaseArticles.AnyAsync(a => a.CategoryId == id, cancellationToken) ||
            await db.KbSolutions.AnyAsync(s => s.CategoryId == id, cancellationToken) ||
            await db.KbGuides.AnyAsync(g => g.CategoryId == id, cancellationToken);

        if (referenced)
        {
            return KnowledgeBaseCategoryResult.ReferencedByContent;
        }

        db.KnowledgeBaseCategories.Remove(category);
        await db.SaveChangesAsync(cancellationToken);

        return KnowledgeBaseCategoryResult.Deleted;
    }

    // Same pattern as TicketCategoriesService.IsUniqueViolation: a synchronous check on the SQL error
    // number (2601/2627), not a second DB round-trip.
    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
}
