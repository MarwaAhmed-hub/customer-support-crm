namespace CustomerSupportCrm.Api.Customers.Interactions;

public sealed record CustomerInteractionDto(
    Guid Id,
    Guid CustomerId,
    DateTime OccurredAt,
    string InteractionType,
    string? Summary,
    string? Details,
    Guid? UserId,
    string? UserDisplayName);

public sealed record CustomerInteractionListResponse(
    IReadOnlyList<CustomerInteractionDto> Items,
    int Total,
    int Page,
    int PageSize);
