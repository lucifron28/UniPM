import '../api/api_client.dart';
import 'auth_models.dart';

abstract interface class AuthGateway {
  Future<LoginResult> login(String email, String password);
  Future<LoginResult> refresh();
  Future<AuthUser> currentUser();
  Future<void> logout();
}

class AuthRepository implements AuthGateway {
  const AuthRepository(this._client);

  final ApiClient _client;

  @override
  Future<LoginResult> login(String email, String password) async {
    final json = await _client.postJson('/api/v1/auth/login', {
      'email': email,
      'password': password,
    });
    return LoginResult.fromJson(json);
  }

  @override
  Future<LoginResult> refresh() async {
    final json = await _client.postJson('/api/v1/auth/refresh', {});
    return LoginResult.fromJson(json);
  }

  @override
  Future<AuthUser> currentUser() async {
    final json = await _client.getJson('/api/v1/auth/me');
    return AuthUser.fromJson(json);
  }

  @override
  Future<void> logout() => _client.postEmpty('/api/v1/auth/logout');
}
