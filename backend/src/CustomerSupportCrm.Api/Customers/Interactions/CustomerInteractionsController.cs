using CustomerSupportCrm.Api.Authorization;
using CustomerSupportCrm.Domain.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupportCrm.Api.Customers.Interactions;

/// <summary>Read-only interaction history for a single customer — see <see cref="CustomerInteractionsService"/>. No POST/PUT/DELETE in this story.</summary>
[ApiController]
[Route("api/customers/{customerId:guid}/interactions")]
[Authorize]
public class CustomerInteractionsController(ICustomerInteractionsService interactionsService) : ControllerBase
{
    [HasPermission(Permissions.Customers.InteractionsRead)]
    [HttpGet]
    [ProducesResponseType(typeof(CustomerInteractionListResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CustomerInteractionListResponse>> List(
        Guid customerId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] Guid? ticketId = null,
        CancellationToken cancellationToken = default)
    {
        var result = await interactionsService.ListForCustomerAsync(customerId, page, pageSize, ticketId, cancellationToken);
        return result is null ? NotFound() : Ok(result);
    }
}
