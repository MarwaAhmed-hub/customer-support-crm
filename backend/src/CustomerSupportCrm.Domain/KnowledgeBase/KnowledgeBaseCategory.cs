namespace CustomerSupportCrm.Domain.KnowledgeBase;

/// <summary>
/// Story 26: master data for classifying knowledge base articles — independent of
/// <see cref="Tickets.TicketCategory"/> on purpose (see that story's remarks); the two must be free to
/// evolve separately. Same shape/rationale as <see cref="Departments.Department"/>.
/// </summary>
public class KnowledgeBaseCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    /// <summary>Upper-invariant form of <see cref="Name"/>, used for the case-insensitive unique index — same pattern as <see cref="Departments.Department.NormalizedName"/>.</summary>
    public string NormalizedName { get; set; } = string.Empty;

    /// <summary>Only <c>true</c> categories appear in the article form's picker. Unlike <see cref="Tickets.TicketCategory"/>, this story also allows a real hard delete (see <c>KnowledgeBaseCategoriesService.DeleteAsync</c>) — blocked while any article still references the category.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }
}
