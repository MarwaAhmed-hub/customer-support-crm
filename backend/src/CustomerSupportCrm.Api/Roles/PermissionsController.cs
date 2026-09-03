using CustomerSupportCrm.Api.Authorization;
using CustomerSupportCrm.Domain.Roles;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CustomerSupportCrm.Api.Roles;

public sealed record PermissionCategoryDto(string Category, IReadOnlyList<PermissionDefinition> Permissions);

[ApiController]
[Route("api/permissions")]
[Authorize]
public class PermissionsController(IPermissionCatalog catalog) : ControllerBase
{
    [HasPermission(Permissions.PermissionsMgmt.View)]
    [HttpGet]
    public ActionResult<IReadOnlyList<PermissionCategoryDto>> List()
    {
        var grouped = catalog.All
            .GroupBy(p => p.Category)
            .OrderBy(g => g.Key)
            .Select(g => new PermissionCategoryDto(g.Key, g.OrderBy(p => p.Code).ToList()))
            .ToList();

        return Ok(grouped);
    }
}
