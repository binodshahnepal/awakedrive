using Dms.Api.Hubs;
using Dms.Shared.Contracts.Alerts;
using Dms.Shared.Contracts.Telemetry;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;

namespace Dms.Api.Controllers;

[ApiController]
[Route("api/v1/telemetry")]
[Authorize]
public class TelemetryController : ControllerBase
{
    private readonly ILogger<TelemetryController> _logger;
    private readonly IHubContext<DrowsinessHub> _hub;

    public TelemetryController(ILogger<TelemetryController> logger, IHubContext<DrowsinessHub> hub)
    {
        _logger = logger;
        _hub = hub;
    }

    /// <summary>
    /// Ingests micro-sleep / distraction incident logs (Timestamp, GPS, EAR, MAR,
    /// HeadPose) from an edge client (mobile app or desktop Driver HUD), persists
    /// it, and broadcasts a DrowsinessAlert to Fleet Manager clients via SignalR.
    /// </summary>
    [HttpPost("incidents")]
    [ProducesResponseType(typeof(IncidentIngestResponse), StatusCodes.Status201Created)]
    public async Task<ActionResult<IncidentIngestResponse>> IngestIncident([FromBody] IncidentReport report)
    {
        // TODO: persist via EF Core, resolve driver display name / fleet group.
        _logger.LogInformation("Incident {Type} from device {DeviceId} at {Timestamp}",
            report.Type, report.DeviceId, report.TimestampUtc);

        var incidentId = Guid.NewGuid().ToString();

        var alert = new DrowsinessAlert(
            incidentId,
            report.DeviceId,
            report.DriverId,
            DriverName: "TODO: resolve from driver record",
            report.Type,
            report.TimestampUtc,
            report.Location);

        await _hub.Clients.All.SendAsync("DrowsinessAlert", alert);

        return StatusCode(StatusCodes.Status201Created, new IncidentIngestResponse(incidentId, AlertBroadcast: true));
    }
}
