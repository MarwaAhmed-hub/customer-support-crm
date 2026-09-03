using CustomerSupportCrm.Domain.Users;

namespace CustomerSupportCrm.Domain.Customers;

/// <summary>
/// A free-text note attached to a <see cref="Customer"/> — not an interaction-history record (Story
/// 08) and not a ticket note; see Story 09's "Not in scope".
/// </summary>
/// <remarks>A plain, settable POCO — matching <see cref="Branches.Branch"/>/<see cref="Departments.Department"/>'s style.</remarks>
public class CustomerNote
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid CustomerId { get; set; }

    public string Body { get; set; } = string.Empty;

    /// <summary>Nullable — the author's account may have since been deleted; the note itself is never removed as a result (see the SetNull FK in <c>CrmDbContext</c>).</summary>
    public Guid? CreatedByUserId { get; set; }

    public User? CreatedByUser { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>Null until the first update.</summary>
    public DateTime? UpdatedAt { get; set; }
}
