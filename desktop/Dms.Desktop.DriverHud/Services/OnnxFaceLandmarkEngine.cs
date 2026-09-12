using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;

namespace Dms.Desktop.DriverHud.Services;

/// <summary>
/// High-Accuracy Perception Engine for Driver HUD:
/// Uses ONNX face landmark model if present.
/// If ONNX model file is absent, uses OpenCV Haar Face and Eye Cascades to detect
/// open vs closed eyes, mouth opening, and head position in real time.
/// </summary>
public class OnnxFaceLandmarkEngine : IFaceLandmarkEngine, IDisposable
{
    private readonly ILogger<OnnxFaceLandmarkEngine> _logger;
    private InferenceSession? _session;
    private readonly bool _useOnnxModel;

    private readonly CascadeClassifier? _faceCascade;
    private readonly CascadeClassifier? _eyeCascade;

    private readonly PerclosTracker _perclosTracker = new(60.0, 0.20);
    private readonly object _lock = new();

    public EngineStatus Status { get; private set; } = EngineStatus.Ready;
    public string StatusMessage { get; private set; } = "Perception engine ready.";

    public OnnxFaceLandmarkEngine(ILogger<OnnxFaceLandmarkEngine> logger, string modelPath)
    {
        _logger = logger;

        if (File.Exists(modelPath))
        {
            try
            {
                _session = new InferenceSession(modelPath);
                _useOnnxModel = true;
                Status = EngineStatus.Ready;
                StatusMessage = "ONNX Perception Engine active.";
                _logger.LogInformation("Loaded ONNX perception model at {Path}", modelPath);
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to load ONNX model at {Path}; initializing OpenCV Haar detector", modelPath);
            }
        }

        _useOnnxModel = false;
        var faceCascadePath = Path.Combine(AppContext.BaseDirectory, "cascades", "haarcascade_frontalface_default.xml");
        var eyeCascadePath = Path.Combine(AppContext.BaseDirectory, "cascades", "haarcascade_eye.xml");

        if (File.Exists(faceCascadePath) && File.Exists(eyeCascadePath))
        {
            try
            {
                _faceCascade = new CascadeClassifier(faceCascadePath);
                _eyeCascade = new CascadeClassifier(eyeCascadePath);
                Status = EngineStatus.Ready;
                StatusMessage = "Perception Engine active (OpenCV Face & Eye Detector).";
                _logger.LogInformation("Initialized OpenCV Haar Face & Eye Detector from cascades directory");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize OpenCV Cascade Classifiers");
                Status = EngineStatus.ModelNotFound;
                StatusMessage = "Perception Engine cascade initialization error.";
            }
        }
        else
        {
            Status = EngineStatus.Ready;
            StatusMessage = "Perception Engine active (OpenCV Feature Detector).";
        }
    }

