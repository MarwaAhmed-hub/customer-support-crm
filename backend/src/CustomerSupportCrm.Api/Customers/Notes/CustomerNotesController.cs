using CustomerSupportCrm.Api.Auth;
using CustomerSupportCrm.Api.Authorization;
using CustomerSupportCrm.Domain.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupportCrm.Api.Customers.Notes;

/// <summary>Free-text notes attached to a customer — see <see cref="CustomerNotesService"/> for the business rules. Not interaction-history (Story 08).</summary>
[ApiController]
[Route("api/customers/{customerId:guid}/notes")]
[Authorize]
public class CustomerNotesController(ICustomerNotesService notesService) : ControllerBase
{
    [HasPermission(Permissions.Customers.NotesRead)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CustomerNoteDto>>> List(Guid customerId, CancellationToken cancellationToken)
    {
        var notes = await notesService.ListAsync(customerId, cancellationToken);
        return notes is null ? NotFound() : Ok(notes);
    }

    [HasPermission(Permissions.Customers.NotesCreate)]
    [HttpPost]
    public async Task<ActionResult<CustomerNoteDto>> Create(Guid customerId, CreateCustomerNoteRequest request, CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        var result = await notesService.CreateAsync(customerId, actorUserId, request, cancellationToken);
        return result.Outcome switch
        {
            CustomerNoteOperationOutcome.Success => CreatedAtAction(nameof(List), new { customerId }, result.Note),
            CustomerNoteOperationOutcome.CustomerNotFound => NotFound(),
            CustomerNoteOperationOutcome.InvalidBody => InvalidBody(),
            _ => Problem(statusCode: 500),
        };
    }

    [HasPermission(Permissions.Customers.NotesUpdate)]
    [HttpPut("{noteId:guid}")]
    public async Task<ActionResult<CustomerNoteDto>> Update(Guid customerId, Guid noteId, UpdateCustomerNoteRequest request, CancellationToken cancellationToken)
    {
        var result = await notesService.UpdateAsync(customerId, noteId, request, cancellationToken);
        return result.Outcome switch
        {
            CustomerNoteOperationOutcome.Success => Ok(result.Note),
            CustomerNoteOperationOutcome.CustomerNotFound => NotFound(),
            CustomerNoteOperationOutcome.NoteNotFound => NotFound(),
            CustomerNoteOperationOutcome.InvalidBody => InvalidBody(),
            _ => Problem(statusCode: 500),
        };
    }

    [HasPermission(Permissions.Customers.NotesDelete)]
    [HttpDelete("{noteId:guid}")]
    public async Task<IActionResult> Delete(Guid customerId, Guid noteId, CancellationToken cancellationToken)
    {
        var deleted = await notesService.DeleteAsync(customerId, noteId, cancellationToken);
        return deleted ? NoContent() : NotFound();
    }

    private BadRequestObjectResult InvalidBody() => BadRequest(new { error = "note.body_required" });
}
