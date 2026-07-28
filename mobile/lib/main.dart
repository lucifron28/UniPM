import 'package:flutter/material.dart';

import 'api/api_client.dart';
import 'auth/auth_repository.dart';
import 'auth/session_controller.dart';
import 'config/app_config.dart';
import 'routing/app_router.dart';
import 'storage/secure_session_store.dart';

void main() {
  final config = AppConfig.fromEnvironment();
  final cookieStore = SecureSessionStore();
  final apiClient = ApiClient(
    baseUrl: config.apiBaseUrl,
    cookieStore: cookieStore,
  );
  final authRepository = AuthRepository(apiClient);
  final sessionController = SessionController(authRepository, cookieStore);

  apiClient.configureSession(
    accessTokenProvider: () => sessionController.accessToken,
    refreshHandler: sessionController.refreshForRequest,
  );

  runApp(
    UniPmApp(
      sessionController: sessionController,
      configurationError: config.errorMessage,
    ),
  );
}

class UniPmApp extends StatelessWidget {
  const UniPmApp({
    super.key,
    required this.sessionController,
    this.configurationError,
  });

  final SessionController sessionController;
  final String? configurationError;

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'UniPM Mobile',
      theme: ThemeData(
        colorScheme: ColorScheme.fromSeed(seedColor: Colors.indigo),
        useMaterial3: true,
      ),
      home: AppRouter(
        sessionController: sessionController,
        configurationError: configurationError,
      ),
    );
  }
}
