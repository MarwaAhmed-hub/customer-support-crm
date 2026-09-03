using CustomerSupportCrm.Domain.Tickets;

namespace CustomerSupportCrm.Api.LiveChat;

/// <summary>
/// Story 21: a chat conversation's <c>Waiting</c> / <c>Active</c> / <c>Closed</c> status is entirely
/// <b>derived</b> from the linked <see cref="Ticket"/> — never stored on <see cref="Domain.LiveChat.LiveChatSession"/>
/// itself. Assignment already exists (<c>Ticket.AssignedUserId</c>, Story 12); closing/reopening
/// already exists (<c>Ticket.Status</c>, Story 13's <c>Closed</c>/<c>InProgress</c> transition). Live
/// Chat reuses both rather than tracking its own copy — an agent assigns or closes a chat exactly the
/// same way they assign or close any other ticket.
/// </summary>
public static class LiveChatStatus
{
    public const string Waiting = "Waiting";
    public const string Active = "Active";
    public const string Closed = "Closed";

    public static string From(string ticketStatus, Guid? assignedUserId) =>
        ticketStatus == TicketStatuses.Closed ? Closed
        : assignedUserId is not null ? Active
        : Waiting;
}
