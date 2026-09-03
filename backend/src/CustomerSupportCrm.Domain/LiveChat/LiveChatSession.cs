using CustomerSupportCrm.Domain.Customers;
using CustomerSupportCrm.Domain.Tickets;

namespace CustomerSupportCrm.Domain.LiveChat;

/// <summary>
/// Story 21: the thin binding that lets an anonymous browser tab keep talking to the same ticket
/// across page loads — one session per chat, one ticket per chat (same "one ticket per conversation"
/// shape Story 20 established for WhatsApp/SMS, except a live chat's "conversation" and "ticket" are
/// the same thing for its whole lifetime, so there is nothing to thread or re-link).
/// </summary>
/// <remarks>
/// Deliberately does not duplicate anything <see cref="Tickets.Ticket"/> or <see cref="Customers.CustomerInteraction"/>
/// already model: there is no separate "conversation status" field (Waiting/Active/Closed is derived —
/// see <see cref="Api.LiveChat.LiveChatStatus"/> — from <c>Ticket.AssignedUserId</c>/<c>Ticket.Status</c>),
/// no <c>AssignedUserId</c> (same reason), and no per-message table — every chat message, customer or
/// agent, is written as its own <see cref="Customers.CustomerInteraction"/> row
/// (<c>InteractionType = "livechat_inbound"</c> / <c>"livechat_outbound"</c>), exactly like Story 19/20's
/// channels. This class exists purely to answer "which ticket does this anonymous token speak for."
/// </remarks>
public class LiveChatSession
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>One session per ticket — a closed chat's ticket is never reused for a new session.</summary>
    public Guid TicketId { get; set; }

    public Ticket? Ticket { get; set; }

    public Guid CustomerId { get; set; }

    public Customer? Customer { get; set; }

    /// <summary>Opaque bearer credential handed back to the anonymous widget by <c>StartAsync</c> — proves the caller is the visitor who opened this specific chat, not just anyone who guesses the session id.</summary>
    public string SessionToken { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
