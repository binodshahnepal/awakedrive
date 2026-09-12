using Dms.Api.Data;
using Dms.Api.Data.Entities;
using Dms.Api.Services;
using Dms.Shared.Contracts.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dms.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthenticationController : ControllerBase
{
    private readonly ILogger<AuthenticationController> _logger;
    private readonly DmsDbContext _db;
    private readonly IJwtTokenService _jwtTokenService;

    public AuthenticationController(ILogger<AuthenticationController> logger, DmsDbContext db, IJwtTokenService jwtTokenService)
    {
        _logger = logger;
        _db = db;
        _jwtTokenService = jwtTokenService;
    }

    /// <summary>
    /// Authenticates drivers and fleet managers, returning a JWT bearer token.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var user = await _db.Users.SingleOrDefaultAsync(u => u.Email == normalizedEmail);

        if (user is null || !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            // Deliberately the same message/shape for "no such user" and "wrong
            // password" so the endpoint doesn't leak which emails are registered.
            _logger.LogInformation("Failed login attempt for {Email}", normalizedEmail);
            return Unauthorized();
        }

        var response = _jwtTokenService.IssueToken(user);
        return Ok(response);
    }

    /// <summary>
    /// Onboards a new driver, fleet manager, or admin. Admin-only: this is how
    /// accounts get created — there is no public self-signup.
    /// </summary>
    [HttpPost("register")]
    [Authorize(Roles = nameof(UserRole.Admin))]
    [ProducesResponseType(typeof(RegisterUserResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<RegisterUserResponse>> Register([FromBody] RegisterUserRequest request)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        if (await _db.Users.AnyAsync(u => u.Email == normalizedEmail))
        {
            return Conflict($"A user with email '{normalizedEmail}' already exists.");
        }

        Guid? fleetId = null;
        if (!string.IsNullOrWhiteSpace(request.FleetId))
        {
            if (!Guid.TryParse(request.FleetId, out var parsedFleetId))
            {
                return BadRequest($"'{request.FleetId}' is not a valid fleet id.");
            }
            if (!await _db.Fleets.AnyAsync(f => f.Id == parsedFleetId))
            {
                return BadRequest($"No fleet found with id '{request.FleetId}'.");
            }
            fleetId = parsedFleetId;
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            DisplayName = request.DisplayName,
            Role = request.Role,
            FleetId = fleetId,
            CreatedAtUtc = DateTimeOffset.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Registered new {Role} user {UserId} ({Email})", user.Role, user.Id, user.Email);

        return StatusCode(StatusCodes.Status201Created,
            new RegisterUserResponse(user.Id.ToString(), user.Email, user.DisplayName, user.Role));
    }
}
