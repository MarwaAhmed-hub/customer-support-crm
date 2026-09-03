namespace CustomerSupportCrm.Api.Communications.WebForms;

public enum WebFormSubmissionOutcome
{
    Success,

    /// <summary>The hidden honeypot field was filled — treated as a bot, not a real error. The controller still returns 202 so a bot cannot distinguish this from a real submission.</summary>
    HoneypotTriggered,

    InvalidEmail,
}

public sealed record WebFormSubmissionResult(WebFormSubmissionOutcome Outcome, Guid? TicketId = null, Guid? CustomerId = null)
{
    public static WebFormSubmissionResult Success(Guid ticketId, Guid customerId) => new(WebFormSubmissionOutcome.Success, ticketId, customerId);
    public static readonly WebFormSubmissionResult HoneypotTriggered = new(WebFormSubmissionOutcome.HoneypotTriggered);
    public static readonly WebFormSubmissionResult InvalidEmail = new(WebFormSubmissionOutcome.InvalidEmail);
}

/// <summary>
/// The same find-or-create-customer / create-ticket path as <see cref="Email.EmailIngestionService"/>,
/// but for an anonymous public submission: no authenticated agent exists to attribute the ticket to
/// (see <c>DbSeeder.SeedSystemUserAsync</c>), and a honeypot field guards against naive bots.
/// </summary>
public interface IWebFormSubmissionService
{
    Task<WebFormSubmissionResult> SubmitAsync(WebFormSubmissionRequest request, CancellationToken cancellationToken = default);
}
