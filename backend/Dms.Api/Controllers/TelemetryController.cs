using Dms.Api.Data;
using Dms.Api.Data.Entities;
using Dms.Api.Hubs;
using Dms.Shared.Contracts.Alerts;
using Dms.Shared.Contracts.Telemetry;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace Dms.Api.Controllers;

[ApiController]
[Route("api/v1/telemetry")]
[Authorize]
public class TelemetryController : ControllerBase
{
    private readonly ILogger<TelemetryController> _logger;
    private readonly DmsDbContext _db;
    private readonly IHubContext<DrowsinessHub> _hub;

    public TelemetryController(ILogger<TelemetryController> logger, DmsDbContext db, IHubContext<DrowsinessHub> hub)
    {
        _logger = logger;
        _db = db;
        _hub = hub;
    }

    /// <summary>
    /// Recent incidents for the fleet-manager dashboards, newest first.
    /// </summary>
    [HttpGet("incidents")]
    [Authorize(Roles = "FleetManager,Admin")]
    [ProducesResponseType(typeof(List<IncidentSummary>), StatusCodes.Status200OK)]
    public async Task<ActionResult<List<IncidentSummary>>> GetRecentIncidents([FromQuery] int take = 50)
    {
        take = Math.Clamp(take, 1, 500);

        var incidents = await _db.Incidents
            .Include(i => i.Driver)
            .OrderByDescending(i => i.TimestampUtc)
            .Take(take)
            .Select(i => new IncidentSummary(
                i.Id.ToString(),
                i.DeviceId.ToString(),
                i.DriverId.ToString(),
                i.Driver!.DisplayName,
                i.Type,
                i.TimestampUtc,
                i.Latitude != null && i.Longitude != null ? new GeoPoint(i.Latitude.Value, i.Longitude.Value) : null,
                i.Ear,
                i.Mar,
                i.Perclos,
                i.Pitch != null && i.Yaw != null && i.Roll != null
                    ? new HeadPose(i.Pitch.Value, i.Yaw.Value, i.Roll.Value)
                    : null))
            .ToListAsync();

        return Ok(incidents);
    }

    /// <summary>
    /// Ingests micro-sleep / distraction incident logs (Timestamp, GPS, EAR, MAR,
    /// HeadPose) from an edge client (mobile app or desktop Driver HUD), persists
    /// it, and broadcasts a DrowsinessAlert to Fleet Manager clients via SignalR.
    /// </summary>
    [HttpPost("incidents")]
    [ProducesResponseType(typeof(IncidentIngestResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IncidentIngestResponse>> IngestIncident([FromBody] IncidentReport report)
    {
        if (!Guid.TryParse(report.DeviceId, out var deviceId))
        {
            return BadRequest($"'{report.DeviceId}' is not a valid device id.");
        }
        if (!Guid.TryParse(report.DriverId, out var driverId))
        {
            return BadRequest($"'{report.DriverId}' is not a valid driver id.");
        }

        var driver = await _db.Users.SingleOrDefaultAsync(u => u.Id == driverId);
        if (driver is null)
        {
            return BadRequest($"No driver found with id '{report.DriverId}'.");
        }

        var incident = new Incident
        {
            Id = Guid.NewGuid(),
            DeviceId = deviceId,
            DriverId = driverId,
            Type = report.Type,
            TimestampUtc = report.TimestampUtc,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            Latitude = report.Location?.Latitude,
            Longitude = report.Location?.Longitude,
            Ear = report.Ear,
            Mar = report.Mar,
            Perclos = report.Perclos,
            Pitch = report.HeadPose?.Pitch,
            Yaw = report.HeadPose?.Yaw,
            Roll = report.HeadPose?.Roll
        };

        _db.Incidents.Add(incident);
        await _db.SaveChangesAsync();

        _logger.LogInformation("Persisted incident {IncidentId} ({Type}) from device {DeviceId}",
            incident.Id, incident.Type, deviceId);

        var alert = new DrowsinessAlert(
            incident.Id.ToString(),
            report.DeviceId,
            report.DriverId,
            driver.DisplayName,
            report.Type,
            report.TimestampUtc,
            report.Location);

        await _hub.Clients.All.SendAsync("DrowsinessAlert", alert);

        return StatusCode(StatusCodes.Status201Created,
            new IncidentIngestResponse(incident.Id.ToString(), AlertBroadcast: true));
    }
}
