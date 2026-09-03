using CustomerSupportCrm.Api.Audit;
using CustomerSupportCrm.Api.Auth;
using CustomerSupportCrm.Api.Authorization;
using CustomerSupportCrm.Domain.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupportCrm.Api.AgentDesk.Tasks;

/// <summary>Personal to-do items for the authenticated Agent — see <see cref="AgentTasksService"/> for the owner-scoping that keeps one Agent from ever seeing another's tasks.</summary>
[ApiController]
[Route("api/agent-desk/tasks")]
[Authorize]
public class AgentTasksController(IAgentTasksService tasksService, IAuditLogService auditLogService) : ControllerBase
{
    [HasPermission(Permissions.AgentTasks.Read)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AgentTaskDto>>> List(
        [FromQuery] bool? includeCompleted,
        [FromQuery] AgentTaskState? state,
        [FromQuery] Guid? ticketId,
        CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        if (actorUserId is null)
        {
            return Unauthorized();
        }

        return Ok(await tasksService.ListAsync(actorUserId.Value, includeCompleted, state, ticketId, cancellationToken));
    }

    [HasPermission(Permissions.AgentTasks.Read)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AgentTaskDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        if (actorUserId is null)
        {
            return Unauthorized();
        }

        var task = await tasksService.GetAsync(actorUserId.Value, id, cancellationToken);
        return task is null ? NotFound() : Ok(task);
    }

    [HasPermission(Permissions.AgentTasks.Create)]
    [HttpPost]
    public async Task<ActionResult<AgentTaskDto>> Create(CreateAgentTaskRequest request, CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        if (actorUserId is null)
        {
            return Unauthorized();
        }

        var result = await tasksService.CreateAsync(actorUserId.Value, request, cancellationToken);
        if (result.Outcome == AgentTaskOperationOutcome.Success)
        {
            await auditLogService.RecordAsync(
                action: "agenttask.create",
                summary: $"Task '{result.Task!.Title}' created",
                entityType: "AgentTask",
                entityId: result.Task.Id.ToString(),
                ct: cancellationToken);
        }
        return result.Outcome switch
        {
            AgentTaskOperationOutcome.Success => CreatedAtAction(nameof(Get), new { id = result.Task!.Id }, result.Task),
            AgentTaskOperationOutcome.InvalidTitle => Invalid("invalid_title"),
            AgentTaskOperationOutcome.TicketNotFound => NotFound(new { error = "ticket_not_found" }),
            _ => Problem(statusCode: 500),
        };
    }

    [HasPermission(Permissions.AgentTasks.Update)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<AgentTaskDto>> Update(Guid id, UpdateAgentTaskRequest request, CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        if (actorUserId is null)
        {
            return Unauthorized();
        }

        var result = await tasksService.UpdateAsync(actorUserId.Value, id, request, cancellationToken);
        if (result.Outcome == AgentTaskOperationOutcome.Success)
        {
            await auditLogService.RecordAsync(
                action: "agenttask.update",
                summary: $"Task '{result.Task!.Title}' updated",
                entityType: "AgentTask",
                entityId: result.Task.Id.ToString(),
                ct: cancellationToken);
        }
        return result.Outcome switch
        {
            AgentTaskOperationOutcome.Success => Ok(result.Task),
            AgentTaskOperationOutcome.NotFound => NotFound(),
            AgentTaskOperationOutcome.InvalidTitle => Invalid("invalid_title"),
            AgentTaskOperationOutcome.TicketNotFound => NotFound(new { error = "ticket_not_found" }),
            _ => Problem(statusCode: 500),
        };
    }

    /// <summary>Idempotent — completing an already-completed task just returns it unchanged (see <see cref="AgentTasksService.CompleteAsync"/>), so this still writes an audit entry each call rather than trying to detect the no-op case.</summary>
    [HasPermission(Permissions.AgentTasks.Complete)]
    [HttpPost("{id:guid}/complete")]
    public async Task<ActionResult<AgentTaskDto>> Complete(Guid id, CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        if (actorUserId is null)
        {
            return Unauthorized();
        }

        var task = await tasksService.CompleteAsync(actorUserId.Value, id, completed: true, cancellationToken);
        if (task is null)
        {
            return NotFound();
        }

        await auditLogService.RecordAsync(
            action: "agenttask.complete",
            summary: $"Task '{task.Title}' completed",
            entityType: "AgentTask",
            entityId: task.Id.ToString(),
            ct: cancellationToken);

        return Ok(task);
    }

    [HasPermission(Permissions.AgentTasks.Complete)]
    [HttpPost("{id:guid}/reopen")]
    public async Task<ActionResult<AgentTaskDto>> Reopen(Guid id, CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        if (actorUserId is null)
        {
            return Unauthorized();
        }

        var task = await tasksService.CompleteAsync(actorUserId.Value, id, completed: false, cancellationToken);
        if (task is null)
        {
            return NotFound();
        }

        await auditLogService.RecordAsync(
            action: "agenttask.reopen",
            summary: $"Task '{task.Title}' reopened",
            entityType: "AgentTask",
            entityId: task.Id.ToString(),
            ct: cancellationToken);

        return Ok(task);
    }

    [HasPermission(Permissions.AgentTasks.Delete)]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var actorUserId = User.GetUserId();
        if (actorUserId is null)
        {
            return Unauthorized();
        }

        var deleted = await tasksService.DeleteAsync(actorUserId.Value, id, cancellationToken);
        if (!deleted)
        {
            return NotFound();
        }

        await auditLogService.RecordAsync(
            action: "agenttask.delete",
            summary: "Task deleted",
            entityType: "AgentTask",
            entityId: id.ToString(),
            ct: cancellationToken);

        return NoContent();
    }

    private BadRequestObjectResult Invalid(string error) => BadRequest(new { error });
}
