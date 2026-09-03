using CustomerSupportCrm.Api.Roles;
using CustomerSupportCrm.Api.Users;
using CustomerSupportCrm.Domain.Roles;
using CustomerSupportCrm.Domain.Users;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace CustomerSupportCrm.Api.Tests.Users;

public class UserRolesServiceTests
{
    private static CrmDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static User NewUser() => new() { Email = $"{Guid.NewGuid():N}@local.test", DisplayName = "A Person", PasswordHash = "x" };

    private static Role NewRole(string name, bool isAdministrator = false) => new()
    {
        Name = name,
        NormalizedName = isAdministrator ? RolesService.AdministratorNormalizedName : name.ToUpperInvariant(),
    };

    [Fact]
    public async Task AssignAsync_is_idempotent()
    {
        await using var db = CreateDb();
        var user = NewUser();
        var role = NewRole("Agent");
        db.Users.Add(user);
        db.Roles.Add(role);
        await db.SaveChangesAsync();
        var service = new UserRolesService(db);

        await service.AssignAsync(user.Id, role.Id);
        var second = await service.AssignAsync(user.Id, role.Id);

        Assert.Equal(UserRoleOperationOutcome.Success, second);
        Assert.Equal(1, await db.UserRoles.CountAsync(ur => ur.UserId == user.Id && ur.RoleId == role.Id));
    }

    [Fact]
    public async Task RemoveAsync_rejects_removing_the_last_Administrator_link_from_the_last_admin()
    {
        await using var db = CreateDb();
        var user = NewUser();
        var administrator = NewRole("Administrator", isAdministrator: true);
        db.Users.Add(user);
        db.Roles.Add(administrator);
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = administrator.Id });
        await db.SaveChangesAsync();
        var service = new UserRolesService(db);

        var outcome = await service.RemoveAsync(user.Id, administrator.Id);

        Assert.Equal(UserRoleOperationOutcome.LastAdministrator, outcome);
        Assert.Equal(1, await db.UserRoles.CountAsync());
    }

    [Fact]
    public async Task RemoveAsync_allows_removing_Administrator_when_another_user_still_has_it()
    {
        await using var db = CreateDb();
        var (userA, userB) = (NewUser(), NewUser());
        var administrator = NewRole("Administrator", isAdministrator: true);
        db.Users.AddRange(userA, userB);
        db.Roles.Add(administrator);
        db.UserRoles.AddRange(
            new UserRole { UserId = userA.Id, RoleId = administrator.Id },
            new UserRole { UserId = userB.Id, RoleId = administrator.Id });
        await db.SaveChangesAsync();
        var service = new UserRolesService(db);

        var outcome = await service.RemoveAsync(userA.Id, administrator.Id);

        Assert.Equal(UserRoleOperationOutcome.Success, outcome);
        Assert.Equal(1, await db.UserRoles.CountAsync());
    }

    [Fact]
    public async Task RemoveAsync_allows_removing_a_non_Administrator_role_freely()
    {
        await using var db = CreateDb();
        var user = NewUser();
        var agent = NewRole("Agent");
        db.Users.Add(user);
        db.Roles.Add(agent);
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = agent.Id });
        await db.SaveChangesAsync();
        var service = new UserRolesService(db);

        var outcome = await service.RemoveAsync(user.Id, agent.Id);

        Assert.Equal(UserRoleOperationOutcome.Success, outcome);
        Assert.Empty(await db.UserRoles.ToListAsync());
    }
}
