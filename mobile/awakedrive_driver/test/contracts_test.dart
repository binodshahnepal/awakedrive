import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:awakedrive_driver/models/contracts.dart';

void main() {
  group('LoginResponse', () {
    test('parses a camelCase JSON payload matching Dms.Api\'s wire format', () {
      const json = '''
      {
        "accessToken": "token123",
        "expiresAtUtc": "2026-01-01T12:00:00+00:00",
        "userId": "00000000-0000-0000-0000-000000000001",
        "displayName": "Default Admin",
        "role": 2
      }
      ''';

      final response = LoginResponse.fromJson(jsonDecode(json) as Map<String, dynamic>);

      expect(response.accessToken, 'token123');
      expect(response.userId, '00000000-0000-0000-0000-000000000001');
      expect(response.displayName, 'Default Admin');
      expect(response.role, UserRole.admin);
    });
  });

  group('IncidentReport', () {
    test('round-trips through toJson/fromJson', () {
      final original = IncidentReport(
        deviceId: 'device-1',
        driverId: 'driver-1',
        type: IncidentType.yawning,
        timestampUtc: DateTime.utc(2026, 1, 1, 12, 0, 0),
        location: GeoPoint(37.7749, -122.4194),
        ear: 0.15,
        mar: 0.7,
      );

      final roundTripped = IncidentReport.fromJson(jsonDecode(jsonEncode(original.toJson())) as Map<String, dynamic>);

      expect(roundTripped.deviceId, original.deviceId);
      expect(roundTripped.driverId, original.driverId);
      expect(roundTripped.type, IncidentType.yawning);
      expect(roundTripped.location!.latitude, closeTo(37.7749, 0.0001));
      expect(roundTripped.ear, closeTo(0.15, 0.0001));
    });

    test('enum ordinals match the backend\'s IncidentType (int-serialized, no string converter)', () {
      expect(IncidentType.microSleep.toJson(), 0);
      expect(IncidentType.perclos.toJson(), 1);
      expect(IncidentType.yawning.toJson(), 2);
      expect(IncidentType.offRoadDistraction.toJson(), 3);
    });
  });

  group('UserRole / DeviceType ordinals', () {
    test('match the backend enums exactly', () {
      expect(UserRole.driver.toJson(), 0);
      expect(UserRole.fleetManager.toJson(), 1);
      expect(UserRole.admin.toJson(), 2);

      expect(DeviceType.mobileAndroid.toJson(), 0);
      expect(DeviceType.mobileIos.toJson(), 1);
      expect(DeviceType.desktopDriverHud.toJson(), 2);
      expect(DeviceType.dashcam.toJson(), 3);
    });
  });
}
