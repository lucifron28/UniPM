import 'dart:async';

import 'package:flutter/material.dart';

import '../auth/session_controller.dart';
import '../features/assets/asset_repository.dart';
import '../features/auth/authenticated_shell.dart';
import '../features/auth/configuration_error_page.dart';
import '../features/auth/login_page.dart';
import '../features/auth/unsupported_role_page.dart';
import '../features/maintenance_history/asset_maintenance_history_repository.dart';
import '../features/preventive_maintenance/preventive_maintenance_repository.dart';
import '../features/preventive_maintenance/offline_inspection_repository.dart';

class AppRouter extends StatefulWidget {
  const AppRouter({
    super.key,
    required this.sessionController,
    required this.navigatorKey,
    this.assetRepository,
    this.preventiveMaintenanceRepository,
    this.offlineInspectionRepository,
    this.assetMaintenanceHistoryRepository,
    this.configurationError,
  });

  final SessionController sessionController;
  final GlobalKey<NavigatorState> navigatorKey;
  final AssetRepository? assetRepository;
  final PreventiveMaintenanceRepository? preventiveMaintenanceRepository;
  final OfflineInspectionRepository? offlineInspectionRepository;
  final AssetMaintenanceHistoryRepository? assetMaintenanceHistoryRepository;
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
            final user = widget.sessionController.user!;
            return _OfflineSessionScope(
              repository: widget.offlineInspectionRepository,
              userId: user.id,
              child: AuthenticatedShell(
                controller: widget.sessionController,
                assetRepository: widget.assetRepository,
                preventiveMaintenanceRepository:
                    widget.preventiveMaintenanceRepository,
                offlineInspectionRepository: widget.offlineInspectionRepository,
                assetMaintenanceHistoryRepository:
                    widget.assetMaintenanceHistoryRepository,
              ),
            );
          case SessionStatus.unsupportedRole:
            return UnsupportedRolePage(controller: widget.sessionController);
        }
      },
    );
  }
}

class _OfflineSessionScope extends StatefulWidget {
  const _OfflineSessionScope({
    required this.repository,
    required this.userId,
    required this.child,
  });

  final OfflineInspectionRepository? repository;
  final String userId;
  final Widget child;

  @override
  State<_OfflineSessionScope> createState() => _OfflineSessionScopeState();
}

class _OfflineSessionScopeState extends State<_OfflineSessionScope> {
  @override
  void initState() {
    super.initState();
    _activate();
  }

  @override
  void didUpdateWidget(covariant _OfflineSessionScope oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.repository != widget.repository ||
        oldWidget.userId != widget.userId) {
      unawaited(_switchScope());
    }
  }

  Future<void> _switchScope() async {
    await widget.repository?.deactivate();
    await widget.repository?.activate(widget.userId);
  }

  void _activate() {
    final repository = widget.repository;
    if (repository != null) unawaited(repository.activate(widget.userId));
  }

  Future<void> _deactivateSafely() async {
    try {
      await widget.repository?.deactivate();
    } catch (_) {
      // The authenticated scope is discarded; the encrypted snapshot remains.
    }
  }

  @override
  void dispose() {
    unawaited(_deactivateSafely());
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => widget.child;
}
