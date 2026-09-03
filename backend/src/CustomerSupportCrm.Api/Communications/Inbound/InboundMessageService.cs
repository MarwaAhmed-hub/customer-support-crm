using CustomerSupportCrm.Api.Customers;
using CustomerSupportCrm.Api.Tickets.Tickets;
using CustomerSupportCrm.Domain.Customers;
using CustomerSupportCrm.Domain.Tickets;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupportCrm.Api.Communications.Inbound;

/// <summary>
/// Algorithm (see <c>IngestAsync</c>) — mirrors <see cref="Email.EmailIngestionService"/> with phone
/// lookup instead of email.
/// </summary>
/// <remarks>
/// This ticketing rule has been through two corrections; this is the third and current shape.
/// <list type="number">
/// <item>Original: reuse the customer's most recent open ticket <em>on this channel</em>, with no
/// regard for which conversation a message actually belonged to — every message after the first on a
/// still-open channel silently disappeared into interaction history instead of raising its own ticket,
/// merging unrelated conversations together.</item>
/// <item>First correction: stopped reusing tickets at all — every distinct inbound message always
/// opened its own new ticket, and <see cref="InboundMessageRequest.ExternalConversationId"/> became
/// purely informational metadata stamped on the ticket, never used to look anything up.</item>
/// <item><b>Current:</b> a message carrying an <see cref="InboundMessageRequest.ExternalConversationId"/>
/// that matches an existing, still-open ticket <em>for the same customer and channel</em> is added to
/// that ticket as a new interaction instead of opening another one — this is the narrow, correct
/// version of (1): reuse is scoped to one exact provider conversation, not "any open ticket on this
/// channel". A message with no conversation id, or whose conversation id matches nothing open, falls
/// through to opening a new ticket exactly as in (2).</item>
/// </list>
/// 1. Idempotency — a redelivered <see cref="InboundMessageRequest.ExternalMessageId"/> returns the
///    existing ticket without creating anything (a provider retry, not a second message).
/// 2. Find-or-create the customer by phone (never created directly — always through
///    <see cref="ICustomersService"/>).
/// 3. Reuse an open ticket for this customer/channel/conversation id when one exists; otherwise create
///    a new ticket via <see cref="ITicketsService"/> with <c>SourceChannel = channel</c> and stamp
///    <see cref="InboundMessageRequest.ExternalConversationId"/> onto it so a later message in the same
///    conversation can find it.
/// 4. Persist exactly one inbound <c>CustomerInteraction</c> (<c>InteractionType = "{channel}_inbound"</c>)
///    against whichever ticket resulted from step 3 — never one for the "ticket created" side effect
///    <see cref="TicketsService.CreateAsync"/> normally writes (suppressed via its <c>sourceChannel</c>
///    parameter, and not applicable at all when reusing an existing ticket).
/// </summary>
public sealed class InboundMessageService(CrmDbContext db, ICustomersService customersService, ITicketsService ticketsService, ILogger<InboundMessageService> logger)
    : IInboundMessageService
{
    private const int TitleMaxLength = 80;

    public async Task<InboundMessageResult> IngestAsync(string channel, InboundMessageRequest request, CancellationToken cancellationToken = default)
    {
        var existing = await db.CustomerInteractions
            .AsNoTracking()
            .Where(i => i.ExternalMessageId == request.ExternalMessageId)
            .Select(i => new { i.TicketId, i.CustomerId })
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is { TicketId: not null })
        {
            logger.LogInformation(
                "{Channel} message {ExternalMessageId} already ingested — returning existing ticket {TicketId}",
                channel, request.ExternalMessageId, existing.TicketId);
            return new InboundMessageResult(existing.TicketId.Value, existing.CustomerId, Deduplicated: true);
        }

        var normalizedPhone = PhoneNormalizer.Normalize(request.FromPhoneNumber);

        var customer = await customersService.GetByPhoneAsync(normalizedPhone, cancellationToken);
        if (customer is null)
        {
            var createResult = await customersService.CreateAsync(
                new CreateCustomerRequest(normalizedPhone, $"(via {channel})", null, null, normalizedPhone), cancellationToken);
            if (createResult.Outcome != CustomerOperationOutcome.Success)
            {
                throw new InvalidOperationException($"Failed to create customer for inbound {channel} message: {createResult.Outcome}");
            }
            customer = createResult.Customer;
        }

        // Conversation-scoped reuse (see this class's remarks for the two corrections that led here):
        // only a still-open ticket for this exact customer/channel/conversation id is reused. No
        // conversation id, a closed ticket, or no match at all — every one of those falls through to
        // creating a new ticket below, same as before.
        Guid? reusableTicketId = null;
        if (!string.IsNullOrWhiteSpace(request.ExternalConversationId))
        {
            reusableTicketId = await db.Tickets
                .Where(t =>
                    t.CustomerId == customer!.Id &&
                    t.SourceChannel == channel &&
                    t.ExternalConversationId == request.ExternalConversationId &&
                    t.Status != TicketStatuses.Closed)
                .OrderByDescending(t => t.CreatedAt)
                .Select(t => (Guid?)t.Id)
                .FirstOrDefaultAsync(cancellationToken);
        }

        Guid ticketId;
        if (reusableTicketId is { } existingTicketId)
        {
            ticketId = existingTicketId;
        }
        else
        {
            var (categoryId, priorityId) = await ChannelTicketDefaults.ResolveAsync(db, cancellationToken);
            var title = Truncate(request.Body.Trim(), TitleMaxLength);
            // Anonymous, like every other channel entry point — attributed to the seeded system
            // account since there is no authenticated caller to attribute it to.
            var systemUserId = await db.Users.Where(u => u.Email == DbSeeder.SystemUserEmail).Select(u => u.Id).SingleAsync(cancellationToken);
            var createTicketResult = await ticketsService.CreateAsync(
                new CreateTicketRequest(customer!.Id, title, request.Body.Trim(), categoryId, priorityId),
                systemUserId, sourceChannel: channel, cancellationToken: cancellationToken);
            if (createTicketResult.Outcome != TicketOperationOutcome.Success)
            {
                throw new InvalidOperationException($"Failed to create ticket for inbound {channel} message: {createTicketResult.Outcome}");
            }
            ticketId = createTicketResult.Ticket!.Id;

            if (!string.IsNullOrWhiteSpace(request.ExternalConversationId))
            {
                // TicketsService.CreateAsync's request DTO is shared with the authenticated create-ticket
                // UI, which has no such field, so ExternalConversationId can't be set in that call —
                // stamp it here instead, on a freshly tracked load, folded into the same
                // SaveChangesAsync as the interaction insert below (no extra round trip). This is what
                // lets a later message in the same conversation find and reuse this ticket.
                var createdTicket = await db.Tickets.SingleAsync(t => t.Id == ticketId, cancellationToken);
                createdTicket.ExternalConversationId = request.ExternalConversationId;
            }
        }

        var now = DateTime.UtcNow;
        db.CustomerInteractions.Add(new CustomerInteraction
        {
            CustomerId = customer!.Id,
            TicketId = ticketId,
            OccurredAt = now,
            InteractionType = $"{channel.ToLowerInvariant()}_inbound",
            Summary = Truncate(request.Body.Trim(), TitleMaxLength),
            Details = request.Body.Trim(),
            UserId = null,
            ExternalMessageId = request.ExternalMessageId,
            FromAddress = normalizedPhone,
            ToAddress = request.ToPhoneNumber,
            CreatedAt = now,
        });

        await db.SaveChangesAsync(cancellationToken);

        return new InboundMessageResult(ticketId, customer!.Id, Deduplicated: false);
    }

    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
}
