namespace CustomerSupportCrm.Api.Communications.Channels;

/// <summary>Resolves the registered <see cref="IChannelMessageSender"/> for a channel and delegates. Throws <see cref="NotSupportedException"/> if none is registered — a startup/DI-configuration bug, not a runtime condition callers should catch.</summary>
public interface IChannelMessageDispatcher
{
    Task<ChannelSendResult> SendAsync(string channel, ChannelSendRequest request, CancellationToken cancellationToken = default);
}

public sealed class ChannelMessageDispatcher(IEnumerable<IChannelMessageSender> senders) : IChannelMessageDispatcher
{
    public Task<ChannelSendResult> SendAsync(string channel, ChannelSendRequest request, CancellationToken cancellationToken = default)
    {
        var sender = senders.FirstOrDefault(s => s.Channel == channel);
        if (sender is null)
        {
            throw new NotSupportedException($"No {nameof(IChannelMessageSender)} is registered for channel '{channel}'.");
        }

        return sender.SendAsync(request, cancellationToken);
    }
}
