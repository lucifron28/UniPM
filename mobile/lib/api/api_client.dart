import 'dart:convert';

import 'package:http/http.dart' as http;

import 'api_exception.dart';

typedef AccessTokenProvider = String? Function();
typedef TerminalAuthFailureHandler = Future<void> Function();
typedef HttpClientFactory = http.Client Function();

class ApiClient {
  ApiClient({
    required this.baseUrl,
    http.Client? httpClient,
    HttpClientFactory? httpClientFactory,
  }) : assert(httpClient == null || httpClientFactory == null),
       _httpClientFactory = _resolveClientFactory(
         httpClient,
         httpClientFactory,
       ),
       _disposeClients = httpClient == null;

  final Uri? baseUrl;
  final HttpClientFactory _httpClientFactory;
  final bool _disposeClients;
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

  Future<List<dynamic>> getJsonList(String path) async {
    final response = await _request('GET', path);
    return _decodeList(response);
  }

  Future<Map<String, dynamic>> postJson(
    String path, [
    Map<String, dynamic>? body,
  ]) async {
    final response = await _request('POST', path, body: body);
    return _decodeObject(response);
  }

  Future<void> postEmpty(String path) async {
    await _request('POST', path);
  }

  Future<Map<String, dynamic>> putJson(
    String path,
    Map<String, dynamic> body,
  ) async {
    final response = await _request('PUT', path, body: body);
    return _decodeObject(response);
  }

  Future<void> deleteEmpty(String path) async {
    await _request('DELETE', path);
  }

  Future<http.Response> _request(
    String method,
    String path, {
    Map<String, dynamic>? body,
  }) async {
    final response = await _send(method, path, body: body);
    final protectedUnauthorized =
        response.statusCode == 401 && !_isTokenFreeAuthPath(path);
    if (protectedUnauthorized) {
      await _terminalAuthFailureHandler?.call();
    }

    _throwForStatus(response, expiredSession: protectedUnauthorized);
    return response;
  }

  Future<http.Response> _send(
    String method,
    String path, {
    Map<String, dynamic>? body,
  }) async {
    if (baseUrl == null) {
      throw const ApiException(
        message: 'The mobile API URL is not configured.',
      );
    }

    final request = http.Request(method, _resolve(path));
    request.followRedirects = false;
    request.maxRedirects = 0;
    request.headers['Accept'] = 'application/json';
    if (body != null) {
      request.headers['Content-Type'] = 'application/json';
      request.body = jsonEncode(body);
    }

    final accessToken = _accessTokenProvider();
    if (accessToken != null && !_isTokenFreeAuthPath(path)) {
      request.headers['Authorization'] = 'Bearer $accessToken';
    }

    final client = _httpClientFactory();
    try {
      return await http.Response.fromStream(await client.send(request));
    } catch (_) {
      throw const ApiException(
        message: 'The mobile service is unavailable. Please try again.',
      );
    } finally {
      if (_disposeClients) client.close();
    }
  }

  Uri _resolve(String path) {
    final relativePath = path.startsWith('/') ? path.substring(1) : path;
    return baseUrl!.resolve(relativePath);
  }

  static bool _isTokenFreeAuthPath(String path) =>
      path == '/api/v1/auth/login' || path == '/api/v1/auth/logout';

  static HttpClientFactory _resolveClientFactory(
    http.Client? client,
    HttpClientFactory? factory,
  ) {
    if (factory != null) return factory;
    if (client != null) return () => client;
    return () => http.Client();
  }

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

  static List<dynamic> _decodeList(http.Response response) {
    if (response.body.trim().isEmpty) {
      throw const ApiException(
        message: 'The server returned an invalid response.',
      );
    }

    try {
      final decoded = jsonDecode(response.body);
      if (decoded is List<dynamic>) return decoded;
    } catch (_) {
      throw const ApiException(
        message: 'The server returned an invalid response.',
      );
    }

    throw const ApiException(
      message: 'The server returned an invalid response.',
    );
  }

  static void _throwForStatus(
    http.Response response, {
    bool expiredSession = false,
  }) {
    if (response.statusCode >= 200 && response.statusCode < 300) return;
    if (response.statusCode == 401) {
      throw ApiException(
        statusCode: 401,
        message: expiredSession
            ? 'Your session expired. Please sign in again.'
            : 'Invalid credentials or expired session.',
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

  void dispose() {}
}
