using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using CustomerSupportCrm.Domain.Customers;
using CustomerSupportCrm.Domain.Users;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupportCrm.Api.Customers;

/// <remarks>
/// No duplicate-email/duplicate-phone rule: unlike <c>Users.Email</c>, nothing in the existing
/// codebase establishes a uniqueness convention for a customer's contact details (multiple customer
/// records can legitimately share a household email or a company switchboard number), so create/update
/// persist whatever values are given without a conflict check. See Story 07's edge cases.
/// </remarks>
public sealed class CustomersService(CrmDbContext db) : ICustomersService
{
    // Same validator ASP.NET's built-in [EmailAddress] attribute uses (see UserDtos.cs), applied
    // manually here instead of as a DTO attribute so an absent email (empty string) is accepted
    // rather than rejected — [EmailAddress] treats a non-null empty string as invalid, which is wrong
    // for this optional field.
    private static readonly EmailAddressAttribute EmailValidator = new();

    private static readonly Expression<Func<Customer, CustomerDto>> ToDtoExpression =
        c => new CustomerDto(c.Id, c.FirstName, c.LastName, c.CompanyName, c.Email, c.Phone, c.CreatedAt, c.UpdatedAt);

    public async Task<IReadOnlyList<CustomerDto>> ListAsync(string? search, CancellationToken cancellationToken = default)
    {
        var query = db.Customers.AsNoTracking().AsQueryable();

        var term = search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            // SQL Server's default collation is case-insensitive, so a plain .Contains() suffices —
            // same EF-translated pattern DepartmentsService/UsersController rely on.
            query = query.Where(c =>
                c.FirstName.Contains(term) ||
                c.LastName.Contains(term) ||
                (c.CompanyName != null && c.CompanyName.Contains(term)) ||
                (c.Email != null && c.Email.Contains(term)) ||
                (c.Phone != null && c.Phone.Contains(term)));
        }

        return await query
            .OrderBy(c => c.LastName)
            .ThenBy(c => c.FirstName)
            .Select(ToDtoExpression)
            .ToListAsync(cancellationToken);
    }

    public async Task<CustomerDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var customer = await db.Customers.AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        return customer is null ? null : ToDto(customer);
    }

    public async Task<CustomerDto?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalized = EmailNormalizer.Normalize(email);
        return await db.Customers
            .AsNoTracking()
            .Where(c => c.Email == normalized)
            .OrderBy(c => c.CreatedAt)
            .Select(ToDtoExpression)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<CustomerDto?> GetByPhoneAsync(string normalizedPhone, CancellationToken cancellationToken = default)
    {
        return await db.Customers
            .AsNoTracking()
            .Where(c => c.Phone == normalizedPhone)
            .OrderBy(c => c.CreatedAt)
            .Select(ToDtoExpression)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<CustomerResult> CreateAsync(CreateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        var firstName = request.FirstName.Trim();
        var lastName = request.LastName.Trim();
        if (firstName.Length == 0 || lastName.Length == 0)
        {
            return CustomerResult.InvalidName;
        }

        if (!TryNormalizeEmail(request.Email, out var email))
        {
            return CustomerResult.InvalidEmail;
        }

        var customer = new Customer
        {
            FirstName = firstName,
            LastName = lastName,
            CompanyName = NormalizeOptional(request.CompanyName),
            Email = email,
            Phone = NormalizeOptional(request.Phone),
        };

        db.Customers.Add(customer);
        await db.SaveChangesAsync(cancellationToken);

        return CustomerResult.Success(ToDto(customer));
    }

    public async Task<CustomerResult> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken = default)
    {
        var customer = await db.Customers.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (customer is null)
        {
            return CustomerResult.NotFound;
        }

        var firstName = request.FirstName.Trim();
        var lastName = request.LastName.Trim();
        if (firstName.Length == 0 || lastName.Length == 0)
        {
            return CustomerResult.InvalidName;
        }

        if (!TryNormalizeEmail(request.Email, out var email))
        {
            return CustomerResult.InvalidEmail;
        }

        customer.FirstName = firstName;
        customer.LastName = lastName;
        customer.CompanyName = NormalizeOptional(request.CompanyName);
        customer.Email = email;
        customer.Phone = NormalizeOptional(request.Phone);
        customer.UpdatedAt = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return CustomerResult.Success(ToDto(customer));
    }

    public async Task<CustomerDto?> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var customer = await db.Customers.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (customer is null)
        {
            return null;
        }

        var dto = ToDto(customer);
        db.Customers.Remove(customer);
        await db.SaveChangesAsync(cancellationToken);

        return dto;
    }

    private static bool TryNormalizeEmail(string? rawEmail, out string? normalized)
    {
        var trimmed = rawEmail?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            normalized = null;
            return true;
        }

        if (!EmailValidator.IsValid(trimmed))
        {
            normalized = null;
            return false;
        }

        normalized = EmailNormalizer.Normalize(trimmed);
        return true;
    }

    private static string? NormalizeOptional(string? value)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }

    private static CustomerDto ToDto(Customer customer) =>
        new(customer.Id, customer.FirstName, customer.LastName, customer.CompanyName, customer.Email, customer.Phone, customer.CreatedAt, customer.UpdatedAt);
}
