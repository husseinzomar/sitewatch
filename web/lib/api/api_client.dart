import 'dart:convert';

import 'package:http/http.dart' as http;

import 'models/check_result_response.dart';
import 'models/login_response.dart';
import 'models/me_response.dart';
import 'models/site_response.dart';

// No checked-in config file: the default covers plain `flutter run` for
// local dev; production builds pass
// --dart-define=API_BASE_URL=https://sitewatch-production-4647.up.railway.app
const String apiBaseUrl = String.fromEnvironment(
  'API_BASE_URL',
  defaultValue: 'http://localhost:5129',
);

class InvalidCredentialsException implements Exception {}

// A session that was valid and then wasn't — distinct from
// InvalidCredentialsException (wrong password, no session ever existed).
class UnauthorizedException implements Exception {}

class ApiValidationException implements Exception {
  final String message;
  ApiValidationException(this.message);
}

class NetworkException implements Exception {}

class ApiClient {
  final http.Client _client;
  String? _token;

  // Single choke point for session-expiry handling: every authenticated
  // call funnels through _throwIfNotOk, so this fires exactly once per
  // 401, regardless of which screen or provider triggered the call.
  void Function()? onUnauthorized;

  ApiClient({http.Client? client}) : _client = client ?? http.Client();

  void setToken(String? token) => _token = token;

  Map<String, String> get _headers => {
        'Content-Type': 'application/json',
        if (_token != null) 'Authorization': 'Bearer $_token',
      };

  Future<LoginResponse> login(String email, String password) async {
    final response = await _post('/auth/login', {
      'email': email,
      'password': password,
    });

    // Checked before _throwIfNotOk deliberately: a rejected login is not a
    // session expiring (there's no session yet), so it must never trigger
    // onUnauthorized.
    if (response.statusCode == 401) {
      throw InvalidCredentialsException();
    }
    _throwIfNotOk(response);

    return LoginResponse.fromJson(jsonDecode(response.body) as Map<String, dynamic>);
  }

  Future<MeResponse> getMe() async {
    final response = await _get('/me');
    _throwIfNotOk(response);
    return MeResponse.fromJson(jsonDecode(response.body) as Map<String, dynamic>);
  }

  Future<List<SiteResponse>> getSites() async {
    final response = await _get('/sites');
    _throwIfNotOk(response);
    final list = jsonDecode(response.body) as List<dynamic>;
    return list.map((e) => SiteResponse.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<SiteResponse> createSite(String name, String url) async {
    final response = await _post('/sites', {'name': name, 'url': url});

    // Checked before _throwIfNotOk so the caller gets the server's actual
    // validation message, not a generic failure.
    if (response.statusCode == 400) {
      throw ApiValidationException(_extractError(response));
    }
    _throwIfNotOk(response);

    return SiteResponse.fromJson(jsonDecode(response.body) as Map<String, dynamic>);
  }

  Future<void> deleteSite(String id) async {
    final response = await _delete('/sites/$id');
    _throwIfNotOk(response);
  }

  Future<List<CheckResultResponse>> getSiteResults(String siteId) async {
    final response = await _get('/sites/$siteId/results');
    _throwIfNotOk(response);
    final list = jsonDecode(response.body) as List<dynamic>;
    return list.map((e) => CheckResultResponse.fromJson(e as Map<String, dynamic>)).toList();
  }

  Future<http.Response> _get(String path) async {
    try {
      return await _client.get(Uri.parse('$apiBaseUrl$path'), headers: _headers);
    } on http.ClientException {
      throw NetworkException();
    }
  }

  Future<http.Response> _post(String path, Map<String, dynamic> body) async {
    try {
      return await _client.post(
        Uri.parse('$apiBaseUrl$path'),
        headers: _headers,
        body: jsonEncode(body),
      );
    } on http.ClientException {
      throw NetworkException();
    }
  }

  Future<http.Response> _delete(String path) async {
    try {
      return await _client.delete(Uri.parse('$apiBaseUrl$path'), headers: _headers);
    } on http.ClientException {
      throw NetworkException();
    }
  }

  void _throwIfNotOk(http.Response response) {
    if (response.statusCode == 401) {
      onUnauthorized?.call();
      throw UnauthorizedException();
    }
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw NetworkException();
    }
  }

  String _extractError(http.Response response) {
    try {
      final body = jsonDecode(response.body) as Map<String, dynamic>;
      return body['error'] as String? ?? 'Request failed.';
    } catch (_) {
      return 'Request failed.';
    }
  }
}
