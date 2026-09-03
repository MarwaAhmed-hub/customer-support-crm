using CustomerSupportCrm.Api.Roles;
using CustomerSupportCrm.Domain.Roles;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CustomerSupportCrm.Api.Tests.Roles;

public class RolesServiceTests
{
    private static CrmDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static IPermissionCatalog CreateCatalog() => new PermissionCatalog(Permissions.All);

    private static async Task<Role> SeedAdministratorAsync(CrmDbContext db)
    {
        var role = new Role { Name = "Administrator", NormalizedName = RolesService.AdministratorNormalizedName, IsSystem = true };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        return role;
    }

    private static async Task<Role> SeedSystemRoleAsync(CrmDbContext db, string normalizedName, string name)
    {
        var role = new Role { Name = name, NormalizedName = normalizedName, IsSystem = true };
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        return role;
    }

    [Fact]
    public async Task Create_rejects_a_duplicate_name_case_insensitively()
    {
        await using var db = CreateDb();
        var service = new RolesService(db, CreateCatalog());
        await service.CreateAsync(new CreateRoleRequest("Support", null));

        var result = await service.CreateAsync(new CreateRoleRequest("support", null));

        Assert.Equal(RoleOperationOutcome.DuplicateName, result.Outcome);
    }

    [Fact]
    public async Task Create_succeeds_for_a_unique_name_with_zero_permissions()
    {
        await using var db = CreateDb();
        var service = new RolesService(db, CreateCatalog());

        var result = await service.CreateAsync(new CreateRoleRequest("Support", "Front-line support"));

        Assert.Equal(RoleOperationOutcome.Success, result.Outcome);
        Assert.False(result.Role!.IsSystem);
        Assert.Empty(result.Role.Permissions);
    }

    [Fact]
    public async Task Update_rejects_renaming_the_Administrator_role()
    {
        await using var db = CreateDb();
        var admin = await SeedAdministratorAsync(db);
        var service = new RolesService(db, CreateCatalog());

        var result = await service.UpdateAsync(admin.Id, new UpdateRoleRequest("Super Admin", null));

        Assert.Equal(RoleOperationOutcome.AdministratorProtected, result.Outcome);
    }

    [Fact]
    public async Task Update_allows_editing_the_Administrator_roles_description_without_renaming_it()
    {
        await using var db = CreateDb();
        var admin = await SeedAdministratorAsync(db);
        var service = new RolesService(db, CreateCatalog());

        var result = await service.UpdateAsync(admin.Id, new UpdateRoleRequest("Administrator", "Full access"));

        Assert.Equal(RoleOperationOutcome.Success, result.Outcome);
        Assert.Equal("Full access", result.Role!.Description);
    }

    [Fact]
    public async Task ReplacePermissions_rejects_unknown_codes()
    {
        await using var db = CreateDb();
        var service = new RolesService(db, CreateCatalog());
        var created = await service.CreateAsync(new CreateRoleRequest("Support", null));

        var result = await service.ReplacePermissionsAsync(created.Role!.Id, ["not.a.real.code"]);

        Assert.Equal(RoleOperationOutcome.UnknownPermissionCodes, result.Outcome);
        Assert.Equal(["not.a.real.code"], result.UnknownCodes);
    }

    [Fact]
    public async Task ReplacePermissions_rejects_editing_the_Administrator_roles_permissions()
    {
        await using var db = CreateDb();
        var admin = await SeedAdministratorAsync(db);
        var service = new RolesService(db, CreateCatalog());

        var result = await service.ReplacePermissionsAsync(admin.Id, [Permissions.Users.View]);

        Assert.Equal(RoleOperationOutcome.AdministratorProtected, result.Outcome);
    }

