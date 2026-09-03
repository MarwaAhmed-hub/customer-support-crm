using CustomerSupportCrm.Api.Customers;
using CustomerSupportCrm.Api.Tickets.Tickets;
using CustomerSupportCrm.Domain.Customers;
using CustomerSupportCrm.Domain.Users;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupportCrm.Api.Communications.Email;

/// <summary>
/// Algorithm (see <c>IngestAsync</c>):
/// 1. Idempotency — a redelivered <see cref="IncomingEmailRequest.ExternalMessageId"/> returns the
///    existing ticket without creating anything.
/// 2. Find-or-create the customer by sender address (never created directly — always through
///    <see cref="ICustomersService"/>, matching <see cref="TicketsService"/>'s own convention of never
///    touching a sibling aggregate's table directly).
/// 3. Find-or-link the ticket: an <c>InReplyToMessageId</c> matching an earlier *outbound* interaction's
///    <c>ExternalMessageId</c> means this is a threaded customer reply — reuse that ticket. Otherwise
///    create a new one via <see cref="ITicketsService"/> with <c>SourceChannel = "Email"</c>.
/// 4. Persist exactly one inbound <c>CustomerInteraction</c> — never two, and never one for the "ticket
///    created" side effect <see cref="TicketsService.CreateAsync"/> normally writes (suppressed via its
///    <c>sourceChannel</c> parameter).
/// </summary>
public sealed class EmailIngestionService(CrmDbContext db, ICustomersService customersService, ITicketsService ticketsService, ILogger<EmailIngestionService> logger)
    : IEmailIngestionService
{
    public async Task<EmailIngestionResult> IngestAsync(IncomingEmailRequest email, CancellationToken cancellationToken = default)
    {
        var existing = await db.CustomerInteractions
            .AsNoTracking()
            .Where(i => i.ExternalMessageId == email.ExternalMessageId)
            .Select(i => new { i.TicketId, i.CustomerId })
            .FirstOrDefaultAsync(cancellationToken);
        if (existing is { TicketId: not null })
        {
            logger.LogInformation(
                "Email {ExternalMessageId} already ingested — returning existing ticket {TicketId}",
                email.ExternalMessageId, existing.TicketId);
            return EmailIngestionResult.Reprocessed(existing.TicketId.Value, existing.CustomerId);
        }

        var normalizedFrom = EmailNormalizer.Normalize(email.From);

        var customer = await customersService.GetByEmailAsync(normalizedFrom, cancellationToken);
        if (customer is null)
        {
            var localPart = normalizedFrom.Split('@')[0];
            var createResult = await customersService.CreateAsync(
                new CreateCustomerRequest(localPart, "(via email)", null, normalizedFrom, null), cancellationToken);
            if (createResult.Outcome != CustomerOperationOutcome.Success)
            {
                return EmailIngestionResult.InvalidSender;
            }
            customer = createResult.Customer;
        }

        // Threaded reply: an inbound email whose In-Reply-To matches a message this CRM previously
        // sent (an earlier outbound reply's ExternalMessageId) belongs to that same ticket.
        Guid? threadedTicketId = null;
        if (!string.IsNullOrWhiteSpace(email.InReplyToMessageId))
        {
            threadedTicketId = await db.CustomerInteractions
                .Where(i => i.ExternalMessageId == email.InReplyToMessageId)
                .Select(i => i.TicketId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        Guid ticketId;
        var ticketCreated = false;
        if (threadedTicketId is { } linkedTicketId)
        {
            ticketId = linkedTicketId;
        }
        else
        {
            var (categoryId, priorityId) = await ChannelTicketDefaults.ResolveAsync(db, cancellationToken);
            // Anonymous, like every other channel entry point — attributed to the seeded system
            // account since there is no authenticated caller to attribute it to.
            var systemUserId = await db.Users.Where(u => u.Email == DbSeeder.SystemUserEmail).Select(u => u.Id).SingleAsync(cancellationToken);
            var createTicketResult = await ticketsService.CreateAsync(
                new CreateTicketRequest(customer!.Id, email.Subject.Trim(), email.BodyText.Trim(), categoryId, priorityId),
                systemUserId, sourceChannel: "Email", cancellationToken: cancellationToken);
            if (createTicketResult.Outcome != TicketOperationOutcome.Success)
            {
                throw new InvalidOperationException($"Failed to create ticket for inbound email: {createTicketResult.Outcome}");
            }
            ticketId = createTicketResult.Ticket!.Id;
            ticketCreated = true;
        }

        var now = DateTime.UtcNow;
        db.CustomerInteractions.Add(new CustomerInteraction
        {
            CustomerId = customer!.Id,
            TicketId = ticketId,
            OccurredAt = now,
            InteractionType = "email_inbound",
            Summary = email.Subject.Trim(),
            Details = email.BodyText.Trim(),
            UserId = null,
            ExternalMessageId = email.ExternalMessageId,
            InReplyToMessageId = email.InReplyToMessageId,
            FromAddress = normalizedFrom,
            ToAddress = email.To,
            CreatedAt = now,
        });

        await db.SaveChangesAsync(cancellationToken);

        return EmailIngestionResult.Success(ticketId, customer.Id, ticketCreated);
    }
}
