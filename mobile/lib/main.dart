import 'package:flutter/material.dart';

import 'api/api_client.dart';
import 'auth/auth_repository.dart';
import 'auth/session_controller.dart';
import 'config/app_config.dart';
import 'features/assets/asset_repository.dart';
import 'features/maintenance_history/asset_maintenance_history_repository.dart';
import 'features/preventive_maintenance/preventive_maintenance_repository.dart';
import 'routing/app_router.dart';

void main() {
  final config = AppConfig.fromEnvironment();
  final apiClient = ApiClient(baseUrl: config.apiBaseUrl);
  final authRepository = AuthRepository(apiClient);
  final assetRepository = ApiAssetRepository(apiClient);
  final preventiveMaintenanceRepository = ApiPreventiveMaintenanceRepository(
    apiClient,
  );
  final assetMaintenanceHistoryRepository =
      ApiAssetMaintenanceHistoryRepository(apiClient);
  final sessionController = SessionController(authRepository);

  apiClient.configureSession(
    accessTokenProvider: () => sessionController.accessToken,
    terminalAuthFailureHandler:
        sessionController.handleTerminalAuthenticationFailure,
  );

  runApp(
    UniPmApp(
      sessionController: sessionController,
      assetRepository: assetRepository,
      preventiveMaintenanceRepository: preventiveMaintenanceRepository,
      assetMaintenanceHistoryRepository: assetMaintenanceHistoryRepository,
      configurationError: config.errorMessage,
    ),
  );
}

class UniPmApp extends StatefulWidget {
  const UniPmApp({
    super.key,
    required this.sessionController,
    this.assetRepository,
    this.preventiveMaintenanceRepository,
    this.assetMaintenanceHistoryRepository,
    this.configurationError,
    this.navigatorKey,
  });

  final SessionController sessionController;
  final AssetRepository? assetRepository;
  final PreventiveMaintenanceRepository? preventiveMaintenanceRepository;
  final AssetMaintenanceHistoryRepository? assetMaintenanceHistoryRepository;
  final String? configurationError;
  final GlobalKey<NavigatorState>? navigatorKey;

  @override
  State<UniPmApp> createState() => _UniPmAppState();
}

class _UniPmAppState extends State<UniPmApp> {
  late final GlobalKey<NavigatorState> _navigatorKey =
      widget.navigatorKey ?? GlobalKey<NavigatorState>();

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      navigatorKey: _navigatorKey,
      title: 'UniPM Mobile',
      theme: ThemeData(
        colorScheme: ColorScheme.fromSeed(seedColor: Colors.indigo),
        useMaterial3: true,
      ),
      home: AppRouter(
        sessionController: widget.sessionController,
        assetRepository: widget.assetRepository,
        preventiveMaintenanceRepository: widget.preventiveMaintenanceRepository,
        assetMaintenanceHistoryRepository:
            widget.assetMaintenanceHistoryRepository,
        configurationError: widget.configurationError,
        navigatorKey: _navigatorKey,
      ),
    );
  }
}
