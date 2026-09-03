using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupportCrm.Api.Communications;

/// <summary>
/// Shared "which category/priority does a channel-originated ticket get" resolution for the Email and
/// Web-Form ingestion services (Story 19) — neither channel lets the sender pick one. Both fall back
/// to the seeded "General Inquiry" category and "Medium" priority (see
/// <c>DbSeeder.SeedDefaultTicketCategoriesAndPrioritiesAsync</c>), or the first active one (by name /
/// sort order) if an admin has since renamed or deactivated those.
/// </summary>
internal static class ChannelTicketDefaults
{
    // Story 23: every caller of ResolveAsync — this one plus InboundMessageService (WhatsApp/SMS) and
    // LiveChatService — lands the new ticket on the default category, unassigned. Automatic assignment
    // (TicketAssignmentService) never fires here; it only runs once an admin reclassifies the ticket
    // into a non-default category via TicketsService.UpdateAsync.
    public static async Task<(Guid CategoryId, Guid PriorityId)> ResolveAsync(CrmDbContext db, CancellationToken cancellationToken)
    {
        var categoryId = await db.TicketCategories.AsNoTracking()
            .Where(c => c.IsActive)
            .OrderBy(c => c.NormalizedName == "GENERAL INQUIRY" ? 0 : 1)
            .ThenBy(c => c.Name)
            .Select(c => (Guid?)c.Id)
            .FirstOrDefaultAsync(cancellationToken);

        var priorityId = await db.TicketPriorities.AsNoTracking()
            .Where(p => p.IsActive)
            .OrderBy(p => p.NormalizedName == "MEDIUM" ? 0 : 1)
            .ThenBy(p => p.SortOrder)
            .Select(p => (Guid?)p.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (categoryId is null || priorityId is null)
        {
            throw new InvalidOperationException("No active ticket category/priority exists to assign a channel-originated ticket to.");
        }

        return (categoryId.Value, priorityId.Value);
    }
}
