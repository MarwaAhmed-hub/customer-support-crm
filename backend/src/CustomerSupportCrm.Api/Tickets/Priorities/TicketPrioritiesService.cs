using CustomerSupportCrm.Domain.Tickets;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupportCrm.Api.Tickets.Priorities;

public enum TicketPriorityOperationOutcome
{
    Success,
    NotFound,

    /// <summary>Name is empty/whitespace-only after trimming — StringLength(MinimumLength = 1) alone lets a single space through.</summary>
    InvalidName,

    DuplicateName,
}

public sealed record TicketPriorityResult(TicketPriorityOperationOutcome Outcome, TicketPriorityDto? Priority = null)
{
    public static TicketPriorityResult Success(TicketPriorityDto priority) => new(TicketPriorityOperationOutcome.Success, priority);
    public static readonly TicketPriorityResult NotFound = new(TicketPriorityOperationOutcome.NotFound);
    public static readonly TicketPriorityResult InvalidName = new(TicketPriorityOperationOutcome.InvalidName);
    public static readonly TicketPriorityResult DuplicateName = new(TicketPriorityOperationOutcome.DuplicateName);
}

/// <summary>
/// Business rules for ticket priorities: duplicate-name rejection (case-insensitive) and the
/// no-hard-delete rule. Modeled directly on <c>Departments.DepartmentsService</c>, plus
/// <see cref="TicketPriority.SortOrder"/> handling — collisions are permitted (no uniqueness rule),
/// and the canonical listing order is <c>SortOrder</c> ascending, then <c>Name</c> as a tiebreaker.
/// </summary>
public interface ITicketPrioritiesService
{
    /// <summary>Active priorities only by default — a ticket form's picker (a later story) uses this; the admin list page passes <c>includeInactive: true</c>.</summary>
    Task<IReadOnlyList<TicketPriorityDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default);

    Task<TicketPriorityDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<TicketPriorityResult> CreateAsync(CreateTicketPriorityRequest request, CancellationToken cancellationToken = default);

    Task<TicketPriorityResult> UpdateAsync(Guid id, UpdateTicketPriorityRequest request, CancellationToken cancellationToken = default);
}

public sealed class TicketPrioritiesService(CrmDbContext db) : ITicketPrioritiesService
{
    public async Task<IReadOnlyList<TicketPriorityDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default)
    {
        var query = db.TicketPriorities.AsNoTracking().AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(p => p.IsActive);
        }

        return await query
            .OrderBy(p => p.SortOrder)
            .ThenBy(p => p.Name)
            .Select(ToDtoExpression)
            .ToListAsync(cancellationToken);
    }

    public async Task<TicketPriorityDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var priority = await db.TicketPriorities.AsNoTracking().SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        return priority is null ? null : ToDto(priority);
    }

    public async Task<TicketPriorityResult> CreateAsync(CreateTicketPriorityRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        if (name.Length == 0)
        {
            return TicketPriorityResult.InvalidName;
        }

        var normalized = name.ToUpperInvariant();

        if (await db.TicketPriorities.AnyAsync(p => p.NormalizedName == normalized, cancellationToken))
        {
            return TicketPriorityResult.DuplicateName;
        }

        var priority = new TicketPriority
        {
            Name = name,
            NormalizedName = normalized,
            SortOrder = request.SortOrder,
            Description = NormalizeDescription(request.Description),
            IsActive = true,
        };

        db.TicketPriorities.Add(priority);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return TicketPriorityResult.DuplicateName;
        }

        return TicketPriorityResult.Success(ToDto(priority));
    }

    public async Task<TicketPriorityResult> UpdateAsync(Guid id, UpdateTicketPriorityRequest request, CancellationToken cancellationToken = default)
    {
        var priority = await db.TicketPriorities.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (priority is null)
        {
            return TicketPriorityResult.NotFound;
        }

        var name = request.Name.Trim();
        if (name.Length == 0)
        {
            return TicketPriorityResult.InvalidName;
        }

        var normalized = name.ToUpperInvariant();

        if (normalized != priority.NormalizedName &&
            await db.TicketPriorities.AnyAsync(p => p.Id != id && p.NormalizedName == normalized, cancellationToken))
        {
            return TicketPriorityResult.DuplicateName;
        }

        priority.Name = name;
        priority.NormalizedName = normalized;
        priority.SortOrder = request.SortOrder;
        priority.Description = NormalizeDescription(request.Description);
        priority.IsActive = request.IsActive;
        priority.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return TicketPriorityResult.DuplicateName;
        }

        return TicketPriorityResult.Success(ToDto(priority));
    }

    private static readonly System.Linq.Expressions.Expression<Func<TicketPriority, TicketPriorityDto>> ToDtoExpression =
        p => new TicketPriorityDto(p.Id, p.Name, p.SortOrder, p.Description, p.IsActive, p.CreatedAt, p.UpdatedAt);

    private static TicketPriorityDto ToDto(TicketPriority priority) =>
        new(priority.Id, priority.Name, priority.SortOrder, priority.Description, priority.IsActive, priority.CreatedAt, priority.UpdatedAt);

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
}
