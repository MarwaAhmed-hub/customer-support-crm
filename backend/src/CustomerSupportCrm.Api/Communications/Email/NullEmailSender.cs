using Microsoft.Extensions.Options;

namespace CustomerSupportCrm.Api.Communications.Email;

/// <summary>
/// Default <see cref="IEmailSender"/> — logs the message and reports success with a synthesised
/// provider message id. Nothing is actually sent anywhere. This is deliberate: this story ships the
/// abstraction and a way to exercise the full ingest → ticket → reply flow end to end without a real
/// mailbox; wiring a concrete provider is out of scope (see the TODO in <c>Program.cs</c>).
/// </summary>
public sealed class NullEmailSender(IOptions<EmailOptions> options, ILogger<NullEmailSender> logger) : IEmailSender
{
    public Task<EmailSendResult> SendAsync(OutgoingEmail message, CancellationToken cancellationToken = default)
    {
        logger.LogInformation(
            "NullEmailSender: would send from {From} to {To}, subject {Subject}",
            options.Value.FromAddress, message.ToAddress, message.Subject);

        return Task.FromResult(EmailSendResult.Succeeded($"dev-{Guid.NewGuid()}"));
    }
}
