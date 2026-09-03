using CustomerSupportCrm.Domain.Branches;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupportCrm.Api.Branches;

public enum BranchOperationOutcome
{
    Success,
    NotFound,

    /// <summary>Name is empty/whitespace-only after trimming — StringLength(MinimumLength = 1) alone lets a single space through.</summary>
    InvalidName,

    DuplicateName,
    DuplicateCode,
}

public sealed record BranchResult(BranchOperationOutcome Outcome, BranchDto? Branch = null)
{
    public static BranchResult Success(BranchDto branch) => new(BranchOperationOutcome.Success, branch);
    public static readonly BranchResult NotFound = new(BranchOperationOutcome.NotFound);
    public static readonly BranchResult InvalidName = new(BranchOperationOutcome.InvalidName);
    public static readonly BranchResult DuplicateName = new(BranchOperationOutcome.DuplicateName);
    public static readonly BranchResult DuplicateCode = new(BranchOperationOutcome.DuplicateCode);
}

/// <summary>
/// Business rules for branches — the mirror image of <c>Departments.DepartmentsService</c>: duplicate
/// -name rejection (case-insensitive), duplicate-code rejection (case-sensitive, non-null only), and
/// the no-hard-delete rule (deactivation is just <see cref="UpdateBranchRequest.IsActive"/>).
/// </summary>
public interface IBranchesService
{
    /// <summary>Active branches only by default — the picker on <c>UserFormPage</c> uses this; the admin list page passes <c>includeInactive: true</c>.</summary>
    Task<IReadOnlyList<BranchDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default);

    Task<BranchDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    Task<BranchResult> CreateAsync(CreateBranchRequest request, CancellationToken cancellationToken = default);

    Task<BranchResult> UpdateAsync(Guid id, UpdateBranchRequest request, CancellationToken cancellationToken = default);
}

public sealed class BranchesService(CrmDbContext db) : IBranchesService
{
    public async Task<IReadOnlyList<BranchDto>> ListAsync(bool includeInactive, CancellationToken cancellationToken = default)
    {
        var query = db.Branches.AsNoTracking().AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(b => b.IsActive);
        }

        return await query
            .OrderBy(b => b.Name)
            .Select(ToDtoExpression)
            .ToListAsync(cancellationToken);
    }

    public async Task<BranchDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var branch = await db.Branches.AsNoTracking().SingleOrDefaultAsync(b => b.Id == id, cancellationToken);
        return branch is null ? null : ToDto(branch);
    }

    public async Task<BranchResult> CreateAsync(CreateBranchRequest request, CancellationToken cancellationToken = default)
    {
        var name = request.Name.Trim();
        if (name.Length == 0)
        {
            return BranchResult.InvalidName;
        }

        var normalized = name.ToUpperInvariant();
        var code = NormalizeCode(request.Code);

        if (await db.Branches.AnyAsync(b => b.NormalizedName == normalized, cancellationToken))
        {
            return BranchResult.DuplicateName;
        }

        if (code is not null && await db.Branches.AnyAsync(b => b.Code == code, cancellationToken))
        {
            return BranchResult.DuplicateCode;
        }

        var branch = new Branch
        {
            Name = name,
            NormalizedName = normalized,
            Code = code,
            IsActive = true,
        };

        db.Branches.Add(branch);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Defense in depth against a concurrent create racing past the checks above — see
            // DepartmentsService.CreateAsync's matching comment.
            return await db.Branches.AnyAsync(b => b.Id != branch.Id && b.NormalizedName == normalized, cancellationToken)
                ? BranchResult.DuplicateName
                : BranchResult.DuplicateCode;
        }

        return BranchResult.Success(ToDto(branch));
    }

    public async Task<BranchResult> UpdateAsync(Guid id, UpdateBranchRequest request, CancellationToken cancellationToken = default)
    {
        var branch = await db.Branches.SingleOrDefaultAsync(b => b.Id == id, cancellationToken);
        if (branch is null)
        {
            return BranchResult.NotFound;
        }

        var name = request.Name.Trim();
        if (name.Length == 0)
        {
            return BranchResult.InvalidName;
        }

        var normalized = name.ToUpperInvariant();
        var code = NormalizeCode(request.Code);

        if (normalized != branch.NormalizedName &&
            await db.Branches.AnyAsync(b => b.Id != id && b.NormalizedName == normalized, cancellationToken))
        {
            return BranchResult.DuplicateName;
        }

        if (code is not null && code != branch.Code &&
            await db.Branches.AnyAsync(b => b.Id != id && b.Code == code, cancellationToken))
        {
            return BranchResult.DuplicateCode;
        }

        branch.Name = name;
        branch.NormalizedName = normalized;
        branch.Code = code;
        branch.IsActive = request.IsActive;
        branch.UpdatedAt = DateTimeOffset.UtcNow;

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            return await db.Branches.AnyAsync(b => b.Id != id && b.NormalizedName == normalized, cancellationToken)
                ? BranchResult.DuplicateName
                : BranchResult.DuplicateCode;
        }

        return BranchResult.Success(ToDto(branch));
    }

    private static readonly System.Linq.Expressions.Expression<Func<Branch, BranchDto>> ToDtoExpression =
        b => new BranchDto(b.Id, b.Name, b.Code, b.IsActive, b.CreatedAt, b.UpdatedAt);

    private static BranchDto ToDto(Branch branch) =>
        new(branch.Id, branch.Name, branch.Code, branch.IsActive, branch.CreatedAt, branch.UpdatedAt);

    private static string? NormalizeCode(string? code) =>
        string.IsNullOrWhiteSpace(code) ? null : code.Trim();

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
}
