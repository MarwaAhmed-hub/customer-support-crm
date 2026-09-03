using System.Security.Claims;
using System.Text.Json;
using CustomerSupportCrm.Api.Auth;
using CustomerSupportCrm.Domain.Audit;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupportCrm.Api.Audit;

public class AuditLogService(
    CrmDbContext db,
    IHttpContextAccessor httpContextAccessor,
    ILogger<AuditLogService> logger) : IAuditLogService
{
    private const int MaxMetadataBytes = 8192;

    public async Task RecordAsync(
        string action,
        string summary,
        string? entityType = null,
        string? entityId = null,
        object? metadata = null,
        CancellationToken ct = default)
    {
        try
        {
            var context = httpContextAccessor.HttpContext;
            var actorUserId = context?.User.GetUserId();
            var actorEmail = context?.User.FindFirst("email")?.Value;
            var ipAddress = context?.Connection.RemoteIpAddress?.ToString();
            var userAgent = context?.Request.Headers.UserAgent.ToString();

            var metadataJson = metadata is not null
                ? SerializeMetadata(metadata)
                : null;

            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                OccurredAtUtc = DateTime.UtcNow,
                ActorUserId = actorUserId,
                ActorEmail = actorEmail,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                Summary = summary,
                IpAddress = ipAddress,
                UserAgent = userAgent,
                MetadataJson = metadataJson,
            };

            db.AuditLogs.Add(auditLog);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to record audit log: {Action}", action);
        }
    }

    public async Task<AuditLogPageDto> QueryAsync(AuditLogQuery query, CancellationToken ct = default)
    {
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var page = Math.Max(query.Page, 1);

        var q = db.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            q = q.Where(a => a.Action == query.Action);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            q = q.Where(a => a.EntityType == query.EntityType);
        }

        if (query.ActorUserId.HasValue)
        {
            q = q.Where(a => a.ActorUserId == query.ActorUserId);
        }

        if (query.FromUtc.HasValue)
        {
            q = q.Where(a => a.OccurredAtUtc >= query.FromUtc);
        }

        if (query.ToUtc.HasValue)
        {
            q = q.Where(a => a.OccurredAtUtc <= query.ToUtc);
        }

        var total = await q.CountAsync(ct);

        var items = await q
            .OrderByDescending(a => a.OccurredAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new AuditLogListItemDto(
                a.Id,
                a.OccurredAtUtc,
                a.ActorUserId,
                a.ActorEmail,
                a.Action,
                a.EntityType,
                a.EntityId,
                a.Summary,
                a.IpAddress))
            .ToListAsync(ct);

        return new AuditLogPageDto(items, page, pageSize, total);
    }

    private static string SerializeMetadata(object metadata)
    {
        try
        {
            var json = JsonSerializer.Serialize(metadata);
            if (json.Length > MaxMetadataBytes)
            {
                return json[..(MaxMetadataBytes - 3)] + "...";
            }
            return json;
        }
        catch
        {
            return "{}";
        }
    }
}
