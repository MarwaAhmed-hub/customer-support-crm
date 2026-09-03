namespace CustomerSupportCrm.Api.Tickets.History;

public sealed record TicketHistoryDto(
    Guid Id,
    Guid TicketId,
    string EventType,
    string? Field,
    string? PreviousValue,
    string? NewValue,
    string Summary,
    Guid? PerformedByUserId,
    string? PerformedByUserName,
    DateTimeOffset CreatedAt);
