import 'package:flutter/material.dart';

import 'api/api_client.dart';
import 'auth/auth_repository.dart';
import 'auth/session_controller.dart';
import 'config/app_config.dart';
import 'features/preventive_maintenance/preventive_maintenance_repository.dart';
import 'routing/app_router.dart';

void main() {
  final config = AppConfig.fromEnvironment();
  final apiClient = ApiClient(baseUrl: config.apiBaseUrl);
  final authRepository = AuthRepository(apiClient);
  final preventiveMaintenanceRepository = ApiPreventiveMaintenanceRepository(
    apiClient,
  );
  final sessionController = SessionController(authRepository);

  apiClient.configureSession(
    accessTokenProvider: () => sessionController.accessToken,
    terminalAuthFailureHandler:
        sessionController.handleTerminalAuthenticationFailure,
  );

  runApp(
    UniPmApp(
      sessionController: sessionController,
      preventiveMaintenanceRepository: preventiveMaintenanceRepository,
      configurationError: config.errorMessage,
    ),
  );
}

class UniPmApp extends StatelessWidget {
  const UniPmApp({
    super.key,
    required this.sessionController,
    this.preventiveMaintenanceRepository,
    this.configurationError,
  });

  final SessionController sessionController;
  final PreventiveMaintenanceRepository? preventiveMaintenanceRepository;
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
        preventiveMaintenanceRepository: preventiveMaintenanceRepository,
        configurationError: configurationError,
      ),
    );
  }
}
