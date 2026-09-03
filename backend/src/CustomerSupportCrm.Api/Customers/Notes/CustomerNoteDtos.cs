using System.ComponentModel.DataAnnotations;

namespace CustomerSupportCrm.Api.Customers.Notes;

public sealed record CustomerNoteDto(
    Guid Id,
    Guid CustomerId,
    string Body,
    Guid? CreatedByUserId,
    string? CreatedByDisplayName,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

// Attributes target the primary-constructor parameters directly, not the generated properties —
// matching Departments/DepartmentDtos.cs. MinimumLength = 1 alone lets a single space through, so the
// service still rejects a whitespace-only Body after trimming — see CustomerNotesService.
public sealed record CreateCustomerNoteRequest([Required, StringLength(4000, MinimumLength = 1)] string Body);

public sealed record UpdateCustomerNoteRequest([Required, StringLength(4000, MinimumLength = 1)] string Body);
