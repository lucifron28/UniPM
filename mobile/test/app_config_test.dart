import 'package:flutter_test/flutter_test.dart';

import 'package:mobile/config/app_config.dart';

void main() {
  test('debug configuration accepts an HTTP development API URL', () {
    final config = AppConfig.fromRawBaseUrl(
      'http://10.0.2.2:5000/',
      isRelease: false,
    );

    expect(config.apiBaseUrl, Uri.parse('http://10.0.2.2:5000/'));
    expect(config.errorMessage, isNull);
  });

  test('release configuration rejects an HTTP API URL', () {
    final config = AppConfig.fromRawBaseUrl(
      'http://api.example.test/',
      isRelease: true,
    );

    expect(config.apiBaseUrl, isNull);
    expect(
      config.errorMessage,
      'Release builds require an HTTPS mobile API URL.',
    );
  });

  test('release configuration accepts an HTTPS API URL', () {
    final config = AppConfig.fromRawBaseUrl(
      'https://api.example.test/',
      isRelease: true,
    );

    expect(config.apiBaseUrl, Uri.parse('https://api.example.test/'));
    expect(config.errorMessage, isNull);
  });

  test('configuration rejects unsupported URL schemes', () {
    final config = AppConfig.fromRawBaseUrl(
      'ftp://api.example.test/',
      isRelease: false,
    );

    expect(config.apiBaseUrl, isNull);
    expect(config.errorMessage, 'The configured mobile API URL is invalid.');
  });
}
