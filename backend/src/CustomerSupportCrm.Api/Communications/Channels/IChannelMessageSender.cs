namespace CustomerSupportCrm.Api.Communications.Channels;

/// <summary>
/// Story 20: provider-agnostic outbound WhatsApp/SMS — the WhatsApp/SMS analogue of
/// <see cref="Email.IEmailSender"/>. <see cref="Channel"/> matches <c>Ticket.SourceChannel</c>'s
/// string convention ("WhatsApp" / "Sms"), not a separate enum.
/// </summary>
public interface IChannelMessageSender
{
    string Channel { get; }

    Task<ChannelSendResult> SendAsync(ChannelSendRequest request, CancellationToken cancellationToken = default);
}

public sealed record ChannelSendRequest(string ToAddress, string Body, string? ReplyToExternalMessageId = null);

public sealed record ChannelSendResult(bool Success, string? ExternalMessageId, string? Error)
{
    public static ChannelSendResult Succeeded(string externalMessageId) => new(true, externalMessageId, null);
    public static ChannelSendResult Failed(string error) => new(false, null, error);
}
