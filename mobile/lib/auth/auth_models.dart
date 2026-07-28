class AuthUser {
  const AuthUser({
    required this.id,
    required this.email,
    required this.displayName,
    required this.roles,
  });

  final String id;
  final String email;
  final String displayName;
  final List<String> roles;

  factory AuthUser.fromJson(Map<String, dynamic> json) {
    final roles = json['roles'];
    if (json['id'] is! String ||
        json['email'] is! String ||
        json['displayName'] is! String ||
        roles is! List) {
      throw const FormatException('Invalid authenticated-user response.');
    }
    return AuthUser(
      id: json['id'] as String,
      email: json['email'] as String,
      displayName: json['displayName'] as String,
      roles: roles.whereType<String>().toList(growable: false),
    );
  }
}

class LoginResult {
  const LoginResult({required this.accessToken, required this.user});

  final String accessToken;
  final AuthUser user;

  factory LoginResult.fromJson(Map<String, dynamic> json) {
    if (json['accessToken'] is! String || json['user'] is! Map) {
      throw const FormatException('Invalid login response.');
    }
    return LoginResult(
      accessToken: json['accessToken'] as String,
      user: AuthUser.fromJson(
        (json['user'] as Map).cast<String, dynamic>(),
      ),
    );
  }
}
