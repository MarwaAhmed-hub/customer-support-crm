namespace CustomerSupportCrm.Api.Customers.Notes;

public enum CustomerNoteOperationOutcome
{
    Success,
    CustomerNotFound,
    NoteNotFound,

    /// <summary>Body is empty/whitespace-only after trimming — StringLength(MinimumLength = 1) alone lets a single space through.</summary>
    InvalidBody,
}

public sealed record CustomerNoteResult(CustomerNoteOperationOutcome Outcome, CustomerNoteDto? Note = null)
{
    public static CustomerNoteResult Success(CustomerNoteDto note) => new(CustomerNoteOperationOutcome.Success, note);
    public static readonly CustomerNoteResult CustomerNotFound = new(CustomerNoteOperationOutcome.CustomerNotFound);
    public static readonly CustomerNoteResult NoteNotFound = new(CustomerNoteOperationOutcome.NoteNotFound);
    public static readonly CustomerNoteResult InvalidBody = new(CustomerNoteOperationOutcome.InvalidBody);
}

/// <summary>Free-text notes attached to a customer — not interaction-history records (Story 08). Modeled on <c>Departments.DepartmentsService</c>.</summary>
public interface ICustomerNotesService
{
    /// <summary>Newest first. A null return means the customer does not exist — the controller turns that into a 404; an empty (non-null) list means the customer exists with no notes yet.</summary>
    Task<IReadOnlyList<CustomerNoteDto>?> ListAsync(Guid customerId, CancellationToken cancellationToken = default);

    /// <summary>A null return covers both "customer not found" and "note not found" — the controller returns 404 either way.</summary>
    Task<CustomerNoteDto?> GetAsync(Guid customerId, Guid noteId, CancellationToken cancellationToken = default);

    Task<CustomerNoteResult> CreateAsync(Guid customerId, Guid? actorUserId, CreateCustomerNoteRequest request, CancellationToken cancellationToken = default);

    Task<CustomerNoteResult> UpdateAsync(Guid customerId, Guid noteId, UpdateCustomerNoteRequest request, CancellationToken cancellationToken = default);

    /// <summary>False covers both "customer not found" and "note not found".</summary>
    Task<bool> DeleteAsync(Guid customerId, Guid noteId, CancellationToken cancellationToken = default);
}
