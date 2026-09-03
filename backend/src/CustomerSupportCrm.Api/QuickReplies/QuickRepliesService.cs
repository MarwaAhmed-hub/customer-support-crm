using CustomerSupportCrm.Domain.QuickReplies;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupportCrm.Api.QuickReplies;

public sealed class QuickRepliesService(CrmDbContext db) : IQuickRepliesService
{
    public async Task<IReadOnlyList<QuickReplyDto>> ListAsync(bool includeInactive, string? search, CancellationToken cancellationToken = default)
    {
        var query = db.QuickReplies.AsNoTracking().AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(q => q.IsActive);
        }

        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            query = query.Where(q => q.Title.Contains(term) || q.Body.Contains(term));
        }

        return await query
            .OrderBy(q => q.Title)
            .Select(ToDtoExpression)
            .ToListAsync(cancellationToken);
    }

    public async Task<QuickReplyDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var quickReply = await db.QuickReplies.AsNoTracking().SingleOrDefaultAsync(q => q.Id == id, cancellationToken);
        return quickReply is null ? null : ToDto(quickReply);
    }

    public async Task<QuickReplyResult> CreateAsync(CreateQuickReplyRequest request, CancellationToken cancellationToken = default)
    {
        var title = request.Title.Trim();
        if (title.Length == 0)
        {
            return QuickReplyResult.InvalidTitle;
        }

        var body = request.Body.Trim();
        if (body.Length == 0)
        {
            return QuickReplyResult.InvalidBody;
        }

        var normalized = title.ToUpperInvariant();

        if (await db.QuickReplies.AnyAsync(q => q.NormalizedTitle == normalized, cancellationToken))
        {
            return QuickReplyResult.DuplicateTitle;
        }

        var quickReply = new QuickReply
        {
            Title = title,
            NormalizedTitle = normalized,
            Body = body,
            IsActive = true,
        };

        db.QuickReplies.Add(quickReply);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // A concurrent create with the same normalized title raced past the check above and lost
            // to the unique index — same defense-in-depth pattern as TicketCategoriesService.
            return QuickReplyResult.DuplicateTitle;
        }

        return QuickReplyResult.Success(ToDto(quickReply));
    }

    public async Task<QuickReplyResult> UpdateAsync(Guid id, UpdateQuickReplyRequest request, CancellationToken cancellationToken = default)
    {
        var quickReply = await db.QuickReplies.SingleOrDefaultAsync(q => q.Id == id, cancellationToken);
        if (quickReply is null)
        {
            return QuickReplyResult.NotFound;
        }

        var title = request.Title.Trim();
        if (title.Length == 0)
        {
            return QuickReplyResult.InvalidTitle;
        }

        var body = request.Body.Trim();
        if (body.Length == 0)
        {
            return QuickReplyResult.InvalidBody;
        }

        var normalized = title.ToUpperInvariant();

        if (normalized != quickReply.NormalizedTitle &&
            await db.QuickReplies.AnyAsync(q => q.Id != id && q.NormalizedTitle == normalized, cancellationToken))
        {
            return QuickReplyResult.DuplicateTitle;
        }

        quickReply.Title = title;
        quickReply.NormalizedTitle = normalized;
        quickReply.Body = body;
        quickReply.IsActive = request.IsActive;
        quickReply.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return QuickReplyResult.DuplicateTitle;
        }

        return QuickReplyResult.Success(ToDto(quickReply));
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var quickReply = await db.QuickReplies.SingleOrDefaultAsync(q => q.Id == id, cancellationToken);
        if (quickReply is null)
        {
            return false;
        }

        db.QuickReplies.Remove(quickReply);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static readonly System.Linq.Expressions.Expression<Func<QuickReply, QuickReplyDto>> ToDtoExpression =
        q => new QuickReplyDto(q.Id, q.Title, q.Body, q.IsActive, q.CreatedAt, q.UpdatedAt);

    private static QuickReplyDto ToDto(QuickReply quickReply) =>
        new(quickReply.Id, quickReply.Title, quickReply.Body, quickReply.IsActive, quickReply.CreatedAt, quickReply.UpdatedAt);

    // Same pattern as TicketCategoriesService.IsUniqueViolation: a synchronous check on the SQL error
    // number (2601/2627), not a second DB round-trip.
    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
}
