using System.Linq.Expressions;
using CustomerSupportCrm.Domain.Sla;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupportCrm.Api.Sla;

public sealed class SlaPoliciesService(CrmDbContext db) : ISlaPoliciesService
{
    public async Task<IReadOnlyList<SlaPolicyDto>> ListActiveAsync(CancellationToken cancellationToken = default) =>
        await db.SlaPolicies
            .AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.Priority != null ? p.Priority.SortOrder : -1)
            .Select(ToDtoExpression)
            .ToListAsync(cancellationToken);

    public async Task<SlaPolicyResult> UpdateAsync(Guid id, UpdateSlaPolicyRequest request, CancellationToken cancellationToken = default)
    {
        var policy = await db.SlaPolicies.SingleOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (policy is null)
        {
            return SlaPolicyResult.NotFound;
        }

        if (request.FirstResponseMinutes < 1)
        {
            return SlaPolicyResult.InvalidFirstResponseMinutes;
        }

        if (request.ResolutionMinutes < 1)
        {
            return SlaPolicyResult.InvalidResolutionMinutes;
        }

        if (request.IsActive && !policy.IsActive &&
            await db.SlaPolicies.AnyAsync(p => p.Id != id && p.PriorityId == policy.PriorityId && p.IsActive, cancellationToken))
        {
            return SlaPolicyResult.DuplicateActivePolicy;
        }

        policy.FirstResponseMinutes = request.FirstResponseMinutes;
        policy.ResolutionMinutes = request.ResolutionMinutes;
        policy.IsActive = request.IsActive;
        policy.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        var dto = await db.SlaPolicies.AsNoTracking().Where(p => p.Id == id).Select(ToDtoExpression).SingleAsync(cancellationToken);
        return SlaPolicyResult.Success(dto);
    }

    private static readonly Expression<Func<SlaPolicy, SlaPolicyDto>> ToDtoExpression =
        p => new SlaPolicyDto(p.Id, p.PriorityId, p.Priority != null ? p.Priority.Name : null, p.Name, p.FirstResponseMinutes, p.ResolutionMinutes, p.IsActive);
}
