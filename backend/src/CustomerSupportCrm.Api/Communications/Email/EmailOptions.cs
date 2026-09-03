namespace CustomerSupportCrm.Api.Communications.Email;

/// <summary>Bound from the <c>"Email"</c> configuration section. Only <see cref="FromAddress"/> is read anywhere in this story (by <see cref="NullEmailSender"/>'s log line); <see cref="Provider"/> exists so a future concrete implementation has a config-driven on/off switch without inventing one at that point.</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>"None" in this story — the only sender registered is <see cref="NullEmailSender"/>. A concrete provider (e.g. "Smtp") is a follow-up.</summary>
    public string Provider { get; set; } = "None";

    public string FromAddress { get; set; } = "support@localhost";
}
