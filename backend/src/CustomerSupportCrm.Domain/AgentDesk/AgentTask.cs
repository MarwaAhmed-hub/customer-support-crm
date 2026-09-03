using CustomerSupportCrm.Domain.Tickets;
using CustomerSupportCrm.Domain.Users;

namespace CustomerSupportCrm.Domain.AgentDesk;

/// <summary>
/// A personal to-do item owned by exactly one Agent (Story 16). Optionally linked to a
/// <see cref="Tickets.Ticket"/> via <see cref="TicketId"/> — a task created from the Tasks &amp;
/// Reminders page starts unlinked (<c>null</c>), while one created from a ticket's detail page has
/// this set automatically. Never linked to a <see cref="Customers.Customer"/> directly — see the
/// story's "Not in scope". An optional <see cref="ReminderAt"/> drives the Upcoming/Overdue state
/// computed at read time in <c>AgentTasksService</c>, not persisted, since it depends on the current
/// clock rather than on stored data.
/// </summary>
/// <remarks>A plain, settable POCO — matching <see cref="Customers.CustomerNote"/>'s style.</remarks>
public class AgentTask
{
    public Guid Id { get; set; } = Guid.NewGuid();

    /// <summary>Required, unlike <see cref="Customers.CustomerNote.CreatedByUserId"/> — a task with no owner has no meaning, so its row is removed (cascade) rather than orphaned if the owning user is ever deleted.</summary>
    public Guid OwnerUserId { get; set; }

    public User? OwnerUser { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>Optional. Null = a general task with no ticket context. Restrict on delete, matching every other FK on <see cref="Ticket"/> — a ticket is never hard-deleted, only its status changes.</summary>
    public Guid? TicketId { get; set; }

    public Ticket? Ticket { get; set; }

    /// <summary>UTC. Null means no reminder — the task is simply <c>Pending</c> until completed.</summary>
    public DateTime? ReminderAt { get; set; }

    /// <summary>UTC. Null while pending; set once on complete, cleared on reopen.</summary>
    public DateTime? CompletedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
