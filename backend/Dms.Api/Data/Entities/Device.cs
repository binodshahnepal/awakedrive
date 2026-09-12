using Dms.Shared.Contracts.Devices;

namespace Dms.Api.Data.Entities;

public class Device
{
    public Guid Id { get; set; }
    public required string DeviceUuid { get; set; }
    public DeviceType DeviceType { get; set; }
    public string? FirmwareVersion { get; set; }
    public string? ModelName { get; set; }
    public DateTimeOffset RegisteredAtUtc { get; set; }

    public Guid DriverId { get; set; }
    public User? Driver { get; set; }

    public Guid? FleetId { get; set; }
    public Fleet? Fleet { get; set; }

    public DeviceThresholds Thresholds { get; set; } = new();

    public List<Incident> Incidents { get; set; } = [];
}
