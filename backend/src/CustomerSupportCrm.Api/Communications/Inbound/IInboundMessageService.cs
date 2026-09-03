namespace CustomerSupportCrm.Api.Communications.Inbound;

public sealed record InboundMessageResult(Guid TicketId, Guid CustomerId, bool Deduplicated);

/// <summary>
/// Normalises an inbound WhatsApp/SMS message into (find-or-create Customer by phone) + (find-or-link
/// Ticket) + (exactly one inbound <c>CustomerInteraction</c>). See <see cref="InboundMessageService"/>
/// for the algorithm — the WhatsApp/SMS analogue of <see cref="Email.EmailIngestionService"/>.
/// </summary>
public interface IInboundMessageService
{
    /// <summary>Anonymous, like every other channel entry point (correction — see <see cref="InboundMessageService"/>) — the created ticket is attributed to the seeded system account, not a caller, since there is none.</summary>
    Task<InboundMessageResult> IngestAsync(string channel, InboundMessageRequest request, CancellationToken cancellationToken = default);
}
