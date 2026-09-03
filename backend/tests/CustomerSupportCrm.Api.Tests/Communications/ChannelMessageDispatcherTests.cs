using CustomerSupportCrm.Api.Communications.Channels;
using Xunit;

namespace CustomerSupportCrm.Api.Tests.Communications;

/// <summary>Records every call and returns a configurable canned result — never touches anything real.</summary>
public sealed class FakeChannelMessageSender(string channel, ChannelSendResult result) : IChannelMessageSender
{
    public string Channel { get; } = channel;
    public List<ChannelSendRequest> SentMessages { get; } = [];

    public Task<ChannelSendResult> SendAsync(ChannelSendRequest request, CancellationToken cancellationToken = default)
    {
        SentMessages.Add(request);
        return Task.FromResult(result);
    }
}

public class ChannelMessageDispatcherTests
{
    [Fact]
    public async Task SendAsync_routes_to_the_sender_registered_for_the_requested_channel()
    {
        var whatsapp = new FakeChannelMessageSender("WhatsApp", ChannelSendResult.Succeeded("wa-out-1"));
        var sms = new FakeChannelMessageSender("Sms", ChannelSendResult.Succeeded("sms-out-1"));
        var dispatcher = new ChannelMessageDispatcher([whatsapp, sms]);

        var result = await dispatcher.SendAsync("Sms", new ChannelSendRequest("+201001234567", "Hello"));

        Assert.True(result.Success);
        Assert.Equal("sms-out-1", result.ExternalMessageId);
        Assert.Single(sms.SentMessages);
        Assert.Empty(whatsapp.SentMessages);
    }

    [Fact]
    public async Task SendAsync_throws_NotSupportedException_when_no_sender_is_registered_for_the_channel()
    {
        var dispatcher = new ChannelMessageDispatcher([]);

        await Assert.ThrowsAsync<NotSupportedException>(
            () => dispatcher.SendAsync("WhatsApp", new ChannelSendRequest("+201001234567", "Hello")));
    }
}
