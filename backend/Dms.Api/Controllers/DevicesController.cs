using Dms.Api.Data;
using Dms.Api.Data.Entities;
using Dms.Shared.Contracts.Devices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Dms.Api.Controllers;

[ApiController]
[Route("api/v1/devices")]
[Authorize]
public class DevicesController : ControllerBase
{
    private readonly ILogger<DevicesController> _logger;
    private readonly DmsDbContext _db;

    public DevicesController(ILogger<DevicesController> logger, DmsDbContext db)
    {
        _logger = logger;
        _db = db;
    }

    /// <summary>
    /// All registered devices, for the fleet-manager dashboards.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "FleetManager,Admin")]
    [ProducesResponseType(typeof(List<DeviceSummary>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<DeviceSummary>>> GetDevices()
    {
        var devices = await _db.Devices
            .Include(d => d.Driver)
            .OrderByDescending(d => d.RegisteredAtUtc)
            .Select(d => new DeviceSummary(
                d.Id.ToString(),
                d.DeviceUuid,
                d.DeviceType,
                d.DriverId.ToString(),
                d.Driver!.DisplayName,
                d.FirmwareVersion,
                d.ModelName,
                d.RegisteredAtUtc))
            .ToListAsync();

        return Ok(devices);
    }

    /// <summary>
    /// Registers dashcams/mobile/desktop hardware UUIDs and syncs threshold
    /// configuration (EAR/PERCLOS/MAR/head-pose) back to the device. Idempotent
    /// on DeviceUuid — calling this again (e.g. after a factory reset) updates
    /// the existing record instead of creating a duplicate.
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(typeof(DeviceRegistrationResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DeviceRegistrationResponse>> Register([FromBody] DeviceRegistrationRequest request)
    {
        if (!Guid.TryParse(request.DriverId, out var driverId))
        {
            return BadRequest($"'{request.DriverId}' is not a valid driver id.");
        }

        var driverExists = await _db.Users.AnyAsync(u => u.Id == driverId);
        if (!driverExists)
        {
            return BadRequest($"No driver found with id '{request.DriverId}'.");
        }

        var device = await _db.Devices.SingleOrDefaultAsync(d => d.DeviceUuid == request.DeviceUuid);

        if (device is null)
        {
            device = new Device
            {
                Id = Guid.NewGuid(),
                DeviceUuid = request.DeviceUuid,
                RegisteredAtUtc = DateTimeOffset.UtcNow,
                Thresholds = new DeviceThresholds()
            };
            _db.Devices.Add(device);
            _logger.LogInformation("Registering new device {Uuid} ({Type}) for driver {DriverId}",
                request.DeviceUuid, request.DeviceType, driverId);
        }
        else
        {
            _logger.LogInformation("Re-registering existing device {Uuid} ({Type}) for driver {DriverId}",
                request.DeviceUuid, request.DeviceType, driverId);
        }

        device.DeviceType = request.DeviceType;
        device.DriverId = driverId;
        device.FirmwareVersion = request.FirmwareVersion;
        device.ModelName = request.ModelName;

        await _db.SaveChangesAsync();

        var config = new DeviceConfig(
            device.Thresholds.EarThreshold,
            device.Thresholds.EarDurationSeconds,
            device.Thresholds.PerclosThreshold,
            device.Thresholds.MarThreshold,
            device.Thresholds.MarDurationSeconds,
            device.Thresholds.YawThresholdDegrees,
            device.Thresholds.YawDurationSeconds);

        return StatusCode(StatusCodes.Status201Created,
            new DeviceRegistrationResponse(device.Id.ToString(), device.RegisteredAtUtc, config));
    }
}
