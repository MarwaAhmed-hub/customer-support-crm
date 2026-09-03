using System.ComponentModel.DataAnnotations;

namespace CustomerSupportCrm.Api.Sla;

public sealed record SlaPolicyDto(
    Guid Id,
    Guid? PriorityId,
    string? PriorityName,
    string Name,
    int FirstResponseMinutes,
    int ResolutionMinutes,
    bool IsActive);

public sealed record UpdateSlaPolicyRequest(
    [Range(1, int.MaxValue)] int FirstResponseMinutes,
    [Range(1, int.MaxValue)] int ResolutionMinutes,
    bool IsActive);
