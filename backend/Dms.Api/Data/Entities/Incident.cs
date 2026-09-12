using Dms.Shared.Contracts.Telemetry;

namespace Dms.Api.Data.Entities;

public class Incident
{
    public Guid Id { get; set; }
    public IncidentType Type { get; set; }
    public DateTimeOffset TimestampUtc { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }

    public Guid DeviceId { get; set; }
    public Device? Device { get; set; }

    public Guid DriverId { get; set; }
    public User? Driver { get; set; }

    // GPS — nullable: a device may report an incident before it has a GPS fix.
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    // Perceptual metrics — populated depending on IncidentType.
    public double? Ear { get; set; }
    public double? Mar { get; set; }
    public double? Perclos { get; set; }
    public double? Pitch { get; set; }
    public double? Yaw { get; set; }
    public double? Roll { get; set; }
}
