import 'package:flutter/foundation.dart';

import '../api/api_exception.dart';
import '../storage/secure_session_store.dart';
import 'auth_models.dart';
import 'auth_repository.dart';

enum SessionStatus { restoring, signedOut, signingIn, authenticated, unsupportedRole }

class SessionController extends ChangeNotifier {
  SessionController(this._gateway, this._cookieStore);

  final AuthGateway _gateway;
  final SessionCookieStore _cookieStore;
  SessionStatus status = SessionStatus.restoring;
  AuthUser? user;
  String? errorMessage;
  String? _accessToken;
  Future<String?>? _refreshInFlight;

  String? get accessToken => _accessToken;
  bool get hasSupportedRole =>
      user?.roles.any((role) => role == 'Inspector' || role == 'GSD') ?? false;

  Future<void> restore() async {
    status = SessionStatus.restoring;
    errorMessage = null;
    notifyListeners();

    final cookie = await _cookieStore.readRefreshCookie();
    if (cookie == null || cookie.isEmpty) {
      _setSignedOut();
      return;
    }

    try {
      await _refreshSession();
      await _loadCurrentUser();
    } catch (_) {
      await _clearSession();
      errorMessage = 'Your session expired. Please sign in again.';
      _setSignedOut(notify: false);
      notifyListeners();
    }
  }

  Future<void> login(String email, String password) async {
    status = SessionStatus.signingIn;
    errorMessage = null;
    notifyListeners();
    try {
      final result = await _gateway.login(email.trim(), password);
      _accessToken = result.accessToken;
      user = await _gateway.currentUser();
      _setRoleStatus();
    } on ApiException catch (error) {
      await _clearSession();
      errorMessage = error.isUnauthorized
          ? 'Invalid email or password.'
          : 'The mobile service is unavailable. Please try again.';
      _setSignedOut(notify: false);
      notifyListeners();
    } on FormatException {
      await _clearSession();
      errorMessage = 'The server returned an invalid response.';
      _setSignedOut(notify: false);
      notifyListeners();
    } catch (_) {
      await _clearSession();
      errorMessage = 'The mobile service is unavailable. Please try again.';
      _setSignedOut(notify: false);
      notifyListeners();
    }
  }

  Future<String?> refreshForRequest() async {
    final existing = _refreshInFlight;
    if (existing != null) return existing;

    final refresh = _refreshForRequest();
    _refreshInFlight = refresh;
    try {
      return await refresh;
    } finally {
      _refreshInFlight = null;
    }
  }

  Future<String?> _refreshForRequest() async {
    try {
      return await _refreshSession();
    } catch (_) {
      await _clearSession();
      _setSignedOut();
      return null;
    }
  }

  Future<void> logout() async {
    try {
      await _gateway.logout();
    } catch (_) {
      // Local session clearing is required even when the server is unavailable.
    } finally {
      await _clearSession();
      _setSignedOut();
    }
  }

  Future<void> handleTerminalAuthenticationFailure() async {
    await _clearSession();
    _setSignedOut();
  }

  Future<String?> _refreshSession() async {
    final result = await _gateway.refresh();
    _accessToken = result.accessToken;
    user = result.user;
    _setRoleStatus();
    return _accessToken;
  }

  Future<void> _loadCurrentUser() async {
    user = await _gateway.currentUser();
    _setRoleStatus();
  }

  void _setRoleStatus() {
    status = hasSupportedRole
        ? SessionStatus.authenticated
        : SessionStatus.unsupportedRole;
    errorMessage = null;
    notifyListeners();
  }

  Future<void> _clearSession() async {
    _accessToken = null;
    user = null;
    await _cookieStore.clearRefreshCookie();
  }

  void _setSignedOut({bool notify = true}) {
    status = SessionStatus.signedOut;
    if (notify) notifyListeners();
  }
}
