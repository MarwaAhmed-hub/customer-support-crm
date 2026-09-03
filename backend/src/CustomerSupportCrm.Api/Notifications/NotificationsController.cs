using CustomerSupportCrm.Api.Auth;
using CustomerSupportCrm.Api.Authorization;
using CustomerSupportCrm.Domain.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupportCrm.Api.Notifications;

/// <summary>A signed-in staff member's own notification inbox (Story 25). Always scoped server-side to the caller's own id — there is no "view anyone's notifications" endpoint, not even for Administrator.</summary>
[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController(INotificationService notificationService) : ControllerBase
{
    [HasPermission(Permissions.Notifications.ViewOwn)]
    [HttpGet("me")]
    public async Task<ActionResult<NotificationListResponse>> Me(
        [FromQuery] bool unreadOnly, [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        return Ok(await notificationService.ListForUserAsync(userId.Value, unreadOnly, page, pageSize, cancellationToken));
    }

    [HasPermission(Permissions.Notifications.MarkRead)]
    [HttpPost("{id:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid id, CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        // Same "not found" for both "doesn't exist" and "belongs to someone else" — never reveals
        // which, matching the rest of this codebase's own-resource-only endpoints.
        var updated = await notificationService.MarkReadAsync(id, userId.Value, cancellationToken);
        return updated ? NoContent() : NotFound();
    }

    [HasPermission(Permissions.Notifications.MarkRead)]
    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        var userId = User.GetUserId();
        if (userId is null)
        {
            return Unauthorized();
        }

        await notificationService.MarkAllReadAsync(userId.Value, cancellationToken);
        return NoContent();
    }
}
