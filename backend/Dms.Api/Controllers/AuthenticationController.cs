using Dms.Shared.Contracts.Auth;
using Microsoft.AspNetCore.Mvc;

namespace Dms.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthenticationController : ControllerBase
{
    private readonly ILogger<AuthenticationController> _logger;

    public AuthenticationController(ILogger<AuthenticationController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Authenticates drivers and fleet managers, returning a JWT bearer token.
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public ActionResult<LoginResponse> Login([FromBody] LoginRequest request)
    {
        // TODO: validate credentials against the user store and issue a real JWT
        // (Microsoft.IdentityModel.Tokens / System.IdentityModel.Tokens.Jwt).
        _logger.LogInformation("Login attempt for {Email}", request.Email);
        throw new NotImplementedException("Wire up credential validation and JWT issuance.");
    }
}
