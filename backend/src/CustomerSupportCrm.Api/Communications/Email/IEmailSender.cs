namespace CustomerSupportCrm.Api.Communications.Email;

/// <summary>
/// Story 19: provider-agnostic outbound email. The only implementation in this story is
/// <see cref="NullEmailSender"/> — a real SMTP/API-based provider is a follow-up (see the TODO on its
/// DI registration in <c>Program.cs</c>).
/// </summary>
public interface IEmailSender
{
    Task<EmailSendResult> SendAsync(OutgoingEmail message, CancellationToken cancellationToken = default);
}

public sealed record OutgoingEmail(
    string ToAddress,
    string Subject,
    string BodyText,
    string? InReplyToMessageId = null);

public sealed record EmailSendResult(bool Success, string? ProviderMessageId, string? Error)
{
    public static EmailSendResult Succeeded(string providerMessageId) => new(true, providerMessageId, null);
    public static EmailSendResult Failed(string error) => new(false, null, error);
}
