import 'package:flutter_secure_storage/flutter_secure_storage.dart';

abstract interface class SessionCookieStore {
  Future<String?> readRefreshCookie();
  Future<void> writeRefreshCookie(String cookie);
  Future<void> clearRefreshCookie();
}

class SecureSessionStore implements SessionCookieStore {
  SecureSessionStore({FlutterSecureStorage? storage})
      : _storage = storage ?? const FlutterSecureStorage();

  static const _cookieKey = 'unipm_refresh_cookie';
  final FlutterSecureStorage _storage;

  @override
  Future<String?> readRefreshCookie() => _storage.read(key: _cookieKey);

  @override
  Future<void> writeRefreshCookie(String cookie) =>
      _storage.write(key: _cookieKey, value: cookie);

  @override
  Future<void> clearRefreshCookie() => _storage.delete(key: _cookieKey);
}
