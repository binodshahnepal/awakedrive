using Dms.Shared.Contracts.Devices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Dms.Api.Controllers;

[ApiController]
[Route("api/v1/devices")]
[Authorize]
public class DevicesController : ControllerBase
{
    private readonly ILogger<DevicesController> _logger;

    public DevicesController(ILogger<DevicesController> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Registers dashcams/mobile/desktop hardware UUIDs and syncs threshold
    /// configuration (EAR/PERCLOS/MAR/head-pose) back to the device.
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(DeviceRegistrationResponse), StatusCodes.Status201Created)]
    public ActionResult<DeviceRegistrationResponse> Register([FromBody] DeviceRegistrationRequest request)
    {
        // TODO: upsert device record via EF Core, return persisted DeviceId + effective config.
        _logger.LogInformation("Registering device {Uuid} ({Type}) for driver {DriverId}",
            request.DeviceUuid, request.DeviceType, request.DriverId);
        throw new NotImplementedException("Wire up device persistence.");
    }
}
