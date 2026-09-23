import 'package:flutter/material.dart';
import '../app_colors.dart';

enum StatusBadgeVariant {
  draft,
  submitted,
  acknowledged,
  due,
  ongoing,
  completed,
  operational,
  nonOperational,
  overdue,
  active,
  inactive,
  retired,
  neutral,
}

class StatusBadge extends StatelessWidget {
  const StatusBadge({
    super.key,
    required this.label,
    required this.variant,
    this.icon,
  });

  final String label;
  final StatusBadgeVariant variant;
  final IconData? icon;

  factory StatusBadge.fromFormStatus(String status) {
    return switch (status.trim().toLowerCase()) {
      'draft' => const StatusBadge(
        label: 'Draft',
        variant: StatusBadgeVariant.draft,
      ),
      'submitted' => const StatusBadge(
        label: 'Awaiting acknowledgement',
        variant: StatusBadgeVariant.submitted,
      ),
      'acknowledged' => const StatusBadge(
        label: 'Acknowledged',
        variant: StatusBadgeVariant.acknowledged,
      ),
      _ => StatusBadge(label: status, variant: StatusBadgeVariant.neutral),
    };
  }

  factory StatusBadge.fromScheduleStatus(String status) {
    return switch (status.trim().toLowerCase()) {
      'due' => const StatusBadge(label: 'Due', variant: StatusBadgeVariant.due),
      'ongoing' => const StatusBadge(
        label: 'Ongoing',
        variant: StatusBadgeVariant.ongoing,
      ),
      'completed' => const StatusBadge(
        label: 'Completed',
        variant: StatusBadgeVariant.completed,
      ),
      'overdue' => const StatusBadge(
        label: 'Overdue',
        variant: StatusBadgeVariant.overdue,
      ),
      _ => StatusBadge(label: status, variant: StatusBadgeVariant.neutral),
    };
  }

  factory StatusBadge.fromAssetCondition(String condition) {
    return switch (condition.trim().toLowerCase()) {
      'operational' => const StatusBadge(
        label: 'Operational',
        variant: StatusBadgeVariant.operational,
        icon: Icons.check_circle_outline,
      ),
      'non-operational' || 'nonoperational' => const StatusBadge(
        label: 'Non-operational',
        variant: StatusBadgeVariant.nonOperational,
        icon: Icons.error_outline,
      ),
      _ => StatusBadge(label: condition, variant: StatusBadgeVariant.neutral),
    };
  }

  factory StatusBadge.fromAssetStatus(String status) {
    return switch (status.trim().toLowerCase()) {
      'active' => const StatusBadge(
        label: 'Active',
        variant: StatusBadgeVariant.active,
      ),
      'inactive' => const StatusBadge(
        label: 'Inactive',
        variant: StatusBadgeVariant.inactive,
      ),
      'retired' => const StatusBadge(
        label: 'Retired',
        variant: StatusBadgeVariant.retired,
      ),
      _ => StatusBadge(label: status, variant: StatusBadgeVariant.neutral),
    };
  }

  @override
  Widget build(BuildContext context) {
    final (bg, border, text) = switch (variant) {
      StatusBadgeVariant.draft => (
        AppColors.draftBg,
        AppColors.draftBorder,
        AppColors.draftText,
      ),
      StatusBadgeVariant.submitted => (
        AppColors.submittedBg,
        AppColors.submittedBorder,
        AppColors.submittedText,
      ),
      StatusBadgeVariant.acknowledged ||
      StatusBadgeVariant.completed ||
      StatusBadgeVariant.operational ||
      StatusBadgeVariant.active => (
        AppColors.acknowledgedBg,
        AppColors.acknowledgedBorder,
        AppColors.acknowledgedText,
      ),
      StatusBadgeVariant.due => (
        AppColors.dueBg,
        AppColors.dueBorder,
        AppColors.dueText,
      ),
      StatusBadgeVariant.ongoing => (
        AppColors.ongoingBg,
        AppColors.ongoingBorder,
        AppColors.ongoingText,
      ),
      StatusBadgeVariant.overdue || StatusBadgeVariant.nonOperational => (
        const Color(0xFFFEE2E2),
        const Color(0xFFFECACA),
        AppColors.error,
      ),
      StatusBadgeVariant.inactive ||
      StatusBadgeVariant.retired ||
      StatusBadgeVariant.neutral => (
        AppColors.surfaceMuted,
        AppColors.borderSoft,
        AppColors.textNeutral,
      ),
    };

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
      decoration: BoxDecoration(
        color: bg,
        borderRadius: BorderRadius.circular(9999),
        border: Border.all(color: border, width: 1),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (icon != null) ...[
            Icon(icon, size: 14, color: text),
            const SizedBox(width: 4),
          ],
          Text(
            label,
            style: TextStyle(
              fontSize: 12,
              fontWeight: FontWeight.w600,
              color: text,
              letterSpacing: -0.2,
            ),
          ),
        ],
      ),
    );
  }
}
