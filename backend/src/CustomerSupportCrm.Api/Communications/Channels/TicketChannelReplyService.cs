using CustomerSupportCrm.Api.Sla;
using CustomerSupportCrm.Api.Tickets.Tickets;
using CustomerSupportCrm.Domain.Customers;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupportCrm.Api.Communications.Channels;

public sealed class TicketChannelReplyService(CrmDbContext db, IChannelMessageDispatcher dispatcher, ITicketsService ticketsService, ISlaService sla) : ITicketChannelReplyService
{
    private static readonly string[] SendableChannels = ["WhatsApp", "Sms"];

    public async Task<TicketChannelReplyResult> SendReplyAsync(Guid ticketId, string body, Guid actingAgentId, CancellationToken cancellationToken = default)
    {
        var ticket = await db.Tickets.Include(t => t.Customer).SingleOrDefaultAsync(t => t.Id == ticketId, cancellationToken);
        if (ticket is null)
        {
            return TicketChannelReplyResult.TicketNotFound;
        }

        if (ticket.SourceChannel is null || !SendableChannels.Contains(ticket.SourceChannel))
        {
            return TicketChannelReplyResult.NotSendableChannel;
        }

        // Reply to whoever most recently messaged in on this ticket — falls back to the customer's
        // profile phone only when there is no inbound interaction to read it from (e.g. a ticket
        // reused via ExternalConversationId whose first message somehow wasn't recorded).
        var lastInboundFrom = await db.CustomerInteractions
            .Where(i => i.TicketId == ticketId && i.InteractionType == $"{ticket.SourceChannel.ToLowerInvariant()}_inbound")
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => i.FromAddress)
            .FirstOrDefaultAsync(cancellationToken);

        var recipient = lastInboundFrom ?? ticket.Customer?.Phone;
        if (string.IsNullOrWhiteSpace(recipient))
        {
            return TicketChannelReplyResult.NoRecipient;
        }

        var trimmedBody = body.Trim();
        if (trimmedBody.Length == 0)
        {
            return TicketChannelReplyResult.InvalidBody;
        }

        var latestInboundExternalId = await db.CustomerInteractions
            .Where(i => i.TicketId == ticketId && i.InteractionType == $"{ticket.SourceChannel.ToLowerInvariant()}_inbound")
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => i.ExternalMessageId)
            .FirstOrDefaultAsync(cancellationToken);

        var sendResult = await dispatcher.SendAsync(
            ticket.SourceChannel, new ChannelSendRequest(recipient, trimmedBody, latestInboundExternalId), cancellationToken);

        if (!sendResult.Success)
        {
            // Story 19/20 invariant: an outgoing send failure must not persist a "sent" interaction.
            return TicketChannelReplyResult.SendFailed;
        }

        var now = DateTime.UtcNow;
        db.CustomerInteractions.Add(new CustomerInteraction
        {
            CustomerId = ticket.CustomerId,
            TicketId = ticket.Id,
            OccurredAt = now,
            InteractionType = $"{ticket.SourceChannel.ToLowerInvariant()}_outbound",
            Summary = trimmedBody.Length > 80 ? trimmedBody[..80] : trimmedBody,
            Details = trimmedBody,
            UserId = actingAgentId,
            ExternalMessageId = sendResult.ExternalMessageId,
            ToAddress = recipient,
            CreatedAt = now,
        });

        // Deliberately does not touch Ticket.Status/AssignedUserId/UpdatedAt — same "purely additive"
        // boundary the email reply path and Story 18's collaboration comments already established.
        await db.SaveChangesAsync(cancellationToken);

        // Story 22: the first successful outbound reply on this ticket marks First Response SLA Met
        // (or Breached, if this arrived after the due time) — a no-op if it's already been marked.
        // Reuses the same instant as the interaction just written above (now is already UTC).
        await sla.MarkFirstResponseAsync(ticketId, new DateTimeOffset(now, TimeSpan.Zero), cancellationToken);

        var dto = await ticketsService.GetAsync(ticketId, cancellationToken);
        return TicketChannelReplyResult.Success(dto!);
    }
}
