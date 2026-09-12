using System.Media;
using System.Windows;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Dms.Client.Api;
using Dms.Desktop.DriverHud.Services;
using Dms.Shared.Contracts.Devices;
using Dms.Shared.Contracts.Telemetry;

namespace Dms.Desktop.DriverHud.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly DmsApiClient _api;
    private readonly AuthSession _authSession;
    private readonly CameraService _camera;
    private readonly IFaceLandmarkEngine _faceEngine;
    private readonly DeviceIdentityService _deviceIdentity;
    private readonly IncidentQueueService _incidentQueue;
    private readonly AudioAlertService _audioAlert;

    private DeviceConfig _thresholds = new();
    private string? _deviceId;

    public MainViewModel(
        DmsApiClient api,
        AuthSession authSession,
        CameraService camera,
        IFaceLandmarkEngine faceEngine,
        DeviceIdentityService deviceIdentity,
        IncidentQueueService incidentQueue,
        AudioAlertService audioAlert)
    {
        _api = api;
        _authSession = authSession;
        _camera = camera;
        _faceEngine = faceEngine;
        _deviceIdentity = deviceIdentity;
        _incidentQueue = incidentQueue;
        _audioAlert = audioAlert;

        _camera.FrameCaptured += OnFrameCaptured;
        _camera.RawFrameCaptured += OnRawFrameCaptured;
        _camera.CameraError += err => Application.Current.Dispatcher.Invoke(() => CameraStatus = err);
        _incidentQueue.PendingCountChanged += count => Application.Current.Dispatcher.Invoke(() => PendingQueueCount = count);
        _incidentQueue.IncidentSynced += _ => Application.Current.Dispatcher.Invoke(() => LastSyncedAtUtc = DateTimeOffset.UtcNow);

        EngineStatusMessage = _faceEngine.StatusMessage;
    }

    [ObservableProperty] private bool _isAuthenticated;
    [ObservableProperty] private string _email = "";
    [ObservableProperty] private string _password = "";
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _displayName = "";

    [ObservableProperty] private BitmapSource? _previewFrame;
    [ObservableProperty] private string _cameraStatus = "Camera not started.";
    [ObservableProperty] private string _engineStatusMessage;

    [ObservableProperty] private double? _ear;
    [ObservableProperty] private double? _mar;
    [ObservableProperty] private double? _perclos;

    [ObservableProperty] private bool _isAlerting;
    [ObservableProperty] private string? _alertMessage;

    [ObservableProperty] private int _pendingQueueCount;
    [ObservableProperty] private DateTimeOffset? _lastSyncedAtUtc;

    private readonly object _perclosLock = new();
    private PerclosTracker? _perclosTracker;
    private DateTime _eyesClosedSince = DateTime.MinValue;
    private DateTime _mouthOpenSince = DateTime.MinValue;

    [RelayCommand]
    private async Task LoginAsync()
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            var response = await _api.LoginAsync(Email, Password);
            DisplayName = $"{response.DisplayName} ({response.Role})";
            IsAuthenticated = true;
            Password = "";

            await RegisterDeviceAsync();

            _perclosTracker = new PerclosTracker(60.0, _thresholds.EarThreshold);
            _camera.Start();
            CameraStatus = "Camera active";
            _incidentQueue.StartBackgroundSync();
            PendingQueueCount = await _incidentQueue.GetPendingCountAsync();
        }
        catch (DmsApiException ex)
        {
            ErrorMessage = ex.Message.Contains("401") ? "Invalid email or password." : ex.Message;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Cannot reach API server (http://localhost:5287): {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task RegisterDeviceAsync()
    {
        var deviceUuid = _deviceIdentity.GetOrCreateDeviceUuid();
        var request = new DeviceRegistrationRequest(
            deviceUuid, DeviceType.DesktopDriverHud, _authSession.UserId!, FirmwareVersion: null,
            ModelName: Environment.MachineName);

        var response = await _api.RegisterDeviceAsync(request);
        _deviceId = response.DeviceId;
        _thresholds = response.Config;
    }

    [RelayCommand]
    private void SignOut()
    {
        _audioAlert.Stop();
        _camera.Stop();
        CameraStatus = "Camera not started.";
        _authSession.Clear();
        IsAuthenticated = false;
        PreviewFrame = null;
    }

    /// <summary>
    /// Manually fires the same incident pipeline a real detection would use
    /// (queue -> background sync -> TelemetryController -> SignalR broadcast
    /// -> fleet dashboards), so the whole system can be verified end-to-end
    /// before perception-engine's ONNX model exists. See
    /// OnnxFaceLandmarkEngine's class comment for why real detection is
    /// still stubbed.
    /// </summary>
    [RelayCommand]
    private async Task SimulateIncidentAsync()
    {
        if (_deviceId is null || _authSession.UserId is null)
        {
            return;
        }

        var report = new IncidentReport(
            _deviceId, _authSession.UserId, IncidentType.MicroSleep, DateTimeOffset.UtcNow,
            Location: null, Ear: 0.15, Mar: null, Perclos: null, HeadPose: null);

        await TriggerIncidentAsync(report);
    }

    private async Task TriggerIncidentAsync(IncidentReport report)
    {
        await _incidentQueue.EnqueueAsync(report);
        PendingQueueCount = await _incidentQueue.GetPendingCountAsync();

        IsAlerting = true;
        AlertMessage = $"{report.Type} detected at {report.TimestampUtc:T}";
        _audioAlert.PlayAlert(report.Type);
    }

    private DateTime _lastMicroSleepAt = DateTime.MinValue;
    private DateTime _lastPerclosAt = DateTime.MinValue;
    private DateTime _lastYawningAt = DateTime.MinValue;
    private DateTime _lastDistractionAt = DateTime.MinValue;
    private bool _stage1CautionPlayed;

    [RelayCommand]
    private void DismissAlert()
    {
        _audioAlert.Stop();
        IsAlerting = false;
        AlertMessage = null;

        // Reset detection states so acting sleepy again immediately re-triggers the siren alert
        _eyesClosedSince = DateTime.MinValue;
        _stage1CautionPlayed = false;
        _mouthOpenSince = DateTime.MinValue;
        _lastMicroSleepAt = DateTime.MinValue;
        _lastYawningAt = DateTime.MinValue;
        _lastDistractionAt = DateTime.MinValue;
        _lastPerclosAt = DateTime.UtcNow;

        if (_thresholds is not null)
        {
            _perclosTracker = new PerclosTracker(60.0, _thresholds.EarThreshold);
        }
    }

    private void OnFrameCaptured(BitmapSource frame)
    {
        Application.Current.Dispatcher.Invoke(() => PreviewFrame = frame);
    }

    private void OnRawFrameCaptured(OpenCvSharp.Mat frame)
    {
        if (!_faceEngine.TryPredict(frame, out var metrics) || metrics is null)
        {
            return;
        }

        lock (_perclosLock)
        {
            var now = DateTime.UtcNow;
            var perclos = _perclosTracker?.Update((now - DateTime.UnixEpoch).TotalSeconds, metrics.Ear) ?? 0.0;

            Application.Current.Dispatcher.Invoke(() =>
            {
                Ear = metrics.Ear;
                Mar = metrics.Mar;
                Perclos = perclos;
            });

            EvaluateThresholds(metrics, perclos, now);
        }
    }

    private void EvaluateThresholds(FaceMetrics metrics, double perclos, DateTime now)
    {
        if (_deviceId is null)
        {
            return;
        }

        // MicroSleep detection with Two-Stage Progressive Alarm Escalation
        // Normal Blinks (100ms - 400ms): Filtered out cleanly
        // Stage 1 (> 0.4s): Fast Advisory Caution Chime
        // Stage 2 (> 0.8s): Full Continuous Emergency Siren Alarm
        if (metrics.Ear < _thresholds.EarThreshold)
        {
            if (_eyesClosedSince == DateTime.MinValue)
            {
                _eyesClosedSince = now;
                _stage1CautionPlayed = false;
            }

            double duration = (now - _eyesClosedSince).TotalSeconds;

            if (duration >= 0.4 && !_stage1CautionPlayed && !IsAlerting)
            {
                _stage1CautionPlayed = true;
                _audioAlert.PlayCautionChime();
            }

            if (duration >= 0.8 && (now - _lastMicroSleepAt).TotalSeconds >= 1.5)
            {
                _lastMicroSleepAt = now;
                _eyesClosedSince = DateTime.MinValue;
                _stage1CautionPlayed = false;
                _ = TriggerIncidentAsync(new IncidentReport(
                    _deviceId, _authSession.UserId!, IncidentType.MicroSleep, DateTimeOffset.UtcNow,
                    null, metrics.Ear, metrics.Mar, perclos, new HeadPose(metrics.PitchDegrees, metrics.YawDegrees, metrics.RollDegrees)));
            }
        }
        else
        {
            _eyesClosedSince = DateTime.MinValue;
            _stage1CautionPlayed = false;
        }

        // PERCLOS detection (sliding window percentage)
        if (perclos >= _thresholds.PerclosThreshold)
        {
            if ((now - _lastPerclosAt).TotalSeconds >= 10.0 && !IsAlerting)
            {
                _lastPerclosAt = now;
                _ = TriggerIncidentAsync(new IncidentReport(
                    _deviceId, _authSession.UserId!, IncidentType.Perclos, DateTimeOffset.UtcNow,
                    null, metrics.Ear, metrics.Mar, perclos, null));
            }
        }

        // Yawning detection (mouth open > MAR threshold for duration)
        if (metrics.Mar > _thresholds.MarThreshold)
        {
            if (_mouthOpenSince == DateTime.MinValue)
            {
                _mouthOpenSince = now;
            }
            else if ((now - _mouthOpenSince).TotalSeconds >= _thresholds.MarDurationSeconds)
            {
                if ((now - _lastYawningAt).TotalSeconds >= 4.0)
                {
                    _lastYawningAt = now;
                    _ = TriggerIncidentAsync(new IncidentReport(
                        _deviceId, _authSession.UserId!, IncidentType.Yawning, DateTimeOffset.UtcNow,
                        null, metrics.Ear, metrics.Mar, perclos, null));
                }
                _mouthOpenSince = DateTime.MinValue;
            }
        }
        else
        {
            _mouthOpenSince = DateTime.MinValue;
        }

        // Off-road head distraction detection
        if (Math.Abs(metrics.YawDegrees) > _thresholds.YawThresholdDegrees)
        {
            if ((now - _lastDistractionAt).TotalSeconds >= 3.0)
            {
                _lastDistractionAt = now;
                _ = TriggerIncidentAsync(new IncidentReport(
                    _deviceId, _authSession.UserId!, IncidentType.OffRoadDistraction, DateTimeOffset.UtcNow,
                    null, metrics.Ear, metrics.Mar, perclos, new HeadPose(metrics.PitchDegrees, metrics.YawDegrees, metrics.RollDegrees)));
            }
        }
    }
}
