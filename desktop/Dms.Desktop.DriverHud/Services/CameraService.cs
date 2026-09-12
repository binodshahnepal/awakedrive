using System.Windows.Media.Imaging;
using Microsoft.Extensions.Logging;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace Dms.Desktop.DriverHud.Services;

/// <summary>
/// Wraps an OpenCvSharp VideoCapture in a background capture loop. Raises
/// both a WPF-ready frozen BitmapSource (for the preview <Image>) and the
/// raw Mat (for IFaceLandmarkEngine, which shouldn't need a WPF dependency).
/// </summary>
public class CameraService(ILogger<CameraService> logger) : IDisposable
{
    private CancellationTokenSource? _cts;
    private Task? _captureLoop;
    private VideoCapture? _capture;

    public event Action<BitmapSource>? FrameCaptured;
    public event Action<Mat>? RawFrameCaptured;
    public event Action<string>? CameraError;

    public bool IsRunning => _captureLoop is { IsCompleted: false };

    public void Start(int cameraIndex = 0, int targetFps = 15)
    {
        if (IsRunning)
        {
            return;
        }

        _cts = new CancellationTokenSource();
        var token = _cts.Token;
        var frameIntervalMs = 1000 / Math.Max(1, targetFps);

        _captureLoop = Task.Run(() => CaptureLoop(cameraIndex, frameIntervalMs, token), token);
    }

    public void Stop()
    {
        _cts?.Cancel();
        try
        {
            _captureLoop?.Wait(TimeSpan.FromSeconds(2));
        }
        catch (AggregateException)
        {
            // Expected: the loop observes OperationCanceledException on Stop().
        }
        _capture?.Release();
        _capture?.Dispose();
        _capture = null;
    }

    private void CaptureLoop(int cameraIndex, int frameIntervalMs, CancellationToken token)
    {
        try
        {
            _capture = new VideoCapture(cameraIndex);
            if (!_capture.IsOpened())
            {
                CameraError?.Invoke($"Could not open camera index {cameraIndex}. Is one connected and not in use by another app?");
                return;
            }

            using var frame = new Mat();
            while (!token.IsCancellationRequested)
            {
                if (!_capture.Read(frame) || frame.Empty())
                {
                    continue;
                }

                RawFrameCaptured?.Invoke(frame);

                var bitmap = frame.ToBitmapSource();
                bitmap.Freeze(); // required to hand a BitmapSource to another thread (the UI thread)
                FrameCaptured?.Invoke(bitmap);

                Thread.Sleep(frameIntervalMs);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Camera capture loop failed");
            CameraError?.Invoke($"Camera error: {ex.Message}");
        }
    }

    public void Dispose()
    {
        Stop();
        _cts?.Dispose();
        GC.SuppressFinalize(this);
    }
}
