using System.ComponentModel.DataAnnotations;

namespace CustomerSupportCrm.Api.Communications.Inbound;

/// <summary>
/// A normalised inbound WhatsApp/SMS message — for this story, always the manual/dev replay endpoints
/// (<see cref="WhatsAppInboundController"/>/<see cref="SmsInboundController"/>). A real provider
/// integration (Story 46) would construct the same shape from whatever it receives.
/// </summary>
public sealed record InboundMessageRequest(
    [Required, StringLength(64, MinimumLength = 1)] string FromPhoneNumber,
    [StringLength(64)] string? ToPhoneNumber,
    [Required, StringLength(4000, MinimumLength = 1)] string Body,
    [Required, StringLength(255, MinimumLength = 1)] string ExternalMessageId,
    [StringLength(200)] string? ExternalConversationId);

public sealed record InboundMessageResponse(Guid TicketId, Guid CustomerId, bool Deduplicated);
