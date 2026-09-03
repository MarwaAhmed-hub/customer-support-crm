using CustomerSupportCrm.Domain.Departments;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupportCrm.Api.Departments;

public enum DepartmentOperationOutcome
{
    Success,
    NotFound,

    /// <summary>Name is empty/whitespace-only after trimming — StringLength(MinimumLength = 1) alone lets a single space through.</summary>
    InvalidName,

    DuplicateName,
    DuplicateCode,
}

public sealed record DepartmentResult(DepartmentOperationOutcome Outcome, DepartmentDto? Department = null)
{
    public static DepartmentResult Success(DepartmentDto department) => new(DepartmentOperationOutcome.Success, department);
    public static readonly DepartmentResult NotFound = new(DepartmentOperationOutcome.NotFound);
    public static readonly DepartmentResult InvalidName = new(DepartmentOperationOutcome.InvalidName);
    public static readonly DepartmentResult DuplicateName = new(DepartmentOperationOutcome.DuplicateName);
    public static readonly DepartmentResult DuplicateCode = new(DepartmentOperationOutcome.DuplicateCode);
}

/// <summary>
/// Business rules for departments: duplicate-name rejection (case-insensitive), duplicate-code
/// rejection (case-sensitive, non-null only), and the no-hard-delete rule (see
/// <see cref="UpdateAsync"/> — deactivation is just <see cref="UpdateDepartmentRequest.IsActive"/>).
/// Modeled directly on <c>Roles.RolesService</c>.
/// </summary>
public interface IDepartmentsService
{
    /// <summary>Active departments only by default — the picker on <c>UserFormPage</c> uses this; the admin list page passes <c>includeInactive: true</c>.</summary>
    Task<IReadOnlyList<DepartmentDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default);

    Task<DepartmentDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<DepartmentResult> CreateAsync(CreateDepartmentRequest request, CancellationToken cancellationToken = default);

    Task<DepartmentResult> UpdateAsync(Guid id, UpdateDepartmentRequest request, CancellationToken cancellationToken = default);
}

public sealed class DepartmentsService(CrmDbContext db) : IDepartmentsService
{
    public async Task<IReadOnlyList<DepartmentDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default)
    {
        var query = db.Departments.AsNoTracking().AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(d => d.IsActive);
        }

        return await query
            .OrderBy(d => d.Name)
            .Select(ToDtoExpression)
            .ToListAsync(cancellationToken);
    }

    public async Task<DepartmentDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var department = await db.Departments.AsNoTracking().SingleOrDefaultAsync(d => d.Id == id, cancellationToken);
        return department is null ? null : ToDto(department);
    }

    public async Task<DepartmentResult> CreateAsync(CreateDepartmentRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        if (name.Length == 0)
        {
            return DepartmentResult.InvalidName;
        }

        var normalized = name.ToUpperInvariant();
        var code = NormalizeCode(request.Code);

        if (await db.Departments.AnyAsync(d => d.NormalizedName == normalized, cancellationToken))
        {
            return DepartmentResult.DuplicateName;
        }

        if (code is not null && await db.Departments.AnyAsync(d => d.Code == code, cancellationToken))
        {
            return DepartmentResult.DuplicateCode;
        }

        var department = new Department
        {
            Name = name,
            NormalizedName = normalized,
            Code = code,
            IsActive = true,
        };

        db.Departments.Add(department);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // A concurrent create with the same normalized name or code raced past the checks above
            // and lost to a unique index — same defense-in-depth pattern as RolesService/UsersController.
            // Both indexes share the same SQL error numbers, so re-check which one actually collided.
            return await db.Departments.AnyAsync(d => d.Id != department.Id && d.NormalizedName == normalized, cancellationToken)
                ? DepartmentResult.DuplicateName
                : DepartmentResult.DuplicateCode;
        }

        return DepartmentResult.Success(ToDto(department));
    }

    public async Task<DepartmentResult> UpdateAsync(Guid id, UpdateDepartmentRequest request, CancellationToken cancellationToken = default)
    {
        var department = await db.Departments.SingleOrDefaultAsync(d => d.Id == id, cancellationToken);
        if (department is null)
        {
            return DepartmentResult.NotFound;
        }

        var name = request.Name.Trim();
        if (name.Length == 0)
        {
            return DepartmentResult.InvalidName;
        }

        var normalized = name.ToUpperInvariant();
        var code = NormalizeCode(request.Code);

        if (normalized != department.NormalizedName &&
            await db.Departments.AnyAsync(d => d.Id != id && d.NormalizedName == normalized, cancellationToken))
        {
            return DepartmentResult.DuplicateName;
        }

        if (code is not null && code != department.Code &&
            await db.Departments.AnyAsync(d => d.Id != id && d.Code == code, cancellationToken))
        {
            return DepartmentResult.DuplicateCode;
        }

        department.Name = name;
        department.NormalizedName = normalized;
        department.Code = code;
        department.IsActive = request.IsActive;
        department.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return await db.Departments.AnyAsync(d => d.Id != id && d.NormalizedName == normalized, cancellationToken)
                ? DepartmentResult.DuplicateName
                : DepartmentResult.DuplicateCode;
        }

        return DepartmentResult.Success(ToDto(department));
    }

    private static readonly System.Linq.Expressions.Expression<Func<Department, DepartmentDto>> ToDtoExpression =
        d => new DepartmentDto(d.Id, d.Name, d.Code, d.IsActive, d.CreatedAt, d.UpdatedAt);

    private static DepartmentDto ToDto(Department department) =>
        new(department.Id, department.Name, department.Code, department.IsActive, department.CreatedAt, department.UpdatedAt);

    private static string? NormalizeCode(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null : code.Trim();

    // Same pattern as RolesService.IsUniqueNameViolation / UsersController.IsUniqueEmailViolation: a
    // synchronous check on the SQL error number (2601/2627), not a second DB round-trip.
    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
}
