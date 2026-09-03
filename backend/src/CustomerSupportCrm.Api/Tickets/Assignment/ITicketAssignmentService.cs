using CustomerSupportCrm.Domain.Tickets;

namespace CustomerSupportCrm.Api.Tickets.Assignment;

public readonly record struct TicketAssignmentResult(bool Assigned, Guid? AssignedUserId, string? Reason);

/// <summary>
/// Story 23: automatic assignment of a still-unassigned ticket to an eligible agent in its
/// category's department. Triggered only from <c>TicketsService.UpdateAsync</c> when an admin
/// classifies the ticket into a non-default business category — never from channel ingestion (a
/// channel-created ticket always lands unassigned under the default "General Inquiry" category; see
/// <c>Communications.ChannelTicketDefaults</c>), and never re-run once a ticket already has an agent.
/// </summary>
public interface ITicketAssignmentService
{
    /// <summary>
    /// Attempts to assign <paramref name="ticket"/> (already mutated with its new
    /// <see cref="Ticket.CategoryId"/>, not yet saved) to an eligible agent. No-ops — returning
    /// <c>Assigned: false</c> — if the ticket already has an agent, its category is the default or has
    /// no department, or no agent is eligible. On success, mutates <c>ticket.AssignedUserId</c> and
    /// stages a "auto-assigned" <c>TicketHistory</c> row plus an <see cref="AssignmentRoundRobinCursor"/>
    /// upsert on the same <c>DbContext</c> the caller will <c>SaveChangesAsync</c> — this method never
    /// saves on its own, so the category change, the assignment, and the history/cursor rows all
    /// commit atomically together (or not at all).
    /// </summary>
    Task<TicketAssignmentResult> TryAutoAssignAsync(Ticket ticket, CancellationToken cancellationToken = default);
}
