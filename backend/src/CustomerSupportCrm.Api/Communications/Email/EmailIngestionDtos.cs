using System.ComponentModel.DataAnnotations;

namespace CustomerSupportCrm.Api.Communications.Email;

/// <summary>
/// A normalised inbound message, whatever its origin — for this story, always the manual/dev replay
/// endpoint (<see cref="EmailIngestionController"/>). A real provider integration (a follow-up) would
/// construct the same shape from whatever it receives.
/// </summary>
public sealed record IncomingEmailRequest(
    [Required, EmailAddress, StringLength(320)] string From,
    [StringLength(320)] string? To,
    [Required, StringLength(200, MinimumLength = 1)] string Subject,
    [Required, StringLength(4000, MinimumLength = 1)] string BodyText,
    [Required, StringLength(255, MinimumLength = 1)] string ExternalMessageId,
    [StringLength(255)] string? InReplyToMessageId);

public sealed record EmailIngestionResponse(Guid TicketId, Guid CustomerId, bool AlreadyProcessed);
