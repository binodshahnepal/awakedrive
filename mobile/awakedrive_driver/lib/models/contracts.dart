/// Dart mirrors of backend/Dms.Shared.Contracts — kept field-for-field and
/// name-for-name (camelCase, matching ASP.NET Core's default JSON casing) so
/// this app speaks the exact same wire format as the WPF/Blazor clients.
library;

enum UserRole {
  driver,
  fleetManager,
  admin;

  static UserRole fromJson(int index) => UserRole.values[index];
  int toJson() => index;
}

enum DeviceType {
  mobileAndroid,
  mobileIos,
  desktopDriverHud,
  dashcam;

  static DeviceType fromJson(int index) => DeviceType.values[index];
  int toJson() => index;
}

enum IncidentType {
  microSleep,
  perclos,
  yawning,
  offRoadDistraction;

  static IncidentType fromJson(int index) => IncidentType.values[index];
  int toJson() => index;

  String get label => switch (this) {
        IncidentType.microSleep => 'Micro-sleep',
        IncidentType.perclos => 'PERCLOS',
        IncidentType.yawning => 'Yawning',
        IncidentType.offRoadDistraction => 'Off-road distraction',
      };
}

class LoginRequest {
  final String email;
  final String password;
  LoginRequest(this.email, this.password);

  Map<String, dynamic> toJson() => {'email': email, 'password': password};
}

class LoginResponse {
  final String accessToken;
  final DateTime expiresAtUtc;
  final String userId;
  final String displayName;
  final UserRole role;

  LoginResponse({
    required this.accessToken,
    required this.expiresAtUtc,
    required this.userId,
    required this.displayName,
    required this.role,
  });

  factory LoginResponse.fromJson(Map<String, dynamic> json) => LoginResponse(
        accessToken: json['accessToken'] as String,
        expiresAtUtc: DateTime.parse(json['expiresAtUtc'] as String),
        userId: json['userId'] as String,
        displayName: json['displayName'] as String,
        role: UserRole.fromJson(json['role'] as int),
      );
}

class DeviceConfig {
  final double earThreshold;
  final double earDurationSeconds;
  final double perclosThreshold;
  final double marThreshold;
  final double marDurationSeconds;
  final double yawThresholdDegrees;
  final double yawDurationSeconds;

  const DeviceConfig({
    this.earThreshold = 0.20,
    this.earDurationSeconds = 1.5,
    this.perclosThreshold = 0.15,
    this.marThreshold = 0.60,
    this.marDurationSeconds = 2.5,
    this.yawThresholdDegrees = 25.0,
    this.yawDurationSeconds = 2.0,
  });

  factory DeviceConfig.fromJson(Map<String, dynamic> json) => DeviceConfig(
        earThreshold: (json['earThreshold'] as num).toDouble(),
        earDurationSeconds: (json['earDurationSeconds'] as num).toDouble(),
        perclosThreshold: (json['perclosThreshold'] as num).toDouble(),
        marThreshold: (json['marThreshold'] as num).toDouble(),
        marDurationSeconds: (json['marDurationSeconds'] as num).toDouble(),
        yawThresholdDegrees: (json['yawThresholdDegrees'] as num).toDouble(),
        yawDurationSeconds: (json['yawDurationSeconds'] as num).toDouble(),
      );
}

class DeviceRegistrationRequest {
  final String deviceUuid;
  final DeviceType deviceType;
  final String driverId;
  final String? firmwareVersion;
  final String? modelName;

  DeviceRegistrationRequest({
    required this.deviceUuid,
    required this.deviceType,
    required this.driverId,
    this.firmwareVersion,
    this.modelName,
  });

  Map<String, dynamic> toJson() => {
        'deviceUuid': deviceUuid,
        'deviceType': deviceType.toJson(),
        'driverId': driverId,
        'firmwareVersion': firmwareVersion,
        'modelName': modelName,
      };
}

class DeviceRegistrationResponse {
  final String deviceId;
  final DateTime registeredAtUtc;
  final DeviceConfig config;

  DeviceRegistrationResponse({required this.deviceId, required this.registeredAtUtc, required this.config});

  factory DeviceRegistrationResponse.fromJson(Map<String, dynamic> json) => DeviceRegistrationResponse(
        deviceId: json['deviceId'] as String,
        registeredAtUtc: DateTime.parse(json['registeredAtUtc'] as String),
        config: DeviceConfig.fromJson(json['config'] as Map<String, dynamic>),
      );
}

class GeoPoint {
  final double latitude;
  final double longitude;
  GeoPoint(this.latitude, this.longitude);

  Map<String, dynamic> toJson() => {'latitude': latitude, 'longitude': longitude};
}

class HeadPose {
  final double pitch;
  final double yaw;
  final double roll;
  HeadPose(this.pitch, this.yaw, this.roll);

  Map<String, dynamic> toJson() => {'pitch': pitch, 'yaw': yaw, 'roll': roll};
}

class IncidentReport {
  final String deviceId;
  final String driverId;
  final IncidentType type;
  final DateTime timestampUtc;
  final GeoPoint? location;
  final double? ear;
  final double? mar;
  final double? perclos;
  final HeadPose? headPose;

  IncidentReport({
    required this.deviceId,
    required this.driverId,
    required this.type,
    required this.timestampUtc,
    this.location,
    this.ear,
    this.mar,
    this.perclos,
    this.headPose,
  });

  Map<String, dynamic> toJson() => {
        'deviceId': deviceId,
        'driverId': driverId,
        'type': type.toJson(),
        'timestampUtc': timestampUtc.toUtc().toIso8601String(),
        'location': location?.toJson(),
        'ear': ear,
        'mar': mar,
        'perclos': perclos,
        'headPose': headPose?.toJson(),
      };

  factory IncidentReport.fromJson(Map<String, dynamic> json) => IncidentReport(
        deviceId: json['deviceId'] as String,
        driverId: json['driverId'] as String,
        type: IncidentType.fromJson(json['type'] as int),
        timestampUtc: DateTime.parse(json['timestampUtc'] as String),
        location: json['location'] == null
            ? null
            : GeoPoint((json['location']['latitude'] as num).toDouble(), (json['location']['longitude'] as num).toDouble()),
        ear: (json['ear'] as num?)?.toDouble(),
        mar: (json['mar'] as num?)?.toDouble(),
        perclos: (json['perclos'] as num?)?.toDouble(),
      );
}

class IncidentIngestResponse {
  final String incidentId;
  final bool alertBroadcast;
  IncidentIngestResponse({required this.incidentId, required this.alertBroadcast});

  factory IncidentIngestResponse.fromJson(Map<String, dynamic> json) => IncidentIngestResponse(
        incidentId: json['incidentId'] as String,
        alertBroadcast: json['alertBroadcast'] as bool,
      );
}
