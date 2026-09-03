using CustomerSupportCrm.Api.Audit;
using CustomerSupportCrm.Api.Users;
using CustomerSupportCrm.Domain.Branches;
using CustomerSupportCrm.Domain.Departments;
using CustomerSupportCrm.Domain.Users;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CustomerSupportCrm.Api.Tests.Users;

/// <summary>
/// Story 04: department/branch assignment on <see cref="UsersController"/>. There is no separate
/// service layer for plain user CRUD (see the remarks on <see cref="UsersController"/> — it talks to
/// <see cref="CrmDbContext"/> directly), so this constructs the controller directly against an
/// EF InMemory context, the same way <c>Roles/RolesServiceTests.cs</c> constructs a service.
/// </summary>
public class UsersControllerDepartmentBranchTests
{
    private static CrmDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static UsersController CreateController(CrmDbContext db) =>
        new(db, new PasswordHasher<User>(), new UserRolesService(db), new NoOpAuditLogService());

    /// <summary>Story 05 added the audit-log dependency after this test file was written; these tests don't assert on audit entries, so a no-op stand-in is all `UsersController`'s constructor needs.</summary>
    private sealed class NoOpAuditLogService : IAuditLogService
    {
        public Task RecordAsync(string action, string summary, string? entityType = null, string? entityId = null, object? metadata = null, CancellationToken ct = default) =>
            Task.CompletedTask;

        public Task<AuditLogPageDto> QueryAsync(AuditLogQuery query, CancellationToken ct = default) =>
            Task.FromResult(new AuditLogPageDto([], query.Page, query.PageSize, 0));
    }

    private static async Task<Department> AddDepartmentAsync(CrmDbContext db, string name, bool isActive = true)
    {
        var department = new Department { Name = name, NormalizedName = name.ToUpperInvariant(), IsActive = isActive };
        db.Departments.Add(department);
        await db.SaveChangesAsync();
        return department;
    }

    private static async Task<Branch> AddBranchAsync(CrmDbContext db, string name, bool isActive = true)
    {
        var branch = new Branch { Name = name, NormalizedName = name.ToUpperInvariant(), IsActive = isActive };
        db.Branches.Add(branch);
        await db.SaveChangesAsync();
        return branch;
    }

    private static CreateUserRequest CreateRequest(Guid? departmentId = null, Guid? branchId = null) =>
        new("new@local.test", "New User", "Password!23", departmentId, branchId);

    private static string GetErrorCode(object? errorPayload) =>
        (string)errorPayload!.GetType().GetProperty("error")!.GetValue(errorPayload)!;

