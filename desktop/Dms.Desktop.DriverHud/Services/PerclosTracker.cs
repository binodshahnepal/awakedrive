using System.Linq;

namespace Dms.Desktop.DriverHud.Services;

/// <summary>C# port of perception-engine's PerclosTracker (metrics.py) so the
/// desktop HUD and the Python prototype compute PERCLOS identically.</summary>
public class PerclosTracker(double windowSeconds, double closedEyeEarThreshold)
{
    private readonly Queue<(double TimestampSeconds, bool IsClosed)> _samples = new();

    public double Update(double timestampSeconds, double ear)
    {
        var isClosed = ear < closedEyeEarThreshold;
        _samples.Enqueue((timestampSeconds, isClosed));

        var cutoff = timestampSeconds - windowSeconds;
        while (_samples.Count > 0 && _samples.Peek().TimestampSeconds < cutoff)
        {
            _samples.Dequeue();
        }

        if (_samples.Count == 0)
        {
            return 0.0;
        }

        var closedCount = _samples.Count(s => s.IsClosed);
        return (double)closedCount / _samples.Count;
    }
}
