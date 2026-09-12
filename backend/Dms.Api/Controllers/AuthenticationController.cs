using Dms.Api.Data;
using Dms.Api.Services;
using Dms.Shared.Contracts.Auth;
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
}
