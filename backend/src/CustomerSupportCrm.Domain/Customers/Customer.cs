namespace CustomerSupportCrm.Domain.Customers;

/// <summary>
/// A CRM customer's identification and contact details. Independent of <see cref="Users.User"/> —
/// a customer is a person or organisation a CRM user supports, not an account that can log in.
/// </summary>
/// <remarks>
/// A plain, settable POCO — matching <see cref="Departments.Department"/>/<see cref="Branches.Branch"/>'s
/// style. Business rules (required-field trimming, email normalization/format, optional-field
/// null-when-empty) live in <c>CustomerSupportCrm.Api.Customers.CustomersService</c>, not here.
/// Interaction history, notes, attachments, tickets, and multi-tenant isolation (later stories) are
/// deliberately absent — see Story 07's "Not in scope".
/// </remarks>
public class Customer
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string FirstName { get; set; } = default!;

    public string LastName { get; set; } = default!;

    /// <summary>Optional secondary identifier (e.g. the company the customer represents).</summary>
    public string? CompanyName { get; set; }

    /// <summary>
    /// Optional; stored normalized via <see cref="Users.EmailNormalizer"/>, matching
    /// <see cref="Users.User.Email"/>'s normalization — but unlike <c>User.Email</c>, this is not
    /// required and not unique.
    /// </summary>
    public string? Email { get; set; }

    /// <summary>Optional free-form contact phone number; trimmed only, no format transformation.</summary>
    public string? Phone { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
