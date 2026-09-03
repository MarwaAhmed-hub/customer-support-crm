namespace CustomerSupportCrm.Api.Audit;

public interface IAuditLogService
{
    Task RecordAsync(
        string action,
        string summary,
        string? entityType = null,
        string? entityId = null,
        object? metadata = null,
        CancellationToken ct = default);

    Task<AuditLogPageDto> QueryAsync(AuditLogQuery query, CancellationToken ct = default);
}
