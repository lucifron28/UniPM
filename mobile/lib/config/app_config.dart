import 'package:flutter/foundation.dart';

class AppConfig {
  const AppConfig({required this.apiBaseUrl, this.errorMessage});

  final Uri? apiBaseUrl;
  final String? errorMessage;

  factory AppConfig.fromEnvironment() {
    const rawBaseUrl = String.fromEnvironment('UNIPM_API_BASE_URL');
    return AppConfig.fromRawBaseUrl(rawBaseUrl, isRelease: kReleaseMode);
  }

  factory AppConfig.fromRawBaseUrl(
    String rawBaseUrl, {
    bool isRelease = kReleaseMode,
  }) {
    if (rawBaseUrl.trim().isEmpty) {
      return const AppConfig(
        apiBaseUrl: null,
        errorMessage:
            'The mobile API URL is not configured. Run the app with --dart-define=UNIPM_API_BASE_URL=<url>.',
      );
    }

    final uri = Uri.tryParse(rawBaseUrl.trim());
    final scheme = uri?.scheme.toLowerCase();
    if (uri == null ||
        !uri.hasScheme ||
        uri.host.isEmpty ||
        (scheme != 'http' && scheme != 'https')) {
      return const AppConfig(
        apiBaseUrl: null,
        errorMessage: 'The configured mobile API URL is invalid.',
      );
    }

    if (isRelease && scheme != 'https') {
      return const AppConfig(
        apiBaseUrl: null,
        errorMessage: 'Release builds require an HTTPS mobile API URL.',
      );
    }

    return AppConfig(apiBaseUrl: uri);
  }
}