    [Fact]
    public async Task Create_with_a_valid_active_department_and_branch_succeeds_and_surfaces_their_names()
    {
        await using var db = CreateDb();
        var department = await AddDepartmentAsync(db, "Support");
        var branch = await AddBranchAsync(db, "Cairo");
        var controller = CreateController(db);

        var actionResult = await controller.Create(CreateRequest(department.Id, branch.Id), CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
        var dto = Assert.IsType<UserDetailDto>(created.Value);
        Assert.Equal(department.Id, dto.DepartmentId);
        Assert.Equal("Support", dto.DepartmentName);
        Assert.Equal(branch.Id, dto.BranchId);
        Assert.Equal("Cairo", dto.BranchName);
    }

    [Fact]
    public async Task Create_with_no_department_or_branch_leaves_both_null()
    {
        await using var db = CreateDb();
        var controller = CreateController(db);

        var actionResult = await controller.Create(CreateRequest(), CancellationToken.None);

        var created = Assert.IsType<CreatedAtActionResult>(actionResult.Result);
        var dto = Assert.IsType<UserDetailDto>(created.Value);
        Assert.Null(dto.DepartmentId);
        Assert.Null(dto.DepartmentName);
        Assert.Null(dto.BranchId);
        Assert.Null(dto.BranchName);
    }

    [Fact]
    public async Task Create_rejects_an_unknown_departmentId()
    {
        await using var db = CreateDb();
        var controller = CreateController(db);

        var actionResult = await controller.Create(CreateRequest(departmentId: Guid.NewGuid()), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        Assert.Equal("invalid_department", GetErrorCode(badRequest.Value));
    }

    [Fact]
    public async Task Create_rejects_an_inactive_branchId()
    {
        await using var db = CreateDb();
        var inactiveBranch = await AddBranchAsync(db, "Closed Branch", isActive: false);
        var controller = CreateController(db);

        var actionResult = await controller.Create(CreateRequest(branchId: inactiveBranch.Id), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        Assert.Equal("invalid_branch", GetErrorCode(badRequest.Value));
    }

    [Fact]
    public async Task Update_assigns_a_department_and_branch_to_an_existing_user()
    {
        await using var db = CreateDb();
        var controller = CreateController(db);
        var createResult = await controller.Create(CreateRequest(), CancellationToken.None);
        var userId = Assert.IsType<UserDetailDto>(Assert.IsType<CreatedAtActionResult>(createResult.Result).Value).Id;

        var department = await AddDepartmentAsync(db, "Sales");
        var branch = await AddBranchAsync(db, "Dubai");

        var actionResult = await controller.Update(
            userId, new UpdateUserRequest("new@local.test", "New User", department.Id, branch.Id), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        var dto = Assert.IsType<UserDetailDto>(ok.Value);
        Assert.Equal("Sales", dto.DepartmentName);
        Assert.Equal("Dubai", dto.BranchName);
    }

    [Fact]
    public async Task Update_rejects_reassigning_to_an_inactive_department()
    {
        await using var db = CreateDb();
        var controller = CreateController(db);
        var createResult = await controller.Create(CreateRequest(), CancellationToken.None);
        var userId = Assert.IsType<UserDetailDto>(Assert.IsType<CreatedAtActionResult>(createResult.Result).Value).Id;

        var inactiveDepartment = await AddDepartmentAsync(db, "Retired Dept", isActive: false);

        var actionResult = await controller.Update(
            userId, new UpdateUserRequest("new@local.test", "New User", inactiveDepartment.Id, null), CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(actionResult.Result);
        Assert.Equal("invalid_department", GetErrorCode(badRequest.Value));
    }

    [Fact]
    public async Task Update_does_not_reject_an_unrelated_edit_when_the_users_existing_department_has_since_gone_inactive()
    {
        // Deactivating a department never touches the users already assigned to it (no cascading
        // write). Editing one of those users for something unrelated (their display name) must not
        // be rejected as a side effect of that department having gone inactive in the meantime.
        await using var db = CreateDb();
        var department = await AddDepartmentAsync(db, "Support");
        var controller = CreateController(db);
        var createResult = await controller.Create(CreateRequest(department.Id), CancellationToken.None);
        var userId = Assert.IsType<UserDetailDto>(Assert.IsType<CreatedAtActionResult>(createResult.Result).Value).Id;

        department.IsActive = false;
        await db.SaveChangesAsync();

        var actionResult = await controller.Update(
            userId, new UpdateUserRequest("new@local.test", "Renamed", department.Id, null), CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        var dto = Assert.IsType<UserDetailDto>(ok.Value);
        Assert.Equal("Renamed", dto.DisplayName);
        Assert.Equal(department.Id, dto.DepartmentId);
    }

    [Fact]
    public async Task List_filters_by_departmentId_and_surfaces_the_denormalised_name()
    {
        await using var db = CreateDb();
        var department = await AddDepartmentAsync(db, "Support");
        var otherDepartment = await AddDepartmentAsync(db, "Sales");
        var controller = CreateController(db);
        await controller.Create(CreateRequest(department.Id), CancellationToken.None);
        await controller.Create(new CreateUserRequest("other@local.test", "Other User", "Password!23", otherDepartment.Id, null), CancellationToken.None);

        var actionResult = await controller.List(departmentId: department.Id, cancellationToken: CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(actionResult.Result);
        var page = Assert.IsType<PagedResult<UserListItemDto>>(ok.Value);
        Assert.Equal(1, page.Total);
        Assert.Equal("Support", page.Items[0].DepartmentName);
    }
}
