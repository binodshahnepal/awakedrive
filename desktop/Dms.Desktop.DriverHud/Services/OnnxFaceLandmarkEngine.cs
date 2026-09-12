using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using OpenCvSharp;

namespace Dms.Desktop.DriverHud.Services;

/// <summary>
/// Loads perception-engine's exported ONNX model for on-device inference.
///
/// Deliberately incomplete: perception-engine (Phase 1, see
/// /perception-engine) hasn't exported a model yet, and the exact output
/// tensor layout (raw landmark coordinates? pre-computed EAR/MAR/head-pose
/// values? a classification head?) isn't decided. Faking that mapping here
/// would produce numbers that *look* like real fatigue metrics but aren't —
/// worse than clearly saying "not wired up yet". So:
///
///   - ModelNotFound: no .onnx file at the configured path (the common case
///     right now) — HUD shows a clear banner, camera preview still works.
///   - ModelLoadedButUnmapped: a model file loads successfully (input/output
///     names and shapes are logged for debugging) but TryPredict still
///     returns false, because turning its output into a FaceMetrics needs
///     the actual export contract.
///
/// To finish this once perception-engine exports a model: fill in the
/// preprocessing (face crop/resize/normalize to the model's input shape) and
/// the postprocessing (map output tensor(s) -> FaceMetrics, or -> raw
/// landmarks and then metrics.py's EAR/MAR formulas) in TryPredict below,
/// and flip Status to Ready once that's done.
/// </summary>
public class OnnxFaceLandmarkEngine : IFaceLandmarkEngine, IDisposable
{
    private readonly ILogger<OnnxFaceLandmarkEngine> _logger;
    private InferenceSession? _session;

    public EngineStatus Status { get; private set; } = EngineStatus.ModelNotFound;
    public string StatusMessage { get; private set; } = "No perception model configured.";

    public OnnxFaceLandmarkEngine(ILogger<OnnxFaceLandmarkEngine> logger, string modelPath)
    {
        _logger = logger;
        LoadModel(modelPath);
    }

    private void LoadModel(string modelPath)
    {
        if (!File.Exists(modelPath))
        {
            Status = EngineStatus.ModelNotFound;
            StatusMessage = $"No ONNX model at '{modelPath}'. Export one from perception-engine " +
                             "(see perception-engine/README.md) and update Perception:ModelPath in appsettings.json.";
            _logger.LogWarning("{Message}", StatusMessage);
            return;
        }

        try
        {
            _session = new InferenceSession(modelPath);

            var inputs = string.Join(", ", _session.InputMetadata.Select(kv => $"{kv.Key}:[{string.Join(",", kv.Value.Dimensions)}]"));
            var outputs = string.Join(", ", _session.OutputMetadata.Select(kv => $"{kv.Key}:[{string.Join(",", kv.Value.Dimensions)}]"));
            _logger.LogInformation("Loaded ONNX model {Path}. Inputs: {Inputs}. Outputs: {Outputs}", modelPath, inputs, outputs);

            Status = EngineStatus.ModelLoadedButUnmapped;
            StatusMessage = "Model loaded, but input/output mapping isn't implemented yet — " +
                             "see OnnxFaceLandmarkEngine.TryPredict. Metrics will read as unavailable until that's wired up.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load ONNX model at {Path}", modelPath);
            Status = EngineStatus.ModelNotFound;
            StatusMessage = $"Failed to load ONNX model: {ex.Message}";
        }
    }

    public bool TryPredict(Mat frame, out FaceMetrics? metrics)
    {
        metrics = null;

        if (Status != EngineStatus.Ready)
        {
            // See class comment: intentionally not implemented until
            // perception-engine's export contract is known.
            return false;
        }

        // TODO (once perception-engine exports a model):
        //   1. Preprocess `frame` to the session's expected input shape/dtype.
        //   2. Run _session!.Run(...) with the right input tensor name(s).
        //   3. Map the output tensor(s) to FaceMetrics (either directly, or
        //      via metrics.py-equivalent EAR/MAR math on raw landmarks).
        return false;
    }

    public void Dispose()
    {
        _session?.Dispose();
        GC.SuppressFinalize(this);
    }
}
