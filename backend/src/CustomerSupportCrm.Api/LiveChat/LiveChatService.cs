using CustomerSupportCrm.Api.Communications;
using CustomerSupportCrm.Api.Customers;
using CustomerSupportCrm.Api.Sla;
using CustomerSupportCrm.Api.Tickets.Tickets;
using CustomerSupportCrm.Domain.Customers;
using CustomerSupportCrm.Domain.LiveChat;
using CustomerSupportCrm.Domain.Tickets;
using CustomerSupportCrm.Domain.Users;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupportCrm.Api.LiveChat;

public sealed class LiveChatService(CrmDbContext db, ICustomersService customersService, ITicketsService ticketsService, ISlaService sla) : ILiveChatService
{
    private const string InboundType = "livechat_inbound";
    private const string OutboundType = "livechat_outbound";
    private const int TitleMaxLength = 80;

    public async Task<StartLiveChatSessionResponse> StartAsync(StartLiveChatSessionRequest request, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = string.IsNullOrWhiteSpace(request.Email) ? null : EmailNormalizer.Normalize(request.Email);
        var normalizedPhone = string.IsNullOrWhiteSpace(request.Phone) ? null : PhoneNormalizer.Normalize(request.Phone);

        // Same "either identifier matches an existing customer" rule as the corrected WebFormSubmissionService:
        // phone checked first, then email; whichever matches wins, and a blank field on the found
        // customer is backfilled from this session — never overwritten if already set.
        var customer = normalizedPhone is not null ? await customersService.GetByPhoneAsync(normalizedPhone, cancellationToken) : null;
        customer ??= normalizedEmail is not null ? await customersService.GetByEmailAsync(normalizedEmail, cancellationToken) : null;

        if (customer is null)
        {
            var displayName = string.IsNullOrWhiteSpace(request.Name) ? "Visitor" : request.Name.Trim();
            var (firstName, lastName) = SplitName(displayName);
            var createResult = await customersService.CreateAsync(
                new CreateCustomerRequest(firstName, lastName, null, normalizedEmail, normalizedPhone), cancellationToken);
            customer = createResult.Customer!;
        }
        else
        {
            var needsEmail = string.IsNullOrWhiteSpace(customer.Email) && normalizedEmail is not null;
            var needsPhone = string.IsNullOrWhiteSpace(customer.Phone) && normalizedPhone is not null;
            if (needsEmail || needsPhone)
            {
                var updateResult = await customersService.UpdateAsync(customer.Id, new UpdateCustomerRequest(
                    customer.FirstName,
                    customer.LastName,
                    customer.CompanyName,
                    needsEmail ? normalizedEmail : customer.Email,
                    needsPhone ? normalizedPhone : customer.Phone), cancellationToken);
                if (updateResult.Outcome == CustomerOperationOutcome.Success)
                {
                    customer = updateResult.Customer;
                }
            }
        }

        var (categoryId, priorityId) = await ChannelTicketDefaults.ResolveAsync(db, cancellationToken);
        var message = request.Message.Trim();
        var title = Truncate(message, TitleMaxLength);

        // Anonymous, like the public Web Form — no authenticated agent triggered this, so it is
        // attributed to the same seeded system account.
        var systemUserId = await db.Users.Where(u => u.Email == DbSeeder.SystemUserEmail).Select(u => u.Id).SingleAsync(cancellationToken);

        var createTicketResult = await ticketsService.CreateAsync(
            new CreateTicketRequest(customer!.Id, title, message, categoryId, priorityId),
            systemUserId, sourceChannel: "LiveChat", cancellationToken: cancellationToken);
        if (createTicketResult.Outcome != TicketOperationOutcome.Success)
        {
            throw new InvalidOperationException($"Failed to create ticket for a new live chat session: {createTicketResult.Outcome}");
        }
        var ticket = createTicketResult.Ticket!;

        var session = new LiveChatSession
        {
            TicketId = ticket.Id,
            CustomerId = customer.Id,
            SessionToken = Guid.NewGuid().ToString("N"),
        };
        db.LiveChatSessions.Add(session);

        var now = DateTime.UtcNow;
        db.CustomerInteractions.Add(new CustomerInteraction
        {
            CustomerId = customer.Id,
            TicketId = ticket.Id,
            OccurredAt = now,
            InteractionType = InboundType,
            Summary = title,
            Details = message,
            UserId = null,
            CreatedAt = now,
        });

        await db.SaveChangesAsync(cancellationToken);

        return new StartLiveChatSessionResponse(session.Id, session.SessionToken, ticket.Id, customer.Id, LiveChatStatus.Waiting);
    }

