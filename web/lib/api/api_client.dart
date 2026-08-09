import 'dart:convert';

import 'package:http/http.dart' as http;

import 'models/login_response.dart';
import 'models/me_response.dart';

// No checked-in config file: the default covers plain `flutter run` for
// local dev; production builds pass
// --dart-define=API_BASE_URL=https://sitewatch-production-4647.up.railway.app
const String apiBaseUrl = String.fromEnvironment(
  'API_BASE_URL',
  defaultValue: 'http://localhost:5129',
);

class InvalidCredentialsException implements Exception {}

class NetworkException implements Exception {}

class ApiClient {
  final http.Client _client;
  String? _token;

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

  void _throwIfNotOk(http.Response response) {
    if (response.statusCode < 200 || response.statusCode >= 300) {
      throw NetworkException();
    }
  }
}
