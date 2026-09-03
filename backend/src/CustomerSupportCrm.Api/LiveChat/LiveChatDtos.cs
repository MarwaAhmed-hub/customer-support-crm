using System.ComponentModel.DataAnnotations;

namespace CustomerSupportCrm.Api.LiveChat;

public sealed record LiveChatMessageDto(
    Guid Id,
    /// <summary>"Customer" | "Agent" — derived from the backing <c>CustomerInteraction.InteractionType</c> suffix.</summary>
    string Sender,
    Guid? SenderUserId,
    string? SenderName,
    string Body,
    DateTime OccurredAt);

public sealed record StartLiveChatSessionRequest(
    [StringLength(128)] string? Name,
    [StringLength(256)] string? Email,
    [StringLength(64)] string? Phone,
    [Required, StringLength(4000, MinimumLength = 1)] string Message);

public sealed record StartLiveChatSessionResponse(Guid SessionId, string SessionToken, Guid TicketId, Guid CustomerId, string Status);

/// <summary>The anonymous widget has no bearer token to authenticate with, so the session token travels in the body instead — same shape as any other "prove you're this session" credential.</summary>
public sealed record SendCustomerLiveChatMessageRequest(
    [Required] string SessionToken,
    [Required, StringLength(4000, MinimumLength = 1)] string Body);

public sealed record SendAgentLiveChatMessageRequest([Required, StringLength(4000, MinimumLength = 1)] string Body);

public sealed record LiveChatSessionPublicDto(Guid SessionId, Guid TicketId, string Status, IReadOnlyList<LiveChatMessageDto> Messages);

public sealed record LiveChatSessionListItemDto(
    Guid SessionId,
    Guid TicketId,
    string Status,
    Guid CustomerId,
    string CustomerName,
    string Subject,
    Guid? AssignedUserId,
    string? AssignedUserName,
    DateTimeOffset CreatedAt,
    DateTime LastMessageAt);

public sealed record LiveChatSessionDetailDto(
    Guid SessionId,
    Guid TicketId,
    string Status,
    Guid CustomerId,
    string CustomerName,
    string Subject,
    Guid? AssignedUserId,
    string? AssignedUserName,
    IReadOnlyList<LiveChatMessageDto> Messages);