    public async Task<LiveChatSessionResult> GetPublicSessionAsync(Guid sessionId, string sessionToken, CancellationToken cancellationToken = default)
    {
        var session = await db.LiveChatSessions.AsNoTracking().Include(s => s.Ticket).SingleOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (session is null)
        {
            return LiveChatSessionResult.SessionNotFound;
        }
        if (session.SessionToken != sessionToken)
        {
            return LiveChatSessionResult.InvalidSessionToken;
        }

        var messages = await LoadMessagesAsync(session.TicketId, cancellationToken);
        var status = LiveChatStatus.From(session.Ticket!.Status, session.Ticket.AssignedUserId);
        return LiveChatSessionResult.Success(new LiveChatSessionPublicDto(session.Id, session.TicketId, status, messages));
    }

    public async Task<LiveChatMessageResult> AppendCustomerMessageAsync(Guid sessionId, string sessionToken, string body, CancellationToken cancellationToken = default)
    {
        var session = await db.LiveChatSessions.AsNoTracking().Include(s => s.Ticket).SingleOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (session is null)
        {
            return LiveChatMessageResult.SessionNotFound;
        }
        if (session.SessionToken != sessionToken)
        {
            return LiveChatMessageResult.InvalidSessionToken;
        }

        return await AppendMessageAsync(session.TicketId, session.CustomerId, InboundType, body, userId: null, cancellationToken);
    }

    public async Task<LiveChatMessageResult> AppendAgentMessageAsync(Guid sessionId, Guid agentUserId, string body, CancellationToken cancellationToken = default)
    {
        var session = await db.LiveChatSessions.AsNoTracking().SingleOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (session is null)
        {
            return LiveChatMessageResult.SessionNotFound;
        }

        return await AppendMessageAsync(session.TicketId, session.CustomerId, OutboundType, body, agentUserId, cancellationToken);
    }

    private async Task<LiveChatMessageResult> AppendMessageAsync(Guid ticketId, Guid customerId, string interactionType, string body, Guid? userId, CancellationToken cancellationToken)
    {
        var ticket = await db.Tickets.AsNoTracking().Where(t => t.Id == ticketId).Select(t => new { t.Status }).SingleAsync(cancellationToken);
        if (ticket.Status == TicketStatuses.Closed)
        {
            return LiveChatMessageResult.ConversationClosed;
        }

        var trimmedBody = body.Trim();
        if (trimmedBody.Length == 0)
        {
            return LiveChatMessageResult.InvalidBody;
        }

        var now = DateTime.UtcNow;
        var interaction = new CustomerInteraction
        {
            CustomerId = customerId,
            TicketId = ticketId,
            OccurredAt = now,
            InteractionType = interactionType,
            Summary = Truncate(trimmedBody, TitleMaxLength),
            Details = trimmedBody,
            UserId = userId,
            CreatedAt = now,
        };
        db.CustomerInteractions.Add(interaction);
        await db.SaveChangesAsync(cancellationToken);

        // Story 22: an agent's live chat reply is as much a "first outbound agent message" as an Email/
        // WhatsApp/SMS reply — marks First Response SLA Met (or Breached) exactly once; a no-op for the
        // inbound (customer) path and for every reply after the first.
        if (interactionType == OutboundType)
        {
            await sla.MarkFirstResponseAsync(ticketId, new DateTimeOffset(now, TimeSpan.Zero), cancellationToken);
        }

        string? senderName = null;
        if (userId is not null)
        {
            senderName = await db.Users.Where(u => u.Id == userId).Select(u => u.DisplayName).SingleOrDefaultAsync(cancellationToken);
        }

        var sender = interactionType == OutboundType ? "Agent" : "Customer";
        return LiveChatMessageResult.Success(new LiveChatMessageDto(interaction.Id, sender, userId, senderName, trimmedBody, interaction.OccurredAt));
    }

