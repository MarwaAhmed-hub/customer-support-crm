namespace CustomerSupportCrm.Api.Sla;

public enum SlaPolicyOperationOutcome
{
    Success,
    NotFound,

    /// <summary><c>FirstResponseMinutes</c> is less than 1 — the <c>[Range]</c> attribute already rejects this over HTTP; this outcome exists for direct service callers.</summary>
    InvalidFirstResponseMinutes,

    /// <summary><c>ResolutionMinutes</c> is less than 1 — same reasoning as <see cref="InvalidFirstResponseMinutes"/>.</summary>
    InvalidResolutionMinutes,

    /// <summary>Activating this policy would leave two active policies for the same <c>PriorityId</c> — rejected here rather than surfacing the unique filtered index's raw constraint violation.</summary>
    DuplicateActivePolicy,
}

public sealed record SlaPolicyResult(SlaPolicyOperationOutcome Outcome, SlaPolicyDto? Policy = null)
{
    public static SlaPolicyResult Success(SlaPolicyDto policy) => new(SlaPolicyOperationOutcome.Success, policy);
    public static readonly SlaPolicyResult NotFound = new(SlaPolicyOperationOutcome.NotFound);
    public static readonly SlaPolicyResult InvalidFirstResponseMinutes = new(SlaPolicyOperationOutcome.InvalidFirstResponseMinutes);
    public static readonly SlaPolicyResult InvalidResolutionMinutes = new(SlaPolicyOperationOutcome.InvalidResolutionMinutes);
    public static readonly SlaPolicyResult DuplicateActivePolicy = new(SlaPolicyOperationOutcome.DuplicateActivePolicy);
}

/// <summary>
/// Story 22: minimal admin surface over the <see cref="Domain.Sla.SlaPolicy"/> rows <see cref="ISlaService"/>
/// picks from at ticket creation. List + update only — no create/delete in this story; <c>DbSeeder</c>'s
/// seeded "Default SLA" row is the source of truth for the policy set.
/// </summary>
public interface ISlaPoliciesService
{
    Task<IReadOnlyList<SlaPolicyDto>> ListActiveAsync(CancellationToken cancellationToken = default);

    /// <summary>Updates <c>FirstResponseMinutes</c>/<c>ResolutionMinutes</c>/<c>IsActive</c> only — never touches which ticket priority a policy applies to.</summary>
    Task<SlaPolicyResult> UpdateAsync(Guid id, UpdateSlaPolicyRequest request, CancellationToken cancellationToken = default);
}
