using CustomerSupportCrm.Api.Departments;
using CustomerSupportCrm.Api.Tickets.Categories;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CustomerSupportCrm.Api.Tests.Tickets.Categories;

public class TicketCategoriesServiceTests
{
    private static CrmDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task Create_with_a_valid_department_sets_department_id_and_resolves_the_name()
    {
        await using var db = CreateDb();
        var departments = new DepartmentsService(db);
        var department = await departments.CreateAsync(new CreateDepartmentRequest("Billing", null));
        var service = new TicketCategoriesService(db);

        var result = await service.CreateAsync(new CreateTicketCategoryRequest("Billing Issue", null, department.Department!.Id));

        Assert.Equal(TicketCategoryOperationOutcome.Success, result.Outcome);
        Assert.Equal(department.Department.Id, result.Category!.DepartmentId);
        Assert.Equal("Billing", result.Category.DepartmentName);
    }

    [Fact]
    public async Task Create_with_no_department_leaves_department_fields_null()
    {
        await using var db = CreateDb();
        var service = new TicketCategoriesService(db);

        var result = await service.CreateAsync(new CreateTicketCategoryRequest("General", null));

        Assert.Equal(TicketCategoryOperationOutcome.Success, result.Outcome);
        Assert.Null(result.Category!.DepartmentId);
        Assert.Null(result.Category.DepartmentName);
    }

    [Fact]
    public async Task Create_rejects_an_unknown_department_id()
    {
        await using var db = CreateDb();
        var service = new TicketCategoriesService(db);

        var result = await service.CreateAsync(new CreateTicketCategoryRequest("Complaints", null, Guid.NewGuid()));

        Assert.Equal(TicketCategoryOperationOutcome.InvalidDepartment, result.Outcome);
    }

    [Fact]
    public async Task Create_rejects_an_inactive_department_id()
    {
        await using var db = CreateDb();
        var departments = new DepartmentsService(db);
        var department = await departments.CreateAsync(new CreateDepartmentRequest("Legacy", null));
        await departments.UpdateAsync(department.Department!.Id, new UpdateDepartmentRequest("Legacy", null, false));
        var service = new TicketCategoriesService(db);

        var result = await service.CreateAsync(new CreateTicketCategoryRequest("Complaints", null, department.Department.Id));

        Assert.Equal(TicketCategoryOperationOutcome.InvalidDepartment, result.Outcome);
    }

    [Fact]
    public async Task Update_can_change_the_department_and_clear_it()
    {
        await using var db = CreateDb();
        var departments = new DepartmentsService(db);
        var billing = await departments.CreateAsync(new CreateDepartmentRequest("Billing", null));
        var complaints = await departments.CreateAsync(new CreateDepartmentRequest("Complaints", null));
        var service = new TicketCategoriesService(db);
        var created = await service.CreateAsync(new CreateTicketCategoryRequest("Disputes", null, billing.Department!.Id));

        var movedResult = await service.UpdateAsync(created.Category!.Id, new UpdateTicketCategoryRequest("Disputes", null, true, complaints.Department!.Id));
        Assert.Equal(TicketCategoryOperationOutcome.Success, movedResult.Outcome);
        Assert.Equal(complaints.Department.Id, movedResult.Category!.DepartmentId);
        Assert.Equal("Complaints", movedResult.Category.DepartmentName);

        var clearedResult = await service.UpdateAsync(created.Category.Id, new UpdateTicketCategoryRequest("Disputes", null, true, null));
        Assert.Equal(TicketCategoryOperationOutcome.Success, clearedResult.Outcome);
        Assert.Null(clearedResult.Category!.DepartmentId);
        Assert.Null(clearedResult.Category.DepartmentName);
    }

    [Fact]
    public async Task Update_rejects_an_unknown_department_id()
    {
        await using var db = CreateDb();
        var service = new TicketCategoriesService(db);
        var created = await service.CreateAsync(new CreateTicketCategoryRequest("General", null));

        var result = await service.UpdateAsync(created.Category!.Id, new UpdateTicketCategoryRequest("General", null, true, Guid.NewGuid()));

        Assert.Equal(TicketCategoryOperationOutcome.InvalidDepartment, result.Outcome);
    }

    [Fact]
    public async Task List_and_Get_resolve_department_name_through_the_projection_expression()
    {
        await using var db = CreateDb();
        var departments = new DepartmentsService(db);
        var department = await departments.CreateAsync(new CreateDepartmentRequest("Billing", null));
        var service = new TicketCategoriesService(db);
        var created = await service.CreateAsync(new CreateTicketCategoryRequest("Billing Issue", null, department.Department!.Id));

        var listed = await service.ListAsync(includeInactive: false);
        var fetched = await service.GetAsync(created.Category!.Id);

        Assert.Equal("Billing", Assert.Single(listed).DepartmentName);
        Assert.Equal("Billing", fetched!.DepartmentName);
    }
}
