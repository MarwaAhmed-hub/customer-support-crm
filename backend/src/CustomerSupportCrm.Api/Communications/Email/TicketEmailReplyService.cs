using CustomerSupportCrm.Api.Sla;
using CustomerSupportCrm.Api.Tickets.Tickets;
using CustomerSupportCrm.Domain.Customers;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupportCrm.Api.Communications.Email;

public sealed class TicketEmailReplyService(CrmDbContext db, IEmailSender emailSender, ITicketsService ticketsService, ISlaService sla) : ITicketEmailReplyService
{
    public async Task<TicketEmailReplyResult> SendReplyAsync(Guid ticketId, string body, Guid actingAgentId, CancellationToken cancellationToken = default)
    {
        var ticket = await db.Tickets.Include(t => t.Customer).SingleOrDefaultAsync(t => t.Id == ticketId, cancellationToken);
        if (ticket is null)
        {
            return TicketEmailReplyResult.TicketNotFound;
        }

        if (ticket.SourceChannel != "Email")
        {
            return TicketEmailReplyResult.NotEmailChannel;
        }

        var customerEmail = ticket.Customer?.Email;
        if (string.IsNullOrWhiteSpace(customerEmail))
        {
            return TicketEmailReplyResult.CustomerHasNoEmail;
        }

        var trimmedBody = body.Trim();
        if (trimmedBody.Length == 0)
        {
            return TicketEmailReplyResult.InvalidBody;
        }

        // Threads the reply back to the most recent inbound message, if any — the customer's mail
        // client uses this to keep the conversation in one thread.
        var latestInboundExternalId = await db.CustomerInteractions
            .Where(i => i.TicketId == ticketId && i.InteractionType == "email_inbound")
            .OrderByDescending(i => i.CreatedAt)
            .Select(i => i.ExternalMessageId)
            .FirstOrDefaultAsync(cancellationToken);

        var subject = ticket.Subject.StartsWith("Re: ", StringComparison.OrdinalIgnoreCase) ? ticket.Subject : $"Re: {ticket.Subject}";

        var sendResult = await emailSender.SendAsync(
            new OutgoingEmail(customerEmail, subject, trimmedBody, latestInboundExternalId), cancellationToken);

        if (!sendResult.Success)
        {
            // Story 19 invariant: an outgoing send failure must not persist a "sent" interaction.
            return TicketEmailReplyResult.SendFailed;
        }

        var now = DateTime.UtcNow;
        db.CustomerInteractions.Add(new CustomerInteraction
        {
            CustomerId = ticket.CustomerId,
            TicketId = ticket.Id,
            OccurredAt = now,
            InteractionType = "email_outbound",
            Summary = subject,
            Details = trimmedBody,
            UserId = actingAgentId,
            ExternalMessageId = sendResult.ProviderMessageId,
            ToAddress = customerEmail,
            CreatedAt = now,
        });

        // Deliberately does not touch Ticket.Status/AssignedUserId/UpdatedAt — same "purely additive"
        // boundary Story 18's collaboration comments established.
        await db.SaveChangesAsync(cancellationToken);

        // Story 22: the first successful outbound reply on this ticket marks First Response SLA Met
        // (or Breached, if this arrived after the due time) — a no-op if it's already been marked.
        // Reuses the same instant as the interaction just written above (now is already UTC).
        await sla.MarkFirstResponseAsync(ticketId, new DateTimeOffset(now, TimeSpan.Zero), cancellationToken);

        var dto = await ticketsService.GetAsync(ticketId, cancellationToken);
        return TicketEmailReplyResult.Success(dto!);
    }
}
