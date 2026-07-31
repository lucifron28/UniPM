import 'dart:convert';

import 'package:http/http.dart' as http;

import 'api_exception.dart';

typedef AccessTokenProvider = String? Function();
typedef TerminalAuthFailureHandler = Future<void> Function();

class ApiClient {
  ApiClient({
    required this.baseUrl,
    http.Client? httpClient,
  }) : _httpClient = httpClient ?? http.Client();

  final Uri? baseUrl;
  final http.Client _httpClient;
  AccessTokenProvider _accessTokenProvider = () => null;
  TerminalAuthFailureHandler? _terminalAuthFailureHandler;

  void configureSession({
    required AccessTokenProvider accessTokenProvider,
    required TerminalAuthFailureHandler terminalAuthFailureHandler,
  }) {
    _accessTokenProvider = accessTokenProvider;
    _terminalAuthFailureHandler = terminalAuthFailureHandler;
  }

  Future<Map<String, dynamic>> getJson(String path) async {
    final response = await _request('GET', path);
    return _decodeObject(response);
  }

  Future<Map<String, dynamic>> postJson(
    String path,
    Map<String, dynamic> body,
  ) async {
    final response = await _request('POST', path, body: body);
    return _decodeObject(response);
  }

  Future<void> postEmpty(String path) async {
    await _request('POST', path);
  }

  Future<http.Response> _request(
    String method,
    String path, {
    Map<String, dynamic>? body,
  }) async {
    final response = await _send(method, path, body: body);
    if (response.statusCode == 401 && !_isTokenFreeAuthPath(path)) {
      await _terminalAuthFailureHandler?.call();
    }

    _throwForStatus(response);
    return response;
  }

  Future<http.Response> _send(
    String method,
    String path, {
    Map<String, dynamic>? body,
  }) async {
    if (baseUrl == null) {
      throw const ApiException(message: 'The mobile API URL is not configured.');
    }

    final request = http.Request(method, _resolve(path));
    request.headers['Accept'] = 'application/json';
    if (body != null) {
      request.headers['Content-Type'] = 'application/json';
      request.body = jsonEncode(body);
    }

    final accessToken = _accessTokenProvider();
    if (accessToken != null && !_isTokenFreeAuthPath(path)) {
      request.headers['Authorization'] = 'Bearer $accessToken';
    }

    try {
      return await http.Response.fromStream(await _httpClient.send(request));
    } catch (_) {
      throw const ApiException(
        message: 'The mobile service is unavailable. Please try again.',
      );
    }
  }

  Uri _resolve(String path) {
    final relativePath = path.startsWith('/') ? path.substring(1) : path;
    return baseUrl!.resolve(relativePath);
  }

  static bool _isTokenFreeAuthPath(String path) =>
      path == '/api/v1/auth/login' || path == '/api/v1/auth/logout';

  static Map<String, dynamic> _decodeObject(http.Response response) {
    if (response.body.trim().isEmpty) return <String, dynamic>{};
    try {
      final decoded = jsonDecode(response.body);
      if (decoded is Map<String, dynamic>) return decoded;
    } catch (_) {
      throw const ApiException(
        message: 'The server returned an invalid response.',
      );
    }
    throw const ApiException(
      message: 'The server returned an invalid response.',
    );
  }

  static void _throwForStatus(http.Response response) {
    if (response.statusCode >= 200 && response.statusCode < 300) return;
    if (response.statusCode == 401) {
      throw const ApiException(
        statusCode: 401,
        message: 'Invalid credentials or expired session.',
      );
    }
    if (response.statusCode == 403) {
      throw const ApiException(
        statusCode: 403,
        message: 'This account is not supported by the mobile application.',
      );
    }
    if (response.statusCode >= 500) {
      throw const ApiException(
        message: 'The mobile service is unavailable. Please try again.',
      );
    }
    throw ApiException(
      statusCode: response.statusCode,
      message: 'The request could not be completed.',
    );
  }

  void dispose() => _httpClient.close();
}
