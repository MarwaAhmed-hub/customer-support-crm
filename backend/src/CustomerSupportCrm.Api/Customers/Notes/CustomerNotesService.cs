using System.Linq.Expressions;
using CustomerSupportCrm.Domain.Customers;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupportCrm.Api.Customers.Notes;

public sealed class CustomerNotesService(CrmDbContext db) : ICustomerNotesService
{
    private static readonly Expression<Func<CustomerNote, CustomerNoteDto>> ToDtoExpression =
        n => new CustomerNoteDto(
            n.Id, n.CustomerId, n.Body, n.CreatedByUserId,
            n.CreatedByUserId != null ? n.CreatedByUser!.DisplayName : null,
            n.CreatedAt, n.UpdatedAt);

    public async Task<IReadOnlyList<CustomerNoteDto>?> ListAsync(Guid customerId, CancellationToken cancellationToken = default)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == customerId, cancellationToken))
        {
            return null;
        }

        return await db.CustomerNotes
            .AsNoTracking()
            .Where(n => n.CustomerId == customerId)
            .OrderByDescending(n => n.CreatedAt)
            .Select(ToDtoExpression)
            .ToListAsync(cancellationToken);
    }

    public async Task<CustomerNoteDto?> GetAsync(Guid customerId, Guid noteId, CancellationToken cancellationToken = default)
    {
        return await db.CustomerNotes
            .AsNoTracking()
            .Where(n => n.CustomerId == customerId && n.Id == noteId)
            .Select(ToDtoExpression)
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<CustomerNoteResult> CreateAsync(Guid customerId, Guid? actorUserId, CreateCustomerNoteRequest request, CancellationToken cancellationToken = default)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == customerId, cancellationToken))
        {
            return CustomerNoteResult.CustomerNotFound;
        }

        var body = request.Body.Trim();
        if (body.Length == 0)
        {
            return CustomerNoteResult.InvalidBody;
        }

        var note = new CustomerNote
        {
            CustomerId = customerId,
            Body = body,
            CreatedByUserId = actorUserId,
        };

        db.CustomerNotes.Add(note);
        await db.SaveChangesAsync(cancellationToken);

        return CustomerNoteResult.Success(await LoadDtoAsync(note.Id, cancellationToken));
    }

    public async Task<CustomerNoteResult> UpdateAsync(Guid customerId, Guid noteId, UpdateCustomerNoteRequest request, CancellationToken cancellationToken = default)
    {
        if (!await db.Customers.AnyAsync(c => c.Id == customerId, cancellationToken))
        {
            return CustomerNoteResult.CustomerNotFound;
        }

        var note = await db.CustomerNotes.SingleOrDefaultAsync(n => n.CustomerId == customerId && n.Id == noteId, cancellationToken);
        if (note is null)
        {
            return CustomerNoteResult.NoteNotFound;
        }

        var body = request.Body.Trim();
        if (body.Length == 0)
        {
            return CustomerNoteResult.InvalidBody;
        }

        note.Body = body;
        // Stamped unconditionally, even if the trimmed body is unchanged — keeps the "edited at"
        // signal simple rather than diffing for a no-op edit.
        note.UpdatedAt = DateTime.UtcNow;

        await db.SaveChangesAsync(cancellationToken);

        return CustomerNoteResult.Success(await LoadDtoAsync(note.Id, cancellationToken));
    }

    public async Task<bool> DeleteAsync(Guid customerId, Guid noteId, CancellationToken cancellationToken = default)
    {
        var note = await db.CustomerNotes.SingleOrDefaultAsync(n => n.CustomerId == customerId && n.Id == noteId, cancellationToken);
        if (note is null)
        {
            return false;
        }

        db.CustomerNotes.Remove(note);
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<CustomerNoteDto> LoadDtoAsync(Guid noteId, CancellationToken cancellationToken) =>
        await db.CustomerNotes.AsNoTracking().Where(n => n.Id == noteId).Select(ToDtoExpression).SingleAsync(cancellationToken);
}
