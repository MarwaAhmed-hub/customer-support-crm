namespace CustomerSupportCrm.Api.Audit;

public record AuditLogListItemDto(
    Guid Id,
    DateTime OccurredAtUtc,
    Guid? ActorUserId,
    string? ActorEmail,
    string Action,
    string? EntityType,
    string? EntityId,
    string Summary,
    string? IpAddress);

public record AuditLogQuery(
    int Page = 1,
    int PageSize = 25,
    string? Action = null,
    string? EntityType = null,
    Guid? ActorUserId = null,
    DateTime? FromUtc = null,
    DateTime? ToUtc = null);

public record AuditLogPageDto(
    IReadOnlyList<AuditLogListItemDto> Items,
    int Page,
    int PageSize,
    int TotalCount);
