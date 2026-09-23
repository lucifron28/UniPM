import 'package:flutter/material.dart';
import '../app_colors.dart';
import '../display_labels.dart';
import 'status_badge.dart';
import 'pm_progress_indicator.dart';

class BatchPmCard extends StatelessWidget {
  const BatchPmCard({
    super.key,
    required this.department,
    required this.assetCategory,
    required this.pmCycle,
    required this.status,
    required this.completedCount,
    required this.totalCount,
    this.building,
    this.onTap,
    this.actionLabel,
    this.onAction,
  });

  final String department;
  final String assetCategory;
  final String pmCycle;
  final String status;
  final int completedCount;
  final int totalCount;
  final String? building;
  final VoidCallback? onTap;
  final String? actionLabel;
  final VoidCallback? onAction;

  bool get isCompleted => totalCount > 0 && completedCount >= totalCount;
  bool get isAwaitingAck => status.toLowerCase() == 'submitted';

  @override
  Widget build(BuildContext context) {
    return Card(
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(12),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          displayAssetCategory(assetCategory),
                          style: const TextStyle(
                            fontSize: 16,
                            fontWeight: FontWeight.w700,
                            letterSpacing: -0.2,
                            color: AppColors.textPrimary,
                          ),
                        ),
                        const SizedBox(height: 2),
                        Text(
                          department,
                          style: const TextStyle(
                            fontSize: 14,
                            fontWeight: FontWeight.w500,
                            color: AppColors.textSecondary,
                          ),
                        ),
                      ],
                    ),
                  ),
                  StatusBadge.fromFormStatus(status),
                ],
              ),
              const SizedBox(height: 12),
              Row(
                children: [
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 2),
                    decoration: BoxDecoration(
                      color: AppColors.surfaceMuted,
                      borderRadius: BorderRadius.circular(6),
                    ),
                    child: Text(
                      'Cycle: $pmCycle',
                      style: const TextStyle(
                        fontSize: 12,
                        fontWeight: FontWeight.w600,
                        color: AppColors.textNeutral,
                      ),
                    ),
                  ),
                  if (building != null && building!.isNotEmpty) ...[
                    const SizedBox(width: 8),
                    Expanded(
                      child: Text(
                        building!,
                        style: const TextStyle(
                          fontSize: 12,
                          color: AppColors.textNeutral,
                        ),
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                  ],
                ],
              ),
              const SizedBox(height: 14),
              PmProgressIndicator(
                completed: completedCount,
                total: totalCount > 0 ? totalCount : completedCount,
              ),
              if (actionLabel != null && onAction != null) ...[
                const SizedBox(height: 16),
                SizedBox(
                  width: double.infinity,
                  child: FilledButton(
                    onPressed: onAction,
                    style: FilledButton.styleFrom(
                      backgroundColor: isAwaitingAck
                          ? AppColors.warning
                          : AppColors.primary,
                    ),
                    child: Text(actionLabel!),
                  ),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}
