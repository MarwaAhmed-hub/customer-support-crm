using CustomerSupportCrm.Api.Tickets.Tickets;

namespace CustomerSupportCrm.Api.Communications.Channels;

public enum TicketChannelReplyOutcome
{
    Success,
    TicketNotFound,

    /// <summary>The ticket's <c>SourceChannel</c> isn't a channel <see cref="IChannelMessageDispatcher"/> can send through (i.e. not "WhatsApp"/"Sms") — nothing to reply through.</summary>
    NotSendableChannel,

    /// <summary>No recipient phone number: the most recent inbound interaction has none and the customer has no phone on file either.</summary>
    NoRecipient,

    /// <summary>Body is empty/whitespace-only after trimming.</summary>
    InvalidBody,

    /// <summary><see cref="IChannelMessageSender.SendAsync"/> reported failure — no interaction is persisted.</summary>
    SendFailed,
}

public sealed record TicketChannelReplyResult(TicketChannelReplyOutcome Outcome, TicketDetailDto? Ticket = null)
{
    public static TicketChannelReplyResult Success(TicketDetailDto ticket) => new(TicketChannelReplyOutcome.Success, ticket);
    public static readonly TicketChannelReplyResult TicketNotFound = new(TicketChannelReplyOutcome.TicketNotFound);
    public static readonly TicketChannelReplyResult NotSendableChannel = new(TicketChannelReplyOutcome.NotSendableChannel);
    public static readonly TicketChannelReplyResult NoRecipient = new(TicketChannelReplyOutcome.NoRecipient);
    public static readonly TicketChannelReplyResult InvalidBody = new(TicketChannelReplyOutcome.InvalidBody);
    public static readonly TicketChannelReplyResult SendFailed = new(TicketChannelReplyOutcome.SendFailed);
}

/// <summary>
/// Sends an agent's reply on a WhatsApp/SMS-sourced ticket via <see cref="IChannelMessageDispatcher"/>
/// and, only on success, persists exactly one outbound <c>CustomerInteraction</c>. The WhatsApp/SMS
/// analogue of <c>Tickets.Tickets.ITicketEmailReplyService</c> — kept as a separate service rather than
/// unified with it, since the two go through different provider abstractions (<see cref="IEmailSender"/>-
/// esque vs the channel dispatcher) and unifying them would mean reworking the already-shipped, tested
/// email reply path for no functional gain.
/// </summary>
public interface ITicketChannelReplyService
{
    Task<TicketChannelReplyResult> SendReplyAsync(Guid ticketId, string body, Guid actingAgentId, CancellationToken cancellationToken = default);
}
