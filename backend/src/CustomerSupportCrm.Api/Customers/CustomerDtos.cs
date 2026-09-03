using System.ComponentModel.DataAnnotations;

namespace CustomerSupportCrm.Api.Customers;

public sealed record CustomerDto(
    Guid Id,
    string FirstName,
    string LastName,
    string? CompanyName,
    string? Email,
    string? Phone,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

// Attributes target the primary-constructor parameters directly, not the generated properties —
// matching Departments/DepartmentDtos.cs and Users/UserDtos.cs. Email deliberately has no
// [EmailAddress] attribute here: that attribute treats a non-null empty string as invalid, which
// would wrongly reject "no email given" for this optional field. CustomersService validates format
// manually (only when non-empty) using the same EmailAddressAttribute instead — see its remarks.
public sealed record CreateCustomerRequest(
    [Required, StringLength(128, MinimumLength = 1)] string FirstName,
    [Required, StringLength(128, MinimumLength = 1)] string LastName,
    [StringLength(128)] string? CompanyName,
    [StringLength(256)] string? Email,
    [StringLength(64)] string? Phone);

public sealed record UpdateCustomerRequest(
    [Required, StringLength(128, MinimumLength = 1)] string FirstName,
    [Required, StringLength(128, MinimumLength = 1)] string LastName,
    [StringLength(128)] string? CompanyName,
    [StringLength(256)] string? Email,
    [StringLength(64)] string? Phone);
