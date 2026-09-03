using CustomerSupportCrm.Api.Tickets.Tickets;

namespace CustomerSupportCrm.Api.Communications.Email;

public enum TicketEmailReplyOutcome
{
    Success,
    TicketNotFound,

    /// <summary>The ticket's <c>SourceChannel</c> isn't <c>"Email"</c> — nothing to reply through.</summary>
    NotEmailChannel,

    /// <summary>The ticket's customer has no email address on file.</summary>
    CustomerHasNoEmail,

    /// <summary>Body is empty/whitespace-only after trimming.</summary>
    InvalidBody,

    /// <summary><see cref="IEmailSender.SendAsync"/> reported failure — no interaction is persisted.</summary>
    SendFailed,
}

public sealed record TicketEmailReplyResult(TicketEmailReplyOutcome Outcome, TicketDetailDto? Ticket = null)
{
    public static TicketEmailReplyResult Success(TicketDetailDto ticket) => new(TicketEmailReplyOutcome.Success, ticket);
    public static readonly TicketEmailReplyResult TicketNotFound = new(TicketEmailReplyOutcome.TicketNotFound);
    public static readonly TicketEmailReplyResult NotEmailChannel = new(TicketEmailReplyOutcome.NotEmailChannel);
    public static readonly TicketEmailReplyResult CustomerHasNoEmail = new(TicketEmailReplyOutcome.CustomerHasNoEmail);
    public static readonly TicketEmailReplyResult InvalidBody = new(TicketEmailReplyOutcome.InvalidBody);
    public static readonly TicketEmailReplyResult SendFailed = new(TicketEmailReplyOutcome.SendFailed);
}

/// <summary>
/// Sends an agent's reply on an email-sourced ticket via <see cref="IEmailSender"/> and, only on
/// success, persists exactly one outbound <c>CustomerInteraction</c>. Deliberately a standalone
/// service rather than another method on <c>ITicketsService</c> — it depends on <see cref="IEmailSender"/>,
/// which nothing else in the Tickets feature needs, and keeping it separate avoids touching
/// <c>TicketsService</c>'s constructor (and every existing test that instantiates it directly).
/// </summary>
public interface ITicketEmailReplyService
{
    Task<TicketEmailReplyResult> SendReplyAsync(Guid ticketId, string body, Guid actingAgentId, CancellationToken cancellationToken = default);
}
