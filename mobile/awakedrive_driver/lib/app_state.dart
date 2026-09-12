import 'package:flutter/foundation.dart';

import 'models/contracts.dart';
import 'services/api_client.dart';
import 'services/auth_session.dart';
import 'services/device_identity_service.dart';
import 'services/incident_queue_service.dart';

/// Backend base URL. 10.0.2.2 is the Android emulator's alias for the host
/// machine's localhost — the same Dms.Api instance the desktop/web clients
/// point at. Override for a physical device via --dart-define=API_BASE_URL=...
const String apiBaseUrl = String.fromEnvironment('API_BASE_URL', defaultValue: 'http://10.0.2.2:5287');

/// App-wide state: login, device registration, and the incident pipeline.
/// The Dart equivalent of the desktop Driver HUD's MainViewModel.
class AppState extends ChangeNotifier {
  final AuthSession authSession = AuthSession();
  late final DmsApiClient api;
  late final IncidentQueueService incidentQueue;
  final DeviceIdentityService deviceIdentity = DeviceIdentityService();

  DeviceConfig thresholds = const DeviceConfig();
  String? deviceId;

  bool isBusy = false;
  String? errorMessage;
  int pendingQueueCount = 0;

  AppState() {
    api = DmsApiClient(baseUrl: apiBaseUrl, authSession: authSession);
    incidentQueue = IncidentQueueService(api: api);
    incidentQueue.pendingCountStream.listen((count) {
      pendingQueueCount = count;
      notifyListeners();
    });
  }

  Future<void> restoreSession() async {
    await authSession.restore();
    if (authSession.isAuthenticated) {
      await _afterLogin();
    }
    notifyListeners();
  }

  Future<bool> login(String email, String password) async {
    errorMessage = null;
    isBusy = true;
    notifyListeners();
    try {
      await api.login(email, password);
      await _afterLogin();
      return true;
    } on DmsApiException catch (e) {
      errorMessage = e.message.contains('401') ? 'Invalid email or password.' : e.message;
      return false;
    } finally {
      isBusy = false;
      notifyListeners();
    }
  }

  Future<void> _afterLogin() async {
    try {
      final deviceUuid = await deviceIdentity.getOrCreateDeviceUuid();
      final response = await api.registerDevice(DeviceRegistrationRequest(
        deviceUuid: deviceUuid,
        deviceType: DeviceType.mobileAndroid,
        driverId: authSession.userId!,
        modelName: 'Android',
      ));
      deviceId = response.deviceId;
      thresholds = response.config;
    } on DmsApiException catch (e) {
      errorMessage = 'Device registration failed: ${e.message}';
    }

    pendingQueueCount = await incidentQueue.pendingCount();
    incidentQueue.startBackgroundSync();
  }

  Future<void> signOut() async {
    incidentQueue.stopBackgroundSync();
    await authSession.clear();
    deviceId = null;
    notifyListeners();
  }

  /// Manually fires the same incident pipeline a real detection would use
  /// (queue -> background sync -> TelemetryController -> SignalR broadcast
  /// -> fleet dashboards), so the whole system can be exercised end-to-end
  /// before perception-engine's on-device model exists. See the "Perception
  /// engine" note in hud_screen.dart for why real detection is still a
  /// placeholder here.
  Future<void> simulateIncident() async {
    if (deviceId == null || authSession.userId == null) return;

    final report = IncidentReport(
      deviceId: deviceId!,
      driverId: authSession.userId!,
      type: IncidentType.microSleep,
      timestampUtc: DateTime.now().toUtc(),
      ear: 0.15,
    );
    await incidentQueue.enqueue(report);
    pendingQueueCount = await incidentQueue.pendingCount();
    notifyListeners();
  }

  @override
  void dispose() {
    incidentQueue.dispose();
    super.dispose();
  }
}