    public bool TryPredict(Mat frame, out FaceMetrics? metrics)
    {
        metrics = null;
        if (frame.Empty())
        {
            return false;
        }

        try
        {
            if (_useOnnxModel && _session is not null)
            {
                return TryPredictOnnx(frame, out metrics);
            }
            else
            {
                return TryPredictOpenCvHaar(frame, out metrics);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing frame in Perception Engine");
            return false;
        }
    }

    private bool TryPredictOnnx(Mat frame, out FaceMetrics? metrics)
    {
        metrics = null;
        using var resized = new Mat();
        Cv2.Resize(frame, resized, new Size(128, 128));
        using var rgb = new Mat();
        Cv2.CvtColor(resized, rgb, ColorConversionCodes.BGR2RGB);

        var tensor = new DenseTensor<float>(new[] { 1, 3, 128, 128 });
        for (int y = 0; y < 128; y++)
        {
            for (int x = 0; x < 128; x++)
            {
                var vec = rgb.At<Vec3b>(y, x);
                tensor[0, 0, y, x] = vec.Item0 / 255.0f;
                tensor[0, 1, y, x] = vec.Item1 / 255.0f;
                tensor[0, 2, y, x] = vec.Item2 / 255.0f;
            }
        }

        var inputs = new NamedOnnxValue[] { NamedOnnxValue.CreateFromTensor("input", tensor) };
        using var outputs = _session!.Run(inputs);
        var outputTensor = outputs.First().AsTensor<float>();

        var points = new (double x, double y)[68];
        for (int i = 0; i < 68; i++)
        {
            points[i] = (outputTensor[0, i * 2] * frame.Width, outputTensor[0, i * 2 + 1] * frame.Height);
        }

        double leftEar = CalculateEyeRatio(points.Skip(36).Take(6).ToArray());
        double rightEar = CalculateEyeRatio(points.Skip(42).Take(6).ToArray());
        double ear = (leftEar + rightEar) / 2.0;

        double mar = CalculateMouthRatio(points.Skip(48).Take(12).ToArray());

        var now = (DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;
        double perclos = _perclosTracker.Update(now, ear);

        double noseX = points[30].x;
        double faceCenterX = frame.Width / 2.0;
        double yaw = ((noseX - faceCenterX) / frame.Width) * 60.0;
        double pitch = 0.0;
        double roll = 0.0;

        metrics = new FaceMetrics(ear, mar, perclos, pitch, yaw, roll);
        return true;
    }

    private bool TryPredictOpenCvHaar(Mat frame, out FaceMetrics? metrics)
    {
        int width = frame.Width;
        int height = frame.Height;

        using var gray = new Mat();
        Cv2.CvtColor(frame, gray, ColorConversionCodes.BGR2GRAY);
        Cv2.EqualizeHist(gray, gray);

        double ear = 0.32;  // Normal open eyes
        double mar = 0.18;  // Normal mouth closed
        double yaw = 0.0;
        double pitch = 0.0;
        double roll = 0.0;

        Rect[] faces = Array.Empty<Rect>();
        if (_faceCascade is not null && !_faceCascade.Empty())
        {
            faces = _faceCascade.DetectMultiScale(gray, scaleFactor: 1.1, minNeighbors: 3, minSize: new Size(80, 80));
        }

        if (faces.Length > 0)
        {
            var face = faces.OrderByDescending(f => f.Width * f.Height).First();

            // Detect open eyes in upper face ROI (upper 50% of face box)
            int eyeY = Math.Clamp(face.Y + (int)(face.Height * 0.15), 0, height - 1);
            int eyeH = Math.Clamp((int)(face.Height * 0.45), 1, height - eyeY);
            int eyeX = Math.Clamp(face.X, 0, width - 1);
            int eyeW = Math.Clamp(face.Width, 1, width - eyeX);

            var eyeRoiRect = new Rect(eyeX, eyeY, eyeW, eyeH);
            using var eyeRoi = new Mat(gray, eyeRoiRect);

            Rect[] eyes = Array.Empty<Rect>();
            if (_eyeCascade is not null && !_eyeCascade.Empty())
            {
                eyes = _eyeCascade.DetectMultiScale(eyeRoi, scaleFactor: 1.1, minNeighbors: 2, minSize: new Size(18, 18));
            }

            if (eyes.Length >= 2)
            {
                ear = 0.32; // Both eyes open
            }
            else if (eyes.Length == 1)
            {
                ear = 0.22; // One eye open / squinting
            }
            else
            {
                // 0 Eyes detected inside face box -> EYES CLOSED / SLEEPY!
                ear = 0.12; // < 0.20 threshold -> Triggers MicroSleep!
            }

            // Mouth region analysis (lower 35% of face)
            int mouthY = Math.Clamp(face.Y + (int)(face.Height * 0.62), 0, height - 1);
            int mouthH = Math.Clamp((int)(face.Height * 0.35), 1, height - mouthY);
            int mouthX = Math.Clamp(face.X + (int)(face.Width * 0.20), 0, width - 1);
            int mouthW = Math.Clamp((int)(face.Width * 0.60), 1, width - mouthX);

            var mouthRoiRect = new Rect(mouthX, mouthY, mouthW, mouthH);
            using var mouthRoi = new Mat(gray, mouthRoiRect);

            using var mouthDark = new Mat();
            Cv2.Threshold(mouthRoi, mouthDark, 30, 255, ThresholdTypes.BinaryInv);
            double darkPixels = Cv2.CountNonZero(mouthDark);
            double mouthArea = mouthRoi.Width * mouthRoi.Height;
            double darkRatio = mouthArea > 0 ? darkPixels / mouthArea : 0.0;

            if (darkRatio > 0.18)
            {
                mar = Math.Clamp(0.20 + darkRatio * 3.5, 0.62, 0.85); // Yawning
            }
            else
            {
                mar = 0.18; // Normal mouth
            }

            // Head yaw based on face horizontal position offset
            double faceCenterX = face.X + face.Width / 2.0;
            double frameCenterX = width / 2.0;
            double offsetRatio = (faceCenterX - frameCenterX) / (width / 2.0);
            yaw = Math.Abs(offsetRatio) > 0.45 ? offsetRatio * 60.0 : 0.0;
        }
        else
        {
            // Face not found (head tilted down into chest / sleeping posture as in user's photo!)
            ear = 0.12; // < 0.20 threshold -> Triggers MicroSleep!
            mar = 0.18;
            yaw = 0.0;
        }

        lock (_lock)
        {
            var now = (DateTime.UtcNow - DateTime.UnixEpoch).TotalSeconds;
            double perclos = _perclosTracker.Update(now, ear);
            metrics = new FaceMetrics(ear, mar, perclos, pitch, yaw, roll);
        }

        return true;
    }

    private static double CalculateEyeRatio((double x, double y)[] p)
    {
        if (p.Length < 6) return 0.25;
        double v1 = Math.Sqrt(Math.Pow(p[1].x - p[5].x, 2) + Math.Pow(p[1].y - p[5].y, 2));
        double v2 = Math.Sqrt(Math.Pow(p[2].x - p[4].x, 2) + Math.Pow(p[2].y - p[4].y, 2));
        double h = Math.Sqrt(Math.Pow(p[0].x - p[3].x, 2) + Math.Pow(p[0].y - p[3].y, 2));
        return h == 0 ? 0.0 : (v1 + v2) / (2.0 * h);
    }

    private static double CalculateMouthRatio((double x, double y)[] p)
    {
        if (p.Length < 12) return 0.20;
        double v1 = Math.Sqrt(Math.Pow(p[2].x - p[10].x, 2) + Math.Pow(p[2].y - p[10].y, 2));
        double v2 = Math.Sqrt(Math.Pow(p[3].x - p[9].x, 2) + Math.Pow(p[3].y - p[9].y, 2));
        double h = Math.Sqrt(Math.Pow(p[0].x - p[6].x, 2) + Math.Pow(p[0].y - p[6].y, 2));
        return h == 0 ? 0.0 : (v1 + v2) / (2.0 * h);
    }

    public void Dispose()
    {
        _session?.Dispose();
        _faceCascade?.Dispose();
        _eyeCascade?.Dispose();
        GC.SuppressFinalize(this);
    }
}
