using System.IdentityModel.Tokens.Jwt;
using CustomerSupportCrm.Api.Auth;
using CustomerSupportCrm.Domain.Users;
using Microsoft.Extensions.Options;
using Xunit;

namespace CustomerSupportCrm.Api.Tests.Auth;

public class JwtTokenServiceTests
{
    private static JwtTokenService CreateService() => new(Options.Create(new JwtOptions
    {
        Issuer = "test-issuer",
        Audience = "test-audience",
        SigningKey = "0123456789abcdef0123456789abcdef",
        AccessTokenMinutes = 60,
    }));

    private static User CreateUser(bool isAdmin = false) => new()
    {
        Email = "person@local.test",
        DisplayName = "A Person",
        PasswordHash = "irrelevant",
        IsAdmin = isAdmin,
    };

    [Fact]
    public void Issues_one_permission_claim_per_effective_permission()
    {
        var token = CreateService().IssueAccessToken(CreateUser(), ["users.view", "roles.view"]);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token.Token);
        var permissionClaims = jwt.Claims.Where(c => c.Type == "permission").Select(c => c.Value);

        Assert.Equal(["users.view", "roles.view"], permissionClaims);
    }

    [Fact]
    public void Emits_no_permission_claim_when_the_caller_has_no_permissions()
    {
        var token = CreateService().IssueAccessToken(CreateUser(), []);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token.Token);

        Assert.DoesNotContain(jwt.Claims, c => c.Type == "permission");
    }

    [Fact]
    public void Still_emits_the_role_Admin_claim_for_IsAdmin_users_for_backward_compatibility()
    {
        var token = CreateService().IssueAccessToken(CreateUser(isAdmin: true), ["users.view"]);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token.Token);

        Assert.Contains(jwt.Claims, c => c.Type == "role" && c.Value == "Admin");
    }

    [Fact]
    public void Emits_no_role_claim_for_a_non_admin_user()
    {
        var token = CreateService().IssueAccessToken(CreateUser(isAdmin: false), ["users.view"]);

        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token.Token);

        Assert.DoesNotContain(jwt.Claims, c => c.Type == "role");
    }
}
