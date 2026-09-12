import 'package:flutter/foundation.dart';
import 'package:shared_preferences/shared_preferences.dart';

import '../models/contracts.dart';

/// Holds the current driver's JWT + profile, persisted to SharedPreferences
/// so the app doesn't force a re-login every cold start (unlike the desktop
/// apps' in-memory-only AuthSession — a phone gets backgrounded/killed far
/// more often than a desktop app, so persistence matters more here).
class AuthSession extends ChangeNotifier {
  String? accessToken;
  DateTime? expiresAtUtc;
  String? userId;
  String? displayName;
  UserRole? role;

  bool get isAuthenticated => accessToken != null && (expiresAtUtc?.isAfter(DateTime.now().toUtc()) ?? false);

  Future<void> setFromLogin(LoginResponse response) async {
    accessToken = response.accessToken;
    expiresAtUtc = response.expiresAtUtc;
    userId = response.userId;
    displayName = response.displayName;
    role = response.role;

    final prefs = await SharedPreferences.getInstance();
    await prefs.setString('accessToken', accessToken!);
    await prefs.setString('expiresAtUtc', expiresAtUtc!.toIso8601String());
    await prefs.setString('userId', userId!);
    await prefs.setString('displayName', displayName!);
    await prefs.setInt('role', role!.toJson());

    notifyListeners();
  }

  Future<void> restore() async {
    final prefs = await SharedPreferences.getInstance();
    final token = prefs.getString('accessToken');
    final expires = prefs.getString('expiresAtUtc');
    if (token == null || expires == null) return;

    final expiresAt = DateTime.parse(expires);
    if (expiresAt.isBefore(DateTime.now().toUtc())) {
      await clear();
      return;
    }

    accessToken = token;
    expiresAtUtc = expiresAt;
    userId = prefs.getString('userId');
    displayName = prefs.getString('displayName');
    final roleIndex = prefs.getInt('role');
    role = roleIndex == null ? null : UserRole.fromJson(roleIndex);
    notifyListeners();
  }

  Future<void> clear() async {
    accessToken = null;
    expiresAtUtc = null;
    userId = null;
    displayName = null;
    role = null;

    final prefs = await SharedPreferences.getInstance();
    await prefs.clear();

    notifyListeners();
  }
}
