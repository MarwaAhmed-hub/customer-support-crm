using System.Text.Json.Serialization;
using CustomerSupportCrm.Domain.Notifications;
using CustomerSupportCrm.Domain.Tickets;

namespace CustomerSupportCrm.Api.Notifications;

/// <summary>Enums serialize as their names, matching <c>TicketEscalationDto</c>'s own convention (see its remarks).</summary>
public sealed record NotificationDto(
    Guid Id,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] NotificationEventType EventType,
    [property: JsonConverter(typeof(JsonStringEnumConverter))] SlaType? SlaType,
    Guid TicketId,
    string Subject,
    string Body,
    DateTime CreatedAtUtc,
    DateTime? ReadAtUtc);

public sealed record NotificationListResponse(IReadOnlyList<NotificationDto> Items, int Total, int Page, int PageSize);
