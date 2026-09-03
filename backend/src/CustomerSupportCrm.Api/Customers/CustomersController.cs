using CustomerSupportCrm.Api.Audit;
using CustomerSupportCrm.Api.Authorization;
using CustomerSupportCrm.Domain.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupportCrm.Api.Customers;

/// <summary>List/search/create/update/delete customer profiles and contact details. See <see cref="CustomersService"/> for the business rules.</summary>
[ApiController]
[Route("api/customers")]
[Authorize]
public class CustomersController(ICustomersService customersService, IAuditLogService auditLogService) : ControllerBase
{
    [HasPermission(Permissions.Customers.View)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CustomerDto>>> List(
        [FromQuery] string? search, CancellationToken cancellationToken) =>
        Ok(await customersService.ListAsync(search, cancellationToken));

    [HasPermission(Permissions.Customers.View)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CustomerDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var customer = await customersService.GetAsync(id, cancellationToken);
        return customer is null ? NotFound() : Ok(customer);
    }

    [HasPermission(Permissions.Customers.Create)]
    [HttpPost]
    public async Task<ActionResult<CustomerDto>> Create(CreateCustomerRequest request, CancellationToken cancellationToken)
    {
        var result = await customersService.CreateAsync(request, cancellationToken);
        if (result.Outcome == CustomerOperationOutcome.Success)
        {
            await auditLogService.RecordAsync(
                action: "create",
                summary: $"Customer '{result.Customer!.FirstName} {result.Customer.LastName}' created",
                entityType: "Customer",
                entityId: result.Customer.Id.ToString(),
                ct: cancellationToken);
        }
        return result.Outcome switch
        {
            CustomerOperationOutcome.Success => CreatedAtAction(nameof(Get), new { id = result.Customer!.Id }, result.Customer),
            CustomerOperationOutcome.InvalidName => InvalidName(),
            CustomerOperationOutcome.InvalidEmail => InvalidEmail(),
            _ => Problem(statusCode: 500),
        };
    }

    [HasPermission(Permissions.Customers.Update)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CustomerDto>> Update(Guid id, UpdateCustomerRequest request, CancellationToken cancellationToken)
    {
        var result = await customersService.UpdateAsync(id, request, cancellationToken);
        if (result.Outcome == CustomerOperationOutcome.Success)
        {
            await auditLogService.RecordAsync(
                action: "update",
                summary: $"Customer '{result.Customer!.FirstName} {result.Customer.LastName}' updated",
                entityType: "Customer",
                entityId: result.Customer.Id.ToString(),
                ct: cancellationToken);
        }
        return result.Outcome switch
        {
            CustomerOperationOutcome.Success => Ok(result.Customer),
            CustomerOperationOutcome.NotFound => NotFound(),
            CustomerOperationOutcome.InvalidName => InvalidName(),
            CustomerOperationOutcome.InvalidEmail => InvalidEmail(),
            _ => Problem(statusCode: 500),
        };
    }

    [HasPermission(Permissions.Customers.Delete)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var deleted = await customersService.DeleteAsync(id, cancellationToken);
        if (deleted is null)
        {
            return NotFound();
        }

        await auditLogService.RecordAsync(
            action: "delete",
            summary: $"Customer '{deleted.FirstName} {deleted.LastName}' deleted",
            entityType: "Customer",
            entityId: deleted.Id.ToString(),
            ct: cancellationToken);

        return NoContent();
    }

    private BadRequestObjectResult InvalidName() => BadRequest(new { error = "invalid_name" });

    private BadRequestObjectResult InvalidEmail() => BadRequest(new { error = "invalid_email" });
}
