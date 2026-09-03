using System.Linq.Expressions;
using CustomerSupportCrm.Domain.Tickets;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupportCrm.Api.Tickets.Collaboration;

public sealed class TicketCollaborationCommentsService(CrmDbContext db) : ITicketCollaborationCommentsService
{
    private static readonly Expression<Func<TicketCollaborationComment, TicketCollaborationCommentDto>> ToDtoExpression =
        c => new TicketCollaborationCommentDto(
            c.Id, c.TicketId, c.Body, c.AuthorUserId,
            c.AuthorUser != null ? c.AuthorUser.DisplayName : null,
            c.CreatedAt);

    public async Task<IReadOnlyList<TicketCollaborationCommentDto>?> ListAsync(Guid ticketId, CancellationToken cancellationToken = default)
    {
        if (!await db.Tickets.AnyAsync(t => t.Id == ticketId, cancellationToken))
        {
            return null;
        }

        return await db.TicketCollaborationComments
            .AsNoTracking()
            .Where(c => c.TicketId == ticketId)
            .OrderBy(c => c.CreatedAt)
            .ThenBy(c => c.Id)
            .Select(ToDtoExpression)
            .ToListAsync(cancellationToken);
    }

    public async Task<TicketCollaborationCommentResult> CreateAsync(Guid ticketId, Guid authorUserId, CreateTicketCollaborationCommentRequest request, CancellationToken cancellationToken = default)
    {
        if (!await db.Tickets.AnyAsync(t => t.Id == ticketId, cancellationToken))
        {
            return TicketCollaborationCommentResult.TicketNotFound;
        }

        var body = request.Body.Trim();
        if (body.Length == 0)
        {
            return TicketCollaborationCommentResult.InvalidBody;
        }

        var comment = new TicketCollaborationComment
        {
            TicketId = ticketId,
            Body = body,
            AuthorUserId = authorUserId,
        };

        // Intentionally does not touch the parent Ticket row at all — no Status/AssignedUserId/
        // UpdatedAt write, and no TicketHistory entry. This table is the entire side effect.
        db.TicketCollaborationComments.Add(comment);
        await db.SaveChangesAsync(cancellationToken);

        return TicketCollaborationCommentResult.Success(await LoadDtoAsync(comment.Id, cancellationToken));
    }

    private async Task<TicketCollaborationCommentDto> LoadDtoAsync(Guid id, CancellationToken cancellationToken) =>
        await db.TicketCollaborationComments.AsNoTracking().Where(c => c.Id == id).Select(ToDtoExpression).SingleAsync(cancellationToken);
}
