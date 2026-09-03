namespace CustomerSupportCrm.Api.Communications.Email;

public enum EmailIngestionOutcome
{
    Success,
    AlreadyProcessed,

    /// <summary>The sender address failed customer creation (format rejected by <c>CustomersService</c> despite passing this DTO's own <c>[EmailAddress]</c> check — practically unreachable, kept for completeness).</summary>
    InvalidSender,
}

public sealed record EmailIngestionResult(EmailIngestionOutcome Outcome, Guid? TicketId = null, Guid? CustomerId = null, bool TicketCreated = false)
{
    public static EmailIngestionResult Success(Guid ticketId, Guid customerId, bool ticketCreated) =>
        new(EmailIngestionOutcome.Success, ticketId, customerId, ticketCreated);

    public static EmailIngestionResult Reprocessed(Guid ticketId, Guid customerId) =>
        new(EmailIngestionOutcome.AlreadyProcessed, ticketId, customerId);

    public static readonly EmailIngestionResult InvalidSender = new(EmailIngestionOutcome.InvalidSender);
}

/// <summary>
/// Normalises an inbound email into (find-or-create Customer) + (find-or-link Ticket) + (exactly one
/// inbound <c>CustomerInteraction</c>). See <see cref="EmailIngestionService"/> for the algorithm.
/// </summary>
public interface IEmailIngestionService
{
    /// <summary>Anonymous, like every other channel entry point (Story 19 correction) — the created ticket is attributed to the seeded system account, not a caller, since there is none.</summary>
    Task<EmailIngestionResult> IngestAsync(IncomingEmailRequest email, CancellationToken cancellationToken = default);
}