    [Fact]
    public async Task ReplacePermissions_replaces_the_set_atomically()
    {
        await using var db = CreateDb();
        var service = new RolesService(db, CreateCatalog());
        var created = await service.CreateAsync(new CreateRoleRequest("Support", null));
        await service.ReplacePermissionsAsync(created.Role!.Id, [Permissions.Tickets.View, Permissions.Tickets.Create]);

        var result = await service.ReplacePermissionsAsync(created.Role.Id, [Permissions.Users.View]);

        Assert.Equal(RoleOperationOutcome.Success, result.Outcome);
        Assert.Equal([Permissions.Users.View], result.Role!.Permissions);
    }

    [Fact]
    public async Task ReplacePermissions_rejects_a_code_outside_the_Customer_roles_eligible_set()
    {
        await using var db = CreateDb();
        var customer = await SeedSystemRoleAsync(db, "CUSTOMER", "Customer");
        var service = new RolesService(db, CreateCatalog());

        // users.view is a real catalogue code, so this isn't caught by the unknown-code check — it
        // must be rejected because it's outside Customer's Eligible Permissions Matrix row.
        var result = await service.ReplacePermissionsAsync(customer.Id, [Permissions.CustomerPortal.Access, Permissions.Users.View]);

        Assert.Equal(RoleOperationOutcome.PermissionsNotEligibleForRole, result.Outcome);
        Assert.Equal([Permissions.Users.View], result.UnknownCodes);
    }

    [Fact]
    public async Task ReplacePermissions_accepts_every_code_in_the_Managers_eligible_set()
    {
        await using var db = CreateDb();
        var manager = await SeedSystemRoleAsync(db, "MANAGER", "Manager");
        var service = new RolesService(db, CreateCatalog());
        var eligible = Permissions.EligibleBySystemRole["MANAGER"].ToList();

        var result = await service.ReplacePermissionsAsync(manager.Id, eligible);

        Assert.Equal(RoleOperationOutcome.Success, result.Outcome);
        Assert.Equal(eligible.OrderBy(c => c), result.Role!.Permissions);
    }

    [Fact]
    public async Task ReplacePermissions_on_a_custom_role_is_not_restricted_by_the_eligibility_matrix()
    {
        await using var db = CreateDb();
        var service = new RolesService(db, CreateCatalog());
        var created = await service.CreateAsync(new CreateRoleRequest("Support", null));

        // roles.view is outside every system role's eligible set, but this role isn't a system role.
        var result = await service.ReplacePermissionsAsync(created.Role!.Id, [Permissions.Roles.View]);

        Assert.Equal(RoleOperationOutcome.Success, result.Outcome);
        Assert.Equal([Permissions.Roles.View], result.Role!.Permissions);
    }

    [Fact]
    public async Task GetEligiblePermissions_returns_only_the_Customer_roles_matrix_row()
    {
        await using var db = CreateDb();
        var customer = await SeedSystemRoleAsync(db, "CUSTOMER", "Customer");
        var service = new RolesService(db, CreateCatalog());

        var categories = await service.GetEligiblePermissionsAsync(customer.Id);

        var codes = categories!.SelectMany(c => c.Permissions).Select(p => p.Code).ToList();
        Assert.Equal(Permissions.EligibleBySystemRole["CUSTOMER"].OrderBy(c => c), codes.OrderBy(c => c));
        Assert.DoesNotContain(Permissions.Users.View, codes);
        Assert.DoesNotContain(Permissions.Roles.View, codes);
    }

    [Fact]
    public async Task GetEligiblePermissions_returns_the_full_catalogue_for_Administrator()
    {
        await using var db = CreateDb();
        var admin = await SeedAdministratorAsync(db);
        var service = new RolesService(db, CreateCatalog());

        var categories = await service.GetEligiblePermissionsAsync(admin.Id);

        var codes = categories!.SelectMany(c => c.Permissions).Select(p => p.Code).ToList();
        Assert.Equal(Permissions.All.Count, codes.Count);
    }

    [Fact]
    public async Task GetEligiblePermissions_returns_null_for_an_unknown_role()
    {
        await using var db = CreateDb();
        var service = new RolesService(db, CreateCatalog());

        var categories = await service.GetEligiblePermissionsAsync(Guid.NewGuid());

        Assert.Null(categories);
    }
}