    public async Task<IReadOnlyList<LiveChatSessionListItemDto>> ListForAgentAsync(string? status, Guid? scopeToUserId = null, CancellationToken cancellationToken = default)
    {
        var query = db.LiveChatSessions.AsNoTracking().AsQueryable();

        if (scopeToUserId is not null)
        {
            query = query.Where(s => s.Ticket!.AssignedUserId == scopeToUserId);
        }

        if (status == LiveChatStatus.Closed)
        {
            query = query.Where(s => s.Ticket!.Status == TicketStatuses.Closed);
        }
        else if (status == LiveChatStatus.Active)
        {
            query = query.Where(s => s.Ticket!.Status != TicketStatuses.Closed && s.Ticket.AssignedUserId != null);
        }
        else if (status == LiveChatStatus.Waiting)
        {
            query = query.Where(s => s.Ticket!.Status != TicketStatuses.Closed && s.Ticket.AssignedUserId == null);
        }

        return await query
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new LiveChatSessionListItemDto(
                s.Id,
                s.TicketId,
                s.Ticket!.Status == TicketStatuses.Closed ? LiveChatStatus.Closed : (s.Ticket.AssignedUserId != null ? LiveChatStatus.Active : LiveChatStatus.Waiting),
                s.CustomerId,
                s.Customer!.FirstName + " " + s.Customer.LastName,
                s.Ticket.Subject,
                s.Ticket.AssignedUserId,
                s.Ticket.AssignedUser != null ? s.Ticket.AssignedUser.DisplayName : null,
                s.CreatedAt,
                db.CustomerInteractions
                    .Where(i => i.TicketId == s.TicketId && (i.InteractionType == InboundType || i.InteractionType == OutboundType))
                    .Max(i => (DateTime?)i.CreatedAt) ?? s.CreatedAt.UtcDateTime))
            .ToListAsync(cancellationToken);
    }

    public async Task<LiveChatSessionDetailDto?> GetForAgentAsync(Guid sessionId, Guid? scopeToUserId = null, CancellationToken cancellationToken = default)
    {
        var session = await db.LiveChatSessions
            .AsNoTracking()
            .Include(s => s.Ticket).ThenInclude(t => t!.AssignedUser)
            .Include(s => s.Customer)
            .SingleOrDefaultAsync(s => s.Id == sessionId, cancellationToken);
        if (session is null)
        {
            return null;
        }
        if (scopeToUserId is not null && session.Ticket!.AssignedUserId != scopeToUserId)
        {
            // Same "not yours, so it doesn't exist as far as you're concerned" treatment as an unknown
            // id — a scoped-out agent gets a 404, not a 403 that would confirm the conversation exists.
            return null;
        }

        var messages = await LoadMessagesAsync(session.TicketId, cancellationToken);
        var status = LiveChatStatus.From(session.Ticket!.Status, session.Ticket.AssignedUserId);

        return new LiveChatSessionDetailDto(
            session.Id,
            session.TicketId,
            status,
            session.CustomerId,
            session.Customer!.FirstName + " " + session.Customer.LastName,
            session.Ticket.Subject,
            session.Ticket.AssignedUserId,
            session.Ticket.AssignedUser?.DisplayName,
            messages);
    }

    private async Task<IReadOnlyList<LiveChatMessageDto>> LoadMessagesAsync(Guid ticketId, CancellationToken cancellationToken) =>
        await db.CustomerInteractions
            .AsNoTracking()
            .Where(i => i.TicketId == ticketId && (i.InteractionType == InboundType || i.InteractionType == OutboundType))
            .OrderBy(i => i.CreatedAt)
            .Select(i => new LiveChatMessageDto(
                i.Id,
                i.InteractionType == OutboundType ? "Agent" : "Customer",
                i.UserId,
                i.User != null ? i.User.DisplayName : null,
                i.Details ?? string.Empty,
                i.OccurredAt))
            .ToListAsync(cancellationToken);

    private static (string FirstName, string LastName) SplitName(string fullName)
    {
        var parts = fullName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        return parts.Length == 2 ? (parts[0], parts[1]) : (parts[0], "(live chat)");
    }

    private static string Truncate(string value, int maxLength) => value.Length <= maxLength ? value : value[..maxLength];
}
