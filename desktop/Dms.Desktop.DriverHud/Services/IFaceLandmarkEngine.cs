namespace Dms.Desktop.DriverHud.Services;

public record FaceMetrics(double Ear, double Mar, double Perclos, double PitchDegrees, double YawDegrees, double RollDegrees);

public enum EngineStatus
{
    /// <summary>No .onnx file found at the configured path.</summary>
    ModelNotFound,

    /// <summary>A model file was found and loaded, but this engine doesn't
    /// yet know how to turn its output tensors into landmarks/metrics —
    /// that mapping depends on which model perception-engine (Phase 1)
    /// ultimately exports. See OnnxFaceLandmarkEngine's class comment.</summary>
    ModelLoadedButUnmapped,

    Ready
}

public interface IFaceLandmarkEngine
{
    EngineStatus Status { get; }
    string StatusMessage { get; }

    /// <summary>Attempts to compute fatigue metrics from one camera frame.
    /// Returns false (with metrics null) whenever Status != Ready.</summary>
    bool TryPredict(OpenCvSharp.Mat frame, out FaceMetrics? metrics);
}
