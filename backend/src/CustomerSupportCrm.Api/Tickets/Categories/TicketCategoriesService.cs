using System.Linq.Expressions;
using CustomerSupportCrm.Domain.Departments;
using CustomerSupportCrm.Domain.Tickets;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupportCrm.Api.Tickets.Categories;

public enum TicketCategoryOperationOutcome
{
    Success,
    NotFound,

    /// <summary>Name is empty/whitespace-only after trimming — StringLength(MinimumLength = 1) alone lets a single space through.</summary>
    InvalidName,

    DuplicateName,

    /// <summary><c>DepartmentId</c> doesn't match any department, or matches one with <c>IsActive == false</c> — same "invalid reference" 400 as <c>UsersController</c>'s Department/Branch validation.</summary>
    InvalidDepartment,
}

public sealed record TicketCategoryResult(TicketCategoryOperationOutcome Outcome, TicketCategoryDto? Category = null)
{
    public static TicketCategoryResult Success(TicketCategoryDto category) => new(TicketCategoryOperationOutcome.Success, category);
    public static readonly TicketCategoryResult NotFound = new(TicketCategoryOperationOutcome.NotFound);
    public static readonly TicketCategoryResult InvalidName = new(TicketCategoryOperationOutcome.InvalidName);
    public static readonly TicketCategoryResult DuplicateName = new(TicketCategoryOperationOutcome.DuplicateName);
    public static readonly TicketCategoryResult InvalidDepartment = new(TicketCategoryOperationOutcome.InvalidDepartment);
}

/// <summary>
/// Business rules for ticket categories: duplicate-name rejection (case-insensitive) and the
/// no-hard-delete rule (see <see cref="UpdateAsync"/> — deactivation is just
/// <see cref="UpdateTicketCategoryRequest.IsActive"/>). Modeled directly on
/// <c>Departments.DepartmentsService</c>.
/// </summary>
public interface ITicketCategoriesService
{
    /// <summary>Active categories only by default — a ticket form's picker (a later story) uses this; the admin list page passes <c>includeInactive: true</c>.</summary>
    Task<IReadOnlyList<TicketCategoryDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default);

    Task<TicketCategoryDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<TicketCategoryResult> CreateAsync(CreateTicketCategoryRequest request, CancellationToken cancellationToken = default);

    Task<TicketCategoryResult> UpdateAsync(Guid id, UpdateTicketCategoryRequest request, CancellationToken cancellationToken = default);
}

public sealed class TicketCategoriesService(CrmDbContext db) : ITicketCategoriesService
{
    public async Task<IReadOnlyList<TicketCategoryDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default)
    {
        var query = db.TicketCategories.AsNoTracking().AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }

        return await query
            .OrderBy(c => c.Name)
            .Select(ToDtoExpression)
            .ToListAsync(cancellationToken);
    }

    public async Task<TicketCategoryDto?> GetAsync(Guid id, CancellationToken cancellationToken = default) =>
        await db.TicketCategories
            .AsNoTracking()
            .Where(c => c.Id == id)
            .Select(ToDtoExpression)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<TicketCategoryResult> CreateAsync(CreateTicketCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        if (name.Length == 0)
        {
            return TicketCategoryResult.InvalidName;
        }

        var normalized = name.ToUpperInvariant();

        if (await db.TicketCategories.AnyAsync(c => c.NormalizedName == normalized, cancellationToken))
        {
            return TicketCategoryResult.DuplicateName;
        }

        if (!await IsValidDepartmentAsync(request.DepartmentId, cancellationToken))
        {
            return TicketCategoryResult.InvalidDepartment;
        }

        var category = new TicketCategory
        {
            Name = name,
            NormalizedName = normalized,
            Description = NormalizeDescription(request.Description),
            IsActive = true,
            DepartmentId = request.DepartmentId,
        };

        db.TicketCategories.Add(category);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // A concurrent create with the same normalized name raced past the check above and lost
            // to the unique index — same defense-in-depth pattern as DepartmentsService.
            return TicketCategoryResult.DuplicateName;
        }

        return TicketCategoryResult.Success(await ToDtoAsync(category, cancellationToken));
    }

    public async Task<TicketCategoryResult> UpdateAsync(Guid id, UpdateTicketCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var category = await db.TicketCategories.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (category is null)
        {
            return TicketCategoryResult.NotFound;
        }

        var name = request.Name.Trim();
        if (name.Length == 0)
        {
            return TicketCategoryResult.InvalidName;
        }

        var normalized = name.ToUpperInvariant();

        if (normalized != category.NormalizedName &&
            await db.TicketCategories.AnyAsync(c => c.Id != id && c.NormalizedName == normalized, cancellationToken))
        {
            return TicketCategoryResult.DuplicateName;
        }

        if (!await IsValidDepartmentAsync(request.DepartmentId, cancellationToken))
        {
            return TicketCategoryResult.InvalidDepartment;
        }

        category.Name = name;
        category.NormalizedName = normalized;
        category.Description = NormalizeDescription(request.Description);
        category.IsActive = request.IsActive;
        category.DepartmentId = request.DepartmentId;
        category.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return TicketCategoryResult.DuplicateName;
        }

        return TicketCategoryResult.Success(await ToDtoAsync(category, cancellationToken));
    }

    /// <summary>A null <paramref name="departmentId"/> is always valid (no department) — matches <see cref="Api.Users.UsersController"/>'s own Department/Branch validation for the same reason.</summary>
    private Task<bool> IsValidDepartmentAsync(Guid? departmentId, CancellationToken cancellationToken) =>
        departmentId is not { } id
            ? Task.FromResult(true)
            : db.Departments.AnyAsync(d => d.Id == id && d.IsActive, cancellationToken);

    private async Task<TicketCategoryDto> ToDtoAsync(TicketCategory category, CancellationToken cancellationToken)
    {
        string? departmentName = null;
        if (category.DepartmentId is { } departmentId)
        {
            departmentName = await db.Departments.AsNoTracking().Where(d => d.Id == departmentId).Select(d => d.Name).SingleOrDefaultAsync(cancellationToken);
        }
        return new TicketCategoryDto(category.Id, category.Name, category.Description, category.IsActive, category.DepartmentId, departmentName, category.CreatedAt, category.UpdatedAt);
    }

    // No navigation property on TicketCategory to Department (see the remarks on
    // TicketCategory.DepartmentId), so the name is resolved via a correlated subquery here rather
    // than a join through a nav property — same "read-only list projections may reference db
    // directly" pattern LiveChatService's ListForAgentAsync uses for its own correlated subquery.
    private Expression<Func<TicketCategory, TicketCategoryDto>> ToDtoExpression =>
        c => new TicketCategoryDto(
            c.Id, c.Name, c.Description, c.IsActive, c.DepartmentId,
            c.DepartmentId != null ? db.Departments.Where(d => d.Id == c.DepartmentId).Select(d => d.Name).FirstOrDefault() : null,
            c.CreatedAt, c.UpdatedAt);

    private static string? NormalizeDescription(string? description) =>
        string.IsNullOrWhiteSpace(description) ? null : description.Trim();

    // Same pattern as DepartmentsService.IsUniqueViolation: a synchronous check on the SQL error
    // number (2601/2627), not a second DB round-trip.
    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
}
