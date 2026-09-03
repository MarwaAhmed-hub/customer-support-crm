namespace CustomerSupportCrm.Api.LiveChat;

public enum LiveChatOperationOutcome
{
    Success,
    SessionNotFound,

    /// <summary>The session id resolved to a real session, but the token in the request doesn't match it.</summary>
    InvalidSessionToken,

    /// <summary>The linked ticket's status is <c>Closed</c> — reuses the existing ticket-status lifecycle (Story 13), not a chat-specific state.</summary>
    ConversationClosed,

    /// <summary>Body is empty/whitespace-only after trimming.</summary>
    InvalidBody,
}

public sealed record LiveChatSessionResult(LiveChatOperationOutcome Outcome, LiveChatSessionPublicDto? Session = null)
{
    public static LiveChatSessionResult Success(LiveChatSessionPublicDto session) => new(LiveChatOperationOutcome.Success, session);
    public static readonly LiveChatSessionResult SessionNotFound = new(LiveChatOperationOutcome.SessionNotFound);
    public static readonly LiveChatSessionResult InvalidSessionToken = new(LiveChatOperationOutcome.InvalidSessionToken);
}

public sealed record LiveChatMessageResult(LiveChatOperationOutcome Outcome, LiveChatMessageDto? Message = null)
{
    public static LiveChatMessageResult Success(LiveChatMessageDto message) => new(LiveChatOperationOutcome.Success, message);
    public static readonly LiveChatMessageResult SessionNotFound = new(LiveChatOperationOutcome.SessionNotFound);
    public static readonly LiveChatMessageResult InvalidSessionToken = new(LiveChatOperationOutcome.InvalidSessionToken);
    public static readonly LiveChatMessageResult ConversationClosed = new(LiveChatOperationOutcome.ConversationClosed);
    public static readonly LiveChatMessageResult InvalidBody = new(LiveChatOperationOutcome.InvalidBody);
}

/// <summary>
/// Story 21: the same find-or-create-customer / create-ticket / write-one-interaction-per-message shape
/// Stories 19/20 established, specialised for a channel with no external provider (the CRM itself is
/// the transport) and no single "one message = done" lifecycle — a chat is a standing conversation, so
/// (unlike WhatsApp/SMS) there is exactly **one** ticket for the conversation's whole lifetime, reused
/// by every message on both sides. See <see cref="LiveChatService"/> for the implementation and
/// <see cref="LiveChatStatus"/> for why there is no stored conversation-status field.
/// </summary>
public interface ILiveChatService
{
    /// <summary>Customer-facing, anonymous. Always succeeds — <see cref="StartLiveChatSessionRequest.Message"/> is the only required field and is validated by its own attribute before this runs.</summary>
    Task<StartLiveChatSessionResponse> StartAsync(StartLiveChatSessionRequest request, CancellationToken cancellationToken = default);

    /// <summary>Customer-facing polling endpoint — validates <paramref name="sessionToken"/> before returning anything.</summary>
    Task<LiveChatSessionResult> GetPublicSessionAsync(Guid sessionId, string sessionToken, CancellationToken cancellationToken = default);

    Task<LiveChatMessageResult> AppendCustomerMessageAsync(Guid sessionId, string sessionToken, string body, CancellationToken cancellationToken = default);

    /// <summary>Agent-facing — no "must be the assigned agent" restriction, matching how every other reply permission in this catalogue works (the permission check alone is the gate, same as <c>tickets.channel.reply</c>).</summary>
    Task<LiveChatMessageResult> AppendAgentMessageAsync(Guid sessionId, Guid agentUserId, string body, CancellationToken cancellationToken = default);

    /// <summary>
    /// Agent-facing inbox. <paramref name="status"/> filters by the derived <see cref="LiveChatStatus"/>
    /// value ("Waiting"/"Active"/"Closed"); null returns every session regardless of status.
    /// <paramref name="scopeToUserId"/>, when given, restricts the result to conversations whose linked
    /// ticket is assigned to that user — see the doc on <see cref="GetForAgentAsync"/> for why.
    /// </summary>
    Task<IReadOnlyList<LiveChatSessionListItemDto>> ListForAgentAsync(string? status, Guid? scopeToUserId = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// <paramref name="scopeToUserId"/>, when given, returns null (same as an unknown session) for a
    /// conversation not assigned to that user — a plain Agent only has their own queue to work, while a
    /// caller who holds <c>tickets.assign</c> (Manager/Admin) already needs full visibility to route
    /// work across the team, so the controller passes null there. This mirrors <see cref="ListForAgentAsync"/>
    /// so a conversation hidden from the list can't be reached by guessing its id either.
    /// </summary>
    Task<LiveChatSessionDetailDto?> GetForAgentAsync(Guid sessionId, Guid? scopeToUserId = null, CancellationToken cancellationToken = default);
}
