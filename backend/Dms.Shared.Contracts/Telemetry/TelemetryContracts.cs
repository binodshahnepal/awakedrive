namespace Dms.Shared.Contracts.Telemetry;

public enum IncidentType
{
    MicroSleep,
    Perclos,
    Yawning,
    OffRoadDistraction
}

public record GeoPoint(double Latitude, double Longitude);

public record IncidentReport(
    string DeviceId,
    string DriverId,
    IncidentType Type,
    DateTimeOffset TimestampUtc,
    GeoPoint? Location,
    double? Ear,
    double? Mar,
    double? Perclos,
    HeadPose? HeadPose);

public record HeadPose(double Pitch, double Yaw, double Roll);

public record IncidentIngestResponse(string IncidentId, bool AlertBroadcast);
