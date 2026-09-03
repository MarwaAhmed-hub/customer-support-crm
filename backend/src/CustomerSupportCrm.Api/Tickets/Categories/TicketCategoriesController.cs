using CustomerSupportCrm.Api.Audit;
using CustomerSupportCrm.Api.Authorization;
using CustomerSupportCrm.Domain.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupportCrm.Api.Tickets.Categories;

/// <summary>List/create/update ticket categories (master data). No delete endpoint — deactivate via <see cref="Update"/> instead. See <see cref="TicketCategoriesService"/> for the business rules.</summary>
[ApiController]
[Route("api/tickets/categories")]
[Authorize]
public class TicketCategoriesController(ITicketCategoriesService categoriesService, IAuditLogService auditLogService) : ControllerBase
{
    [HasPermission(Permissions.Tickets.CategoriesView)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TicketCategoryDto>>> List(
        [FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        Ok(await categoriesService.ListAsync(includeInactive, cancellationToken));

    [HasPermission(Permissions.Tickets.CategoriesView)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<TicketCategoryDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var category = await categoriesService.GetAsync(id, cancellationToken);
        return category is null ? NotFound() : Ok(category);
    }

    [HasPermission(Permissions.Tickets.CategoriesManage)]
    [HttpPost]
    public async Task<ActionResult<TicketCategoryDto>> Create(CreateTicketCategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await categoriesService.CreateAsync(request, cancellationToken);
        if (result.Outcome == TicketCategoryOperationOutcome.Success)
        {
            await auditLogService.RecordAsync(
                action: "create",
                summary: $"Ticket category '{result.Category!.Name}' created",
                entityType: "TicketCategory",
                entityId: result.Category.Id.ToString(),
                ct: cancellationToken);
        }
        return result.Outcome switch
        {
            TicketCategoryOperationOutcome.Success => CreatedAtAction(nameof(Get), new { id = result.Category!.Id }, result.Category),
            TicketCategoryOperationOutcome.InvalidName => InvalidName(),
            TicketCategoryOperationOutcome.DuplicateName => DuplicateName(),
            TicketCategoryOperationOutcome.InvalidDepartment => InvalidDepartment(),
            _ => Problem(statusCode: 500),
        };
    }

    [HasPermission(Permissions.Tickets.CategoriesManage)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<TicketCategoryDto>> Update(Guid id, UpdateTicketCategoryRequest request, CancellationToken cancellationToken)
    {
        var result = await categoriesService.UpdateAsync(id, request, cancellationToken);
        if (result.Outcome == TicketCategoryOperationOutcome.Success)
        {
            await auditLogService.RecordAsync(
                action: "update",
                summary: $"Ticket category '{result.Category!.Name}' updated",
                entityType: "TicketCategory",
                entityId: result.Category.Id.ToString(),
                ct: cancellationToken);
        }
        return result.Outcome switch
        {
            TicketCategoryOperationOutcome.Success => Ok(result.Category),
            TicketCategoryOperationOutcome.NotFound => NotFound(),
            TicketCategoryOperationOutcome.InvalidName => InvalidName(),
            TicketCategoryOperationOutcome.DuplicateName => DuplicateName(),
            TicketCategoryOperationOutcome.InvalidDepartment => InvalidDepartment(),
            _ => Problem(statusCode: 500),
        };
    }

    private BadRequestObjectResult InvalidName() => BadRequest(new { error = "invalid_name" });

    private BadRequestObjectResult InvalidDepartment() => BadRequest(new { error = "invalid_department" });

    private ObjectResult DuplicateName() => Conflict(new { error = "duplicate_ticket_category_name" });
}
