namespace Dms.Api.Data.Entities;

/// <summary>
/// Persistence-side mirror of Dms.Shared.Contracts.Devices.DeviceConfig,
/// kept as its own owned type so the EF Core model doesn't take a hard
/// dependency on the wire-contract project. Controllers map between the two.
/// </summary>
public class DeviceThresholds
{
    public double EarThreshold { get; set; } = 0.20;
    public double EarDurationSeconds { get; set; } = 0.6;
    public double PerclosThreshold { get; set; } = 0.15;
    public double MarThreshold { get; set; } = 0.60;
    public double MarDurationSeconds { get; set; } = 2.5;
    public double YawThresholdDegrees { get; set; } = 25.0;
    public double YawDurationSeconds { get; set; } = 2.0;
}
