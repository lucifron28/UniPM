import 'package:flutter/material.dart';

abstract final class AppColors {
  // Brand Anchors
  static const primary = Color(0xFF570000);
  static const primaryActive = Color(0xFF7F1D1D);
  static const primaryStrong = Color(0xFF800000);

  // Surfaces & Backgrounds
  static const pageBackground = Color(0xFFF8F9FA);
  static const sidebarBackground = Color(0xFFFAFAF9);
  static const surfaceMuted = Color(0xFFF3F4F5);
  static const inputSurface = Color(0xFFE1E3E4);
  static const cardBackground = Colors.white;

  // Typography
  static const textPrimary = Color(0xFF191C1D);
  static const textSecondary = Color(0xFF5A413D);
  static const textNeutral = Color(0xFF57534E);

  // Borders & Dividers
  static const borderSoft = Color(0xFFE7E5E4);

  // Semantic Status Tints
  static const success = Color(0xFF15803D);
  static const warning = Color(0xFFB45309);
  static const error = Color(0xFFBA1A1A);
  static const information = Color(0xFF1D4ED8);

  // Pill Badges (Background, Border, Text)
  static const draftBg = Color(0xFFF1F5F9);
  static const draftBorder = Color(0xFFE2E8F0);
  static const draftText = Color(0xFF334155);

  static const submittedBg = Color(0xFFFEF3C7);
  static const submittedBorder = Color(0xFFFDE68A);
  static const submittedText = Color(0xFF92400E);

  static const acknowledgedBg = Color(0xFFD1FAE5);
  static const acknowledgedBorder = Color(0xFFA7F3D0);
  static const acknowledgedText = Color(0xFF065F46);

  static const dueBg = Color(0xFFFFFBEB);
  static const dueBorder = Color(0xFFFDE68A);
  static const dueText = Color(0xFFB45309);

  static const ongoingBg = Color(0xFFEFF6FF);
  static const ongoingBorder = Color(0xFFBFDBFE);
  static const ongoingText = Color(0xFF1E40AF);
}
