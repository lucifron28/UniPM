import 'package:flutter/material.dart';

import '../auth/session_controller.dart';
import '../features/assets/asset_repository.dart';
import '../features/auth/authenticated_shell.dart';
import '../features/auth/configuration_error_page.dart';
import '../features/auth/login_page.dart';
import '../features/auth/unsupported_role_page.dart';
import '../features/preventive_maintenance/preventive_maintenance_repository.dart';

class AppRouter extends StatefulWidget {
  const AppRouter({
    super.key,
    required this.sessionController,
    required this.navigatorKey,
    this.assetRepository,
    this.preventiveMaintenanceRepository,
    this.configurationError,
  });

  final SessionController sessionController;
  final GlobalKey<NavigatorState> navigatorKey;
  final AssetRepository? assetRepository;
  final PreventiveMaintenanceRepository? preventiveMaintenanceRepository;
  final String? configurationError;

  @override
  State<AppRouter> createState() => _AppRouterState();
}

class _AppRouterState extends State<AppRouter> {
  bool _signOutResetScheduled = false;

  @override
  void initState() {
    super.initState();
    widget.sessionController.addListener(_handleSessionChange);
  }

  @override
  void dispose() {
    widget.sessionController.removeListener(_handleSessionChange);
    super.dispose();
  }

  void _handleSessionChange() {
    if (widget.sessionController.status != SessionStatus.signedOut) {
      _signOutResetScheduled = false;
      return;
    }
    if (_signOutResetScheduled) return;
    _signOutResetScheduled = true;

    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      if (widget.sessionController.status != SessionStatus.signedOut) {
        _signOutResetScheduled = false;
        return;
      }
      final navigator = widget.navigatorKey.currentState;
      navigator?.popUntil((route) => route.isFirst);
      _signOutResetScheduled = false;
    });
  }

  @override
  Widget build(BuildContext context) {
    if (widget.configurationError != null) {
      return ConfigurationErrorPage(
        configurationError: widget.configurationError,
      );
    }

    return AnimatedBuilder(
      animation: widget.sessionController,
      builder: (context, _) {
        switch (widget.sessionController.status) {
          case SessionStatus.signedOut:
          case SessionStatus.signingIn:
            return LoginPage(controller: widget.sessionController);
          case SessionStatus.authenticated:
            return AuthenticatedShell(
              controller: widget.sessionController,
              assetRepository: widget.assetRepository,
              preventiveMaintenanceRepository:
                  widget.preventiveMaintenanceRepository,
            );
          case SessionStatus.unsupportedRole:
            return UnsupportedRolePage(controller: widget.sessionController);
        }
      },
    );
  }
}
