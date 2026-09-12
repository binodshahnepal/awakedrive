using Dms.Shared.Contracts.Telemetry;

namespace Dms.Shared.Contracts.Alerts;

/// <summary>
/// Payload pushed over the SignalR DrowsinessHub (/hubs/drowsiness) to Fleet
/// Manager clients (Web Portal, Desktop Console) whenever an incident is ingested.
/// </summary>
public record DrowsinessAlert(
    string IncidentId,
    string DeviceId,
    string DriverId,
    string DriverName,
    IncidentType Type,
    DateTimeOffset TimestampUtc,
    GeoPoint? Location);
