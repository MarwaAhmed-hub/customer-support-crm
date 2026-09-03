using CustomerSupportCrm.Api.Branches;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CustomerSupportCrm.Api.Tests.Branches;

public class BranchesServiceTests
{
    private static CrmDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    [Fact]
    public async Task List_excludes_inactive_branches_by_default()
    {
        await using var db = CreateDb();
        var service = new BranchesService(db);
        await service.CreateAsync(new CreateBranchRequest("Cairo", null));
        var inactive = await service.CreateAsync(new CreateBranchRequest("Closed Branch", null));
        await service.UpdateAsync(inactive.Branch!.Id, new UpdateBranchRequest("Closed Branch", null, false));

        var active = await service.ListAsync(includeInactive: false);

        Assert.Equal(["Cairo"], active.Select(b => b.Name));
    }

    [Fact]
    public async Task List_with_includeInactive_returns_every_branch()
    {
        await using var db = CreateDb();
        var service = new BranchesService(db);
        await service.CreateAsync(new CreateBranchRequest("Cairo", null));
        var inactive = await service.CreateAsync(new CreateBranchRequest("Closed Branch", null));
        await service.UpdateAsync(inactive.Branch!.Id, new UpdateBranchRequest("Closed Branch", null, false));

        var all = await service.ListAsync(includeInactive: true);

        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task Get_returns_null_for_an_unknown_id()
    {
        await using var db = CreateDb();
        var service = new BranchesService(db);

        Assert.Null(await service.GetAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task Create_succeeds_for_a_unique_name_and_defaults_to_active()
    {
        await using var db = CreateDb();
        var service = new BranchesService(db);

        var result = await service.CreateAsync(new CreateBranchRequest("Cairo", "CAI"));

        Assert.Equal(BranchOperationOutcome.Success, result.Outcome);
        Assert.True(result.Branch!.IsActive);
        Assert.Equal("CAI", result.Branch.Code);
    }

    [Fact]
    public async Task Create_rejects_an_empty_name_after_trimming()
    {
        await using var db = CreateDb();
        var service = new BranchesService(db);

        var result = await service.CreateAsync(new CreateBranchRequest("   ", null));

        Assert.Equal(BranchOperationOutcome.InvalidName, result.Outcome);
    }

    [Fact]
    public async Task Create_rejects_a_duplicate_name_case_insensitively()
    {
        await using var db = CreateDb();
        var service = new BranchesService(db);
        await service.CreateAsync(new CreateBranchRequest("Cairo", null));

        var result = await service.CreateAsync(new CreateBranchRequest("cairo", null));

        Assert.Equal(BranchOperationOutcome.DuplicateName, result.Outcome);
    }

    [Fact]
    public async Task Create_rejects_a_duplicate_code()
    {
        await using var db = CreateDb();
        var service = new BranchesService(db);
        await service.CreateAsync(new CreateBranchRequest("Cairo", "CAI"));

        var result = await service.CreateAsync(new CreateBranchRequest("Cairo Downtown", "CAI"));

        Assert.Equal(BranchOperationOutcome.DuplicateCode, result.Outcome);
    }

    [Fact]
    public async Task Create_allows_multiple_branches_with_no_code()
    {
        await using var db = CreateDb();
        var service = new BranchesService(db);
        await service.CreateAsync(new CreateBranchRequest("Cairo", null));

        var result = await service.CreateAsync(new CreateBranchRequest("Dubai", null));

        Assert.Equal(BranchOperationOutcome.Success, result.Outcome);
    }

    [Fact]
    public async Task Update_happy_path_renames_and_can_deactivate()
    {
        await using var db = CreateDb();
        var service = new BranchesService(db);
        var created = await service.CreateAsync(new CreateBranchRequest("Cairo", null));

        var result = await service.UpdateAsync(created.Branch!.Id, new UpdateBranchRequest("Cairo Downtown", "CAID", false));

        Assert.Equal(BranchOperationOutcome.Success, result.Outcome);
        Assert.Equal("Cairo Downtown", result.Branch!.Name);
        Assert.Equal("CAID", result.Branch.Code);
        Assert.False(result.Branch.IsActive);
    }

    [Fact]
    public async Task Update_rejects_a_duplicate_name_against_another_row()
    {
        await using var db = CreateDb();
        var service = new BranchesService(db);
        await service.CreateAsync(new CreateBranchRequest("Cairo", null));
        var dubai = await service.CreateAsync(new CreateBranchRequest("Dubai", null));

        var result = await service.UpdateAsync(dubai.Branch!.Id, new UpdateBranchRequest("cairo", null, true));

        Assert.Equal(BranchOperationOutcome.DuplicateName, result.Outcome);
    }

    [Fact]
    public async Task Update_returns_NotFound_for_an_unknown_id()
    {
        await using var db = CreateDb();
        var service = new BranchesService(db);

        var result = await service.UpdateAsync(Guid.NewGuid(), new UpdateBranchRequest("Cairo", null, true));

        Assert.Equal(BranchOperationOutcome.NotFound, result.Outcome);
    }

    [Fact]
    public async Task Deactivating_a_branch_does_not_alter_users_who_reference_it()
    {
        await using var db = CreateDb();
        var service = new BranchesService(db);
        var created = await service.CreateAsync(new CreateBranchRequest("Cairo", null));

        var result = await service.UpdateAsync(created.Branch!.Id, new UpdateBranchRequest("Cairo", null, false));

        Assert.Equal(BranchOperationOutcome.Success, result.Outcome);
        Assert.False(result.Branch!.IsActive);
        Assert.NotNull(await service.GetAsync(created.Branch.Id));
    }
}
