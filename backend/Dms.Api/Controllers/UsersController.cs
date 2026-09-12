using Dms.Api.Data;
using Dms.Shared.Contracts.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dms.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize(Roles = "FleetManager,Admin")]
public class UsersController : ControllerBase
{
    private readonly DmsDbContext _db;

    public UsersController(DmsDbContext db)
    {
        _db = db;
    }

    /// <summary>
    /// Lists users for the fleet-manager dashboards (driver picker on device
    /// registration, driver/staff directory). Optionally filter by role.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(List<UserSummary>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<UserSummary>>> GetUsers([FromQuery] UserRole? role)
    {
        var query = _db.Users.AsQueryable();
        if (role is not null)
        {
            query = query.Where(u => u.Role == role);
        }

        var users = await query
            .OrderBy(u => u.DisplayName)
            .Select(u => new UserSummary(
                u.Id.ToString(),
                u.Email,
                u.DisplayName,
                u.Role,
                u.FleetId != null ? u.FleetId.ToString() : null))
            .ToListAsync();

        return Ok(users);
    }
}
