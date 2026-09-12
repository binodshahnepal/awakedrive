namespace Dms.Shared.Contracts.Devices;

public enum DeviceType
{
    MobileAndroid,
    MobileIos,
    DesktopDriverHud,
    Dashcam
}

public record DeviceRegistrationRequest(
    string DeviceUuid,
    DeviceType DeviceType,
    string DriverId,
    string? FirmwareVersion,
    string? ModelName);

public record DeviceRegistrationResponse(string DeviceId, DateTimeOffset RegisteredAtUtc, DeviceConfig Config);

public record DeviceConfig(
    double EarThreshold = 0.20,
    double EarDurationSeconds = 1.5,
    double PerclosThreshold = 0.15,
    double MarThreshold = 0.60,
    double MarDurationSeconds = 2.5,
    double YawThresholdDegrees = 25.0,
    double YawDurationSeconds = 2.0);
