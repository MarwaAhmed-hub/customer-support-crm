using CustomerSupportCrm.Api.Audit;
using CustomerSupportCrm.Domain.Users;
using CustomerSupportCrm.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CustomerSupportCrm.Api.Auth;

[ApiController]
[Route("api/auth")]
public class AuthController(
    CrmDbContext db,
    IPasswordHasher<User> passwordHasher,
    JwtTokenService tokenService,
    IUserPermissionsQuery permissionsQuery,
    IAuditLogService auditLogService,
    ILogger<AuthController> logger) : ControllerBase
{
    /// <summary>
    /// A hash of a throwaway password, verified when no user row matches so that an unknown e-mail
    /// costs roughly the same as a wrong password. Without this, response time alone would leak
    /// whether an account exists.
    /// </summary>
    private static readonly string DummyHash =
        new PasswordHasher<User>().HashPassword(new User(), "not-a-real-password");

    [AllowAnonymous]
    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var email = EmailNormalizer.Normalize(request.Email);

        var user = await db.Users.SingleOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null)
        {
            // Burn comparable time, then fail exactly as a wrong password does.
            passwordHasher.VerifyHashedPassword(new User(), DummyHash, request.Password);
            return InvalidCredentials();
        }

        var verification = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, request.Password);

        if (verification == PasswordVerificationResult.Failed || !user.IsActive)
        {
            return InvalidCredentials();
        }

        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            // Lets a later story raise the PBKDF2 iteration count without invalidating passwords.
            user.PasswordHash = passwordHasher.HashPassword(user, request.Password);
            await db.SaveChangesAsync(cancellationToken);
        }

        var permissions = await permissionsQuery.GetForUserAsync(user.Id, cancellationToken);
        var token = tokenService.IssueAccessToken(user, permissions);

        logger.LogInformation(
            "event={Event} outcome={Outcome} userId={UserId} email={Email} remoteIp={RemoteIp}",
            "login", "success", user.Id, user.Email, HttpContext.Connection.RemoteIpAddress?.ToString());

        // Record audit log for successful login
        await auditLogService.RecordAsync(
            action: "login",
            summary: $"User {user.Email} logged in successfully",
            entityType: "User",
            entityId: user.Id.ToString(),
            ct: cancellationToken);

        return Ok(new LoginResponse(
            token.Token,
            token.ExpiresAt,
            new UserDto(user.Id, user.Email, user.DisplayName),
            permissions));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized(new { error = "unauthorized" });
        }

        var user = await db.Users.SingleOrDefaultAsync(u => u.Id == userId.Value, cancellationToken);

        // A cryptographically valid token whose account is gone or deactivated is still rejected,
        // which is what makes deactivation take effect within the token's lifetime.
        if (user is null || !user.IsActive)
        {
            return Unauthorized(new { error = "unauthorized" });
        }

        // Recomputed from the database rather than read off the token's own "permission" claims, so
        // a role/permission change since login is reflected the moment the frontend next calls /me —
        // it does not have to wait for the (up to AccessTokenMinutes-old) token to expire.
        var permissions = await permissionsQuery.GetForUserAsync(user.Id, cancellationToken);

        return Ok(new MeResponse(user.Id, user.Email, user.DisplayName, permissions));
    }

    /// <summary>
    /// One response for unknown e-mail, wrong password, and inactive user — identical status and
    /// body. Distinguishing them would be an account-enumeration oracle.
    /// </summary>
    private IActionResult InvalidCredentials()
    {
        // Neither userId nor the attempted e-mail is logged: nothing is confirmed about whether the
        // account exists. (Note for the audit-logs story, not for this one: brute-force detection
        // normally wants the attempted e-mail on failure too. That is a deliberate extension point.)
        logger.LogWarning(
            "event={Event} outcome={Outcome} remoteIp={RemoteIp}",
            "login", "failure", HttpContext.Connection.RemoteIpAddress?.ToString());

        return Unauthorized(new { error = "invalid_credentials" });
    }
}
