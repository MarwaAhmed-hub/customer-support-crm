using CustomerSupportCrm.Api.Audit;
using CustomerSupportCrm.Api.Authorization;
using CustomerSupportCrm.Domain.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupportCrm.Api.Tickets.Priorities;

/// <summary>List/create/update ticket priorities (master data). No delete endpoint — deactivate via <see cref="Update"/> instead. See <see cref="TicketPrioritiesService"/> for the business rules.</summary>
[ApiController]
[Route("api/tickets/priorities")]
[Authorize]
public class TicketPrioritiesController(ITicketPrioritiesService prioritiesService, IAuditLogService auditLogService) : ControllerBase
{
    [HasPermission(Permissions.Tickets.PrioritiesView)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TicketPriorityDto>>> List(
        [FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        Ok(await prioritiesService.ListAsync(includeInactive, cancellationToken));

    [HasPermission(Permissions.Tickets.PrioritiesView)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TicketPriorityDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var priority = await prioritiesService.GetAsync(id, cancellationToken);
        return priority is null ? NotFound() : Ok(priority);
    }

    [HasPermission(Permissions.Tickets.PrioritiesManage)]
    [HttpPost]
    public async Task<ActionResult<TicketPriorityDto>> Create(CreateTicketPriorityRequest request, CancellationToken cancellationToken)
    {
        var result = await prioritiesService.CreateAsync(request, cancellationToken);
        if (result.Outcome == TicketPriorityOperationOutcome.Success)
        {
            await auditLogService.RecordAsync(
                action: "create",
                summary: $"Ticket priority '{result.Priority!.Name}' created",
                entityType: "TicketPriority",
                entityId: result.Priority.Id.ToString(),
                ct: cancellationToken);
        }
        return result.Outcome switch
        {
            TicketPriorityOperationOutcome.Success => CreatedAtAction(nameof(Get), new { id = result.Priority!.Id }, result.Priority),
            TicketPriorityOperationOutcome.InvalidName => InvalidName(),
            TicketPriorityOperationOutcome.DuplicateName => DuplicateName(),
            _ => Problem(statusCode: 500),
        };
    }

    [HasPermission(Permissions.Tickets.PrioritiesManage)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TicketPriorityDto>> Update(Guid id, UpdateTicketPriorityRequest request, CancellationToken cancellationToken)
    {
        var result = await prioritiesService.UpdateAsync(id, request, cancellationToken);
        if (result.Outcome == TicketPriorityOperationOutcome.Success)
        {
            await auditLogService.RecordAsync(
                action: "update",
                summary: $"Ticket priority '{result.Priority!.Name}' updated",
                entityType: "TicketPriority",
                entityId: result.Priority.Id.ToString(),
                ct: cancellationToken);
        }
        return result.Outcome switch
        {
            TicketPriorityOperationOutcome.Success => Ok(result.Priority),
            TicketPriorityOperationOutcome.NotFound => NotFound(),
            TicketPriorityOperationOutcome.InvalidName => InvalidName(),
            TicketPriorityOperationOutcome.DuplicateName => DuplicateName(),
            _ => Problem(statusCode: 500),
        };
    }

    private BadRequestObjectResult InvalidName() => BadRequest(new { error = "invalid_name" });

    private ObjectResult DuplicateName() => Conflict(new { error = "duplicate_ticket_priority_name" });
}
