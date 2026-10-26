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
              onSignOut: widget.sessionController.logout,
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
    required this.onSignOut,
    required this.child,
  });

  final OfflineInspectionRepository? repository;
  final String userId;
  final Future<void> Function() onSignOut;
  final Widget child;

  @override
  State<_OfflineSessionScope> createState() => _OfflineSessionScopeState();
}

class _OfflineSessionScopeState extends State<_OfflineSessionScope> {
  bool _isReady = false;
  bool _hasError = false;
  String? _errorMessage;
  int _generation = 0;

  @override
  void initState() {
    super.initState();
    unawaited(_activate());
  }

  @override
  void didUpdateWidget(covariant _OfflineSessionScope oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.repository != widget.repository ||
        oldWidget.userId != widget.userId) {
      unawaited(_switchScope(oldWidget));
    }
  }

  Future<void> _switchScope(_OfflineSessionScope oldWidget) async {
    final generation = ++_generation;
    _isReady = false;
    _hasError = false;
    _errorMessage = null;
    try {
      await oldWidget.repository?.deactivate(lockOnFailure: true);
    } catch (_) {
      if (!mounted || generation != _generation) return;
      setState(() {
        _hasError = true;
        _errorMessage =
            'Offline changes from the previous account could not be secured. '
            'This account is locked. Sign out and sign in with the account '
            'that owns those changes to recover them.';
      });
      return;
    }
    await _activate(generation: generation);
  }

  Future<void> _activate({int? generation}) async {
    final currentGeneration = generation ?? ++_generation;
    final repository = widget.repository;
    if (repository != null) await repository.activate(widget.userId);
    if (!mounted || currentGeneration != _generation) return;
    final isReady = repository == null || repository.isActiveFor(widget.userId);
    setState(() {
      _isReady = isReady;
      _hasError = !isReady;
      _errorMessage = isReady
          ? null
          : repository.lastIssue ??
                'Offline account data could not be opened. Retry or sign out.';
    });
  }

  Future<void> _deactivateSafely() async {
    try {
      await widget.repository?.deactivate(lockOnFailure: true);
    } catch (_) {
      // Keep the owner's data in memory, but lock it from a later account.
    }
  }

  @override
  void dispose() {
    _generation++;
    unawaited(_deactivateSafely());
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    if (_isReady) return widget.child;

    return Scaffold(
      appBar: AppBar(title: const Text('Offline account data')),
      body: Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              if (!_hasError) const CircularProgressIndicator(),
              if (_hasError) ...[
                Text(
                  _errorMessage ?? 'Offline account data could not be opened.',
                  textAlign: TextAlign.center,
                ),
                const SizedBox(height: 16),
                FilledButton(
                  onPressed: () => unawaited(_activate()),
                  child: const Text('Retry'),
                ),
                const SizedBox(height: 8),
                TextButton(
                  onPressed: widget.onSignOut,
                  child: const Text('Sign out'),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}
