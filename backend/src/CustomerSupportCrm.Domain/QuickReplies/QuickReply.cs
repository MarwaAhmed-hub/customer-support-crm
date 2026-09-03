namespace CustomerSupportCrm.Domain.QuickReplies;

/// <summary>
/// A reusable response-template Agents can insert into a ticket reply (Story 17) — never sent
/// automatically on any channel; inserting one is purely a text-fill convenience the Agent can still
/// edit before whatever they do next. Independent of any specific ticket or customer.
/// </summary>
/// <remarks>Same shape and rationale as <see cref="Tickets.TicketCategory"/> — see its remarks.</remarks>
public class QuickReply
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Title { get; set; } = string.Empty;

    /// <summary>Upper-invariant form of <see cref="Title"/>, used for the case-insensitive unique index — same pattern as <see cref="Tickets.TicketCategory.NormalizedName"/>.</summary>
    public string NormalizedTitle { get; set; } = string.Empty;

    public string Body { get; set; } = string.Empty;

    /// <summary>Only <c>true</c> replies appear in the ticket composer's picker. Deactivating (via <c>Update</c>) hides a template from the picker while keeping it in the management list; deleting (via <c>QuickRepliesController.Delete</c>) removes it entirely — two separate actions.</summary>
    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
