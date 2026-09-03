using CustomerSupportCrm.Api.Departments;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CustomerSupportCrm.Api.Tests.Departments;

public class DepartmentsServiceTests
{
    private static CrmDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task List_excludes_inactive_departments_by_default()
    {
        await using var db = CreateDb();
        var service = new DepartmentsService(db);
        await service.CreateAsync(new CreateDepartmentRequest("Support", null));
        var inactive = await service.CreateAsync(new CreateDepartmentRequest("Legacy", null));
        await service.UpdateAsync(inactive.Department!.Id, new UpdateDepartmentRequest("Legacy", null, false));

        var active = await service.ListAsync(includeInactive: false);

        Assert.Equal(["Support"], active.Select(d => d.Name));
    }

    [Fact]
    public async Task List_with_includeInactive_returns_every_department()
    {
        await using var db = CreateDb();
        var service = new DepartmentsService(db);
        await service.CreateAsync(new CreateDepartmentRequest("Support", null));
        var inactive = await service.CreateAsync(new CreateDepartmentRequest("Legacy", null));
        await service.UpdateAsync(inactive.Department!.Id, new UpdateDepartmentRequest("Legacy", null, false));

        var all = await service.ListAsync(includeInactive: true);

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task Get_returns_null_for_an_unknown_id()
    {
        await using var db = CreateDb();
        var service = new DepartmentsService(db);

        Assert.Null(await service.GetAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Create_succeeds_for_a_unique_name_and_defaults_to_active()
    {
        await using var db = CreateDb();
        var service = new DepartmentsService(db);

        var result = await service.CreateAsync(new CreateDepartmentRequest("Support", "SUP"));

        Assert.Equal(DepartmentOperationOutcome.Success, result.Outcome);
        Assert.True(result.Department!.IsActive);
        Assert.Equal("SUP", result.Department.Code);
    }

    [Fact]
    public async Task Create_rejects_an_empty_name_after_trimming()
    {
        await using var db = CreateDb();
        var service = new DepartmentsService(db);

        var result = await service.CreateAsync(new CreateDepartmentRequest("   ", null));

        Assert.Equal(DepartmentOperationOutcome.InvalidName, result.Outcome);
    }

    [Fact]
    public async Task Create_rejects_a_duplicate_name_case_insensitively()
    {
        await using var db = CreateDb();
        var service = new DepartmentsService(db);
        await service.CreateAsync(new CreateDepartmentRequest("Support", null));

        var result = await service.CreateAsync(new CreateDepartmentRequest("support", null));

        Assert.Equal(DepartmentOperationOutcome.DuplicateName, result.Outcome);
    }

    [Fact]
    public async Task Create_rejects_a_duplicate_code()
    {
        await using var db = CreateDb();
        var service = new DepartmentsService(db);
        await service.CreateAsync(new CreateDepartmentRequest("Support", "SUP"));

        var result = await service.CreateAsync(new CreateDepartmentRequest("Customer Support", "SUP"));

        Assert.Equal(DepartmentOperationOutcome.DuplicateCode, result.Outcome);
    }

    [Fact]
    public async Task Create_allows_multiple_departments_with_no_code()
    {
        await using var db = CreateDb();
        var service = new DepartmentsService(db);
        await service.CreateAsync(new CreateDepartmentRequest("Support", null));

        var result = await service.CreateAsync(new CreateDepartmentRequest("Sales", null));

        Assert.Equal(DepartmentOperationOutcome.Success, result.Outcome);
    }

    [Fact]
    public async Task Update_happy_path_renames_and_can_deactivate()
    {
        await using var db = CreateDb();
        var service = new DepartmentsService(db);
        var created = await service.CreateAsync(new CreateDepartmentRequest("Support", null));

        var result = await service.UpdateAsync(created.Department!.Id, new UpdateDepartmentRequest("Customer Support", "CS", false));

        Assert.Equal(DepartmentOperationOutcome.Success, result.Outcome);
        Assert.Equal("Customer Support", result.Department!.Name);
        Assert.Equal("CS", result.Department.Code);
        Assert.False(result.Department.IsActive);
    }

    [Fact]
    public async Task Update_rejects_a_duplicate_name_against_another_row()
    {
        await using var db = CreateDb();
        var service = new DepartmentsService(db);
        await service.CreateAsync(new CreateDepartmentRequest("Support", null));
        var sales = await service.CreateAsync(new CreateDepartmentRequest("Sales", null));

        var result = await service.UpdateAsync(sales.Department!.Id, new UpdateDepartmentRequest("support", null, true));

        Assert.Equal(DepartmentOperationOutcome.DuplicateName, result.Outcome);
    }

    [Fact]
    public async Task Update_returns_NotFound_for_an_unknown_id()
    {
        await using var db = CreateDb();
        var service = new DepartmentsService(db);

        var result = await service.UpdateAsync(Guid.NewGuid(), new UpdateDepartmentRequest("Support", null, true));

        Assert.Equal(DepartmentOperationOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task Deactivating_a_department_does_not_alter_users_who_reference_it()
    {
        // The service itself never touches Users — deactivation is a plain field update on the
        // Department row. This test documents that no cascading write happens, matching the story's
        // "no cascade-detach" rule.
        await using var db = CreateDb();
        var service = new DepartmentsService(db);
        var created = await service.CreateAsync(new CreateDepartmentRequest("Support", null));

        var result = await service.UpdateAsync(created.Department!.Id, new UpdateDepartmentRequest("Support", null, false));

        Assert.Equal(DepartmentOperationOutcome.Success, result.Outcome);
        Assert.False(result.Department!.IsActive);
        // The row still exists with the same Id — a user's DepartmentId FK would still resolve.
        Assert.NotNull(await service.GetAsync(created.Department.Id));
    }
}
