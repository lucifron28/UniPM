class AppConfig {
  const AppConfig({required this.apiBaseUrl, this.errorMessage});

  final Uri? apiBaseUrl;
  final String? errorMessage;

  factory AppConfig.fromEnvironment() {
    const rawBaseUrl = String.fromEnvironment('UNIPM_API_BASE_URL');
    if (rawBaseUrl.trim().isEmpty) {
      return const AppConfig(
        apiBaseUrl: null,
        errorMessage:
            'The mobile API URL is not configured. Run the app with --dart-define=UNIPM_API_BASE_URL=<url>.',
      );
    }

    final uri = Uri.tryParse(rawBaseUrl.trim());
    if (uri == null || !uri.hasScheme || uri.host.isEmpty) {
      return const AppConfig(
        apiBaseUrl: null,
        errorMessage: 'The configured mobile API URL is invalid.',
      );
    }

    return AppConfig(apiBaseUrl: uri);
  }
}
