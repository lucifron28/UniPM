import 'dart:async';

import 'package:flutter/material.dart';

import '../auth/session_controller.dart';
import '../features/auth/authenticated_shell.dart';
import '../features/auth/login_page.dart';
import '../features/auth/session_restore_page.dart';
import '../features/auth/unsupported_role_page.dart';

class AppRouter extends StatefulWidget {
  const AppRouter({
    super.key,
    required this.sessionController,
    this.configurationError,
  });

  final SessionController sessionController;
  final String? configurationError;

  @override
  State<AppRouter> createState() => _AppRouterState();
}

class _AppRouterState extends State<AppRouter> {
  @override
  void initState() {
    super.initState();
    unawaited(widget.sessionController.restore());
  }

  @override
  Widget build(BuildContext context) {
    if (widget.configurationError != null) {
      return SessionRestorePage(
        configurationError: widget.configurationError,
      );
    }

    return AnimatedBuilder(
      animation: widget.sessionController,
      builder: (context, _) {
        switch (widget.sessionController.status) {
          case SessionStatus.restoring:
            return const SessionRestorePage();
          case SessionStatus.signedOut:
          case SessionStatus.signingIn:
            return LoginPage(controller: widget.sessionController);
          case SessionStatus.authenticated:
            return AuthenticatedShell(controller: widget.sessionController);
          case SessionStatus.unsupportedRole:
            return UnsupportedRolePage(controller: widget.sessionController);
        }
      },
    );
  }
}
