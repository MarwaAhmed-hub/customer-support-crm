using CustomerSupportCrm.Api.Authorization;
using CustomerSupportCrm.Domain.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupportCrm.Api.Tickets.History;

/// <summary>Read-only ticket-lifecycle timeline (Story 14). No POST/PUT/DELETE — entries are written only as a side effect of <c>TicketsService</c> mutations.</summary>
[ApiController]
[Route("api/tickets/{ticketId:guid}/history")]
[Authorize]
public class TicketHistoryController(ITicketHistoryService historyService) : ControllerBase
{
    [HasPermission(Permissions.Tickets.View)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TicketHistoryDto>>> Get(Guid ticketId, CancellationToken cancellationToken)
    {
        if (!await historyService.TicketExistsAsync(ticketId, cancellationToken))
        {
            return NotFound();
        }

        return Ok(await historyService.GetForTicketAsync(ticketId, cancellationToken));
    }
}
