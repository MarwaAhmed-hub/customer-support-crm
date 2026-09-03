namespace CustomerSupportCrm.Api.Communications.Channels;

/// <summary>
/// Default <see cref="IChannelMessageSender"/> for both WhatsApp and Sms (two DI-registered instances,
/// one per channel — see <c>Program.cs</c>) — logs the message and reports success with a synthesised
/// external message id. Nothing is actually sent anywhere; a real Twilio/Meta provider is a follow-up
/// (Story 46), same "abstraction + dev implementation only" scope as <see cref="Email.NullEmailSender"/>.
/// </summary>
public sealed class NoOpChannelMessageSender(string channel, ILogger<NoOpChannelMessageSender> logger) : IChannelMessageSender
{
    public string Channel { get; } = channel;

    public Task<ChannelSendResult> SendAsync(ChannelSendRequest request, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "NoOpChannelMessageSender[{Channel}]: would send to {To}: {Body}",
            Channel, request.ToAddress, request.Body);

        return Task.FromResult(ChannelSendResult.Succeeded($"noop-{Channel.ToLowerInvariant()}-{Guid.NewGuid():N}"));
    }
}
