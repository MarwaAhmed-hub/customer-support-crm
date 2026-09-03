namespace CustomerSupportCrm.Api.Customers;

public enum CustomerOperationOutcome
{
    Success,
    NotFound,

    /// <summary>First/last name is empty/whitespace-only after trimming — StringLength(MinimumLength = 1) alone lets a single space through.</summary>
    InvalidName,

    /// <summary>Email is non-empty (after trimming) but fails the standard email-address format check.</summary>
    InvalidEmail,
}

public sealed record CustomerResult(CustomerOperationOutcome Outcome, CustomerDto? Customer = null)
{
    public static CustomerResult Success(CustomerDto customer) => new(CustomerOperationOutcome.Success, customer);
    public static readonly CustomerResult NotFound = new(CustomerOperationOutcome.NotFound);
    public static readonly CustomerResult InvalidName = new(CustomerOperationOutcome.InvalidName);
    public static readonly CustomerResult InvalidEmail = new(CustomerOperationOutcome.InvalidEmail);
}

/// <summary>
/// Business rules for customer profiles: required-field trimming (first/last name), optional-field
/// normalization (company/phone trimmed to null-when-empty, email normalized via
/// <see cref="CustomerSupportCrm.Domain.Users.EmailNormalizer"/>), and email format validation.
/// Modeled directly on <c>Departments.DepartmentsService</c>.
/// </summary>
public interface ICustomersService
{
    /// <summary>Case-insensitive contains match against first/last name, company, email, and phone when <paramref name="search"/> is non-empty; ordered by last name then first name.</summary>
    Task<IReadOnlyList<CustomerDto>> ListAsync(string? search, CancellationToken cancellationToken = default);

    Task<CustomerDto?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Story 19: exact match on the normalized email (case-insensitive via <see cref="CustomerSupportCrm.Domain.Users.EmailNormalizer"/>), used by the Email/Web-Form channel to find-or-create a customer. Email is not unique on <see cref="Customer"/> (see this class's remarks), so when more than one customer shares an address, the oldest record wins — deterministic, but not a guarantee of "the right one."</summary>
    Task<CustomerDto?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// Story 20: exact match on <see cref="Customer.Phone"/> against <paramref name="normalizedPhone"/>
    /// (already run through <c>PhoneNormalizer</c> by the caller), used by the WhatsApp/SMS channel to
    /// find-or-create a customer. <see cref="Customer.Phone"/> is stored exactly as typed everywhere
    /// else (Story 07's convention), so this only reliably matches a customer whose phone was itself
    /// populated via this same channel — see <see cref="CustomerSupportCrm.Domain.Customers.PhoneNormalizer"/>'s
    /// remarks.
    /// </summary>
    Task<CustomerDto?> GetByPhoneAsync(string normalizedPhone, CancellationToken cancellationToken = default);

    Task<CustomerResult> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default);

    Task<CustomerResult> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken = default);

    /// <summary>Hard delete — unlike Departments/Branches, a customer has no IsActive flag to deactivate instead. Returns the deleted record (for audit logging) or null if it did not exist.</summary>
    Task<CustomerDto?> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}
