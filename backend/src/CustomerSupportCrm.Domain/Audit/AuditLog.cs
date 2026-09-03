namespace CustomerSupportCrm.Domain.Audit;

/// <summary>
/// An immutable record of a user or system action for compliance and debugging. Inserted in the same
/// transaction as the originating operation when possible (service methods), or immediately after
/// successful commit. Append-only: no updates or deletes.
/// </summary>
public class AuditLog
{
    public Guid Id { get; set; }

    // When the action occurred (always UTC)
    public DateTime OccurredAtUtc { get; set; }

    // Who did it (nullable for system/anonymous events, e.g. failed login before user resolved)
    public Guid? ActorUserId { get; set; }
    public string? ActorEmail { get; set; }

    // What happened
    public string Action { get; set; } = string.Empty;        // e.g. "user.created", "auth.login.succeeded"
    public string? EntityType { get; set; }                    // e.g. "User", "Role", "Department", "Branch"
    public string? EntityId { get; set; }                      // The affected resource's id

    // Human-friendly one-liner shown in the UI
    public string Summary { get; set; } = string.Empty;

    // Request context
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }

    // Optional structured payload (JSON) with before/after or extra details (capped at 8 KB)
    public string? MetadataJson { get; set; }
}
