using CustomerSupportCrm.Api.Audit;
using CustomerSupportCrm.Api.Authorization;
using CustomerSupportCrm.Domain.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupportCrm.Api.Departments;

/// <summary>List/create/update departments. No delete endpoint — deactivate via <see cref="Update"/> instead. See <see cref="DepartmentsService"/> for the business rules.</summary>
[ApiController]
[Route("api/departments")]
[Authorize]
public class DepartmentsController(IDepartmentsService departmentsService, IAuditLogService auditLogService) : ControllerBase
{
    [HasPermission(Permissions.Departments.View)]
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DepartmentDto>>> List(
        [FromQuery] bool includeInactive, CancellationToken cancellationToken) =>
        Ok(await departmentsService.ListAsync(includeInactive, cancellationToken));

    [HasPermission(Permissions.Departments.View)]
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DepartmentDto>> Get(Guid id, CancellationToken cancellationToken)
    {
        var department = await departmentsService.GetAsync(id, cancellationToken);
        return department is null ? NotFound() : Ok(department);
    }

    [HasPermission(Permissions.Departments.Create)]
    [HttpPost]
    public async Task<ActionResult<DepartmentDto>> Create(CreateDepartmentRequest request, CancellationToken cancellationToken)
    {
        var result = await departmentsService.CreateAsync(request, cancellationToken);
        if (result.Outcome == DepartmentOperationOutcome.Success)
        {
            await auditLogService.RecordAsync(
                action: "create",
                summary: $"Department '{result.Department!.Name}' created",
                entityType: "Department",
                entityId: result.Department.Id.ToString(),
                ct: cancellationToken);
        }
        return result.Outcome switch
        {
            DepartmentOperationOutcome.Success => CreatedAtAction(nameof(Get), new { id = result.Department!.Id }, result.Department),
            DepartmentOperationOutcome.InvalidName => InvalidName(),
            DepartmentOperationOutcome.DuplicateName => DuplicateName(),
            DepartmentOperationOutcome.DuplicateCode => DuplicateCode(),
            _ => Problem(statusCode: 500),
        };
    }

    [HasPermission(Permissions.Departments.Update)]
    [HttpPut("{id:guid}")]
    public async Task<ActionResult<DepartmentDto>> Update(Guid id, UpdateDepartmentRequest request, CancellationToken cancellationToken)
    {
        var result = await departmentsService.UpdateAsync(id, request, cancellationToken);
        if (result.Outcome == DepartmentOperationOutcome.Success)
        {
            await auditLogService.RecordAsync(
                action: "update",
                summary: $"Department '{result.Department!.Name}' updated",
                entityType: "Department",
                entityId: result.Department.Id.ToString(),
                ct: cancellationToken);
        }
        return result.Outcome switch
        {
            DepartmentOperationOutcome.Success => Ok(result.Department),
            DepartmentOperationOutcome.NotFound => NotFound(),
            DepartmentOperationOutcome.InvalidName => InvalidName(),
            DepartmentOperationOutcome.DuplicateName => DuplicateName(),
            DepartmentOperationOutcome.DuplicateCode => DuplicateCode(),
            _ => Problem(statusCode: 500),
        };
    }

    private BadRequestObjectResult InvalidName() => BadRequest(new { error = "invalid_name" });

    private ObjectResult DuplicateName() => Conflict(new { error = "duplicate_department_name" });

    private ObjectResult DuplicateCode() => Conflict(new { error = "duplicate_department_code" });
}
