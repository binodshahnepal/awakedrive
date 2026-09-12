import 'dart:convert';
import 'package:http/http.dart' as http;

import '../models/contracts.dart';
import 'auth_session.dart';

class DmsApiException implements Exception {
  final String message;
  DmsApiException(this.message);
  @override
  String toString() => message;
}

/// Thin wrapper over Dms.Api's REST surface — the Dart equivalent of
/// backend/Dms.Client.Api's DmsApiClient, so this app speaks the exact same
/// contract as the WPF/Blazor clients.
class DmsApiClient {
  final String baseUrl;
  final AuthSession authSession;
  final http.Client _http;

  DmsApiClient({required this.baseUrl, required this.authSession, http.Client? httpClient})
      : _http = httpClient ?? http.Client();

  Uri _uri(String path) => Uri.parse('$baseUrl$path');

  Map<String, String> _headers({bool auth = false}) => {
        'Content-Type': 'application/json',
        if (auth && authSession.accessToken != null) 'Authorization': 'Bearer ${authSession.accessToken}',
      };

  Future<LoginResponse> login(String email, String password) async {
    final response = await _http.post(
      _uri('/api/v1/auth/login'),
      headers: _headers(),
      body: jsonEncode(LoginRequest(email, password).toJson()),
    );
    _ensureSuccess(response);
    final result = LoginResponse.fromJson(jsonDecode(response.body) as Map<String, dynamic>);
    await authSession.setFromLogin(result);
    return result;
  }

  Future<DeviceRegistrationResponse> registerDevice(DeviceRegistrationRequest request) async {
    final response = await _http.post(
      _uri('/api/v1/devices/register'),
      headers: _headers(auth: true),
      body: jsonEncode(request.toJson()),
    );
    _ensureSuccess(response);
    return DeviceRegistrationResponse.fromJson(jsonDecode(response.body) as Map<String, dynamic>);
  }

  Future<IncidentIngestResponse> postIncident(IncidentReport report) async {
    final response = await _http.post(
      _uri('/api/v1/telemetry/incidents'),
      headers: _headers(auth: true),
      body: jsonEncode(report.toJson()),
    );
    _ensureSuccess(response);
    return IncidentIngestResponse.fromJson(jsonDecode(response.body) as Map<String, dynamic>);
  }

  void _ensureSuccess(http.Response response) {
    if (response.statusCode >= 200 && response.statusCode < 300) return;
    throw DmsApiException('${response.statusCode} ${response.reasonPhrase}: ${response.body}');
  }
}
