import 'package:flutter/foundation.dart';

import '../api/api_exception.dart';
import 'auth_models.dart';
import 'auth_repository.dart';

enum SessionStatus { signedOut, signingIn, authenticated, unsupportedRole }

class SessionController extends ChangeNotifier {
  SessionController(this._gateway);

  final AuthGateway _gateway;
  SessionStatus status = SessionStatus.signedOut;
  AuthUser? user;
  String? errorMessage;
  String? _accessToken;

  String? get accessToken => _accessToken;
  bool get hasSupportedRole =>
      user?.roles.any((role) => role == 'Inspector' || role == 'GSD') ?? false;

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
      _clearSession();
      errorMessage =
          error.message == 'Your session expired. Please sign in again.'
          ? error.message
          : error.isUnauthorized
          ? 'Invalid email or password.'
          : 'The mobile service is unavailable. Please try again.';
      _setSignedOut(notify: false);
      notifyListeners();
    } on FormatException {
      _clearSession();
      errorMessage = 'The server returned an invalid response.';
      _setSignedOut(notify: false);
      notifyListeners();
    } catch (_) {
      _clearSession();
      errorMessage = 'The mobile service is unavailable. Please try again.';
      _setSignedOut(notify: false);
      notifyListeners();
    }
  }

  Future<void> logout() async {
    try {
      await _gateway.logout();
    } catch (_) {
      // Local session clearing is required even when the server is unavailable.
    } finally {
      _clearSession();
      _setSignedOut();
    }
  }

  Future<void> handleTerminalAuthenticationFailure() async {
    _clearSession();
    errorMessage = 'Your session expired. Please sign in again.';
    _setSignedOut(notify: false);
    notifyListeners();
  }

  void _setRoleStatus() {
    status = hasSupportedRole
        ? SessionStatus.authenticated
        : SessionStatus.unsupportedRole;
    errorMessage = null;
    notifyListeners();
  }

  void _clearSession() {
    _accessToken = null;
    user = null;
  }

  void _setSignedOut({bool notify = true}) {
    status = SessionStatus.signedOut;
    if (notify) notifyListeners();
  }
}
