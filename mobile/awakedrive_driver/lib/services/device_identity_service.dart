import 'package:shared_preferences/shared_preferences.dart';
import 'package:uuid/uuid.dart';

/// Persists a stable per-install device UUID (the Flutter equivalent of the
/// desktop HUD's DeviceIdentityService) so re-launching the app re-registers
/// the *same* device instead of creating a new one each cold start.
class DeviceIdentityService {
  static const _key = 'deviceUuid';

  Future<String> getOrCreateDeviceUuid() async {
    final prefs = await SharedPreferences.getInstance();
    final existing = prefs.getString(_key);
    if (existing != null) return existing;

    final generated = const Uuid().v4();
    await prefs.setString(_key, generated);
    return generated;
  }
}
