import 'package:flutter/material.dart';

import '../../ui/app_colors.dart';
import '../../ui/display_labels.dart';
import '../../ui/widgets/pm_progress_indicator.dart';
import 'pm_cycle_presentation.dart';

class InspectionCompletionSheet extends StatelessWidget {
  const InspectionCompletionSheet({
    super.key,
    required this.assetCode,
    required this.department,
    required this.assetCategory,
    required this.pmCycle,
    required this.completedCount,
    required this.totalCount,
    this.isCreatePending = false,
    required this.onNextAsset,
    required this.onViewBatch,
  });

  final String assetCode;
  final String department;
  final String assetCategory;
  final String pmCycle;
  final int completedCount;
  final int totalCount;
  final bool isCreatePending;
  final VoidCallback onNextAsset;
  final VoidCallback onViewBatch;

  static Future<void> show(
    BuildContext context, {
    required String assetCode,
    required String department,
    required String assetCategory,
    required String pmCycle,
    required int completedCount,
    required int totalCount,
    bool isCreatePending = false,
    required VoidCallback onNextAsset,
    required VoidCallback onViewBatch,
  }) {
    return showModalBottomSheet<void>(
      context: context,
      isScrollControlled: true,
      showDragHandle: true,
      backgroundColor: Colors.white,
      shape: const RoundedRectangleBorder(
        borderRadius: BorderRadius.vertical(top: Radius.circular(20)),
      ),
      builder: (_) => InspectionCompletionSheet(
        assetCode: assetCode,
        department: department,
        assetCategory: assetCategory,
        pmCycle: pmCycle,
        completedCount: completedCount,
        totalCount: totalCount,
        isCreatePending: isCreatePending,
        onNextAsset: onNextAsset,
        onViewBatch: onViewBatch,
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final title = isCreatePending
        ? 'Saved on this device'
        : 'Inspection Recorded';
    final message = isCreatePending
        ? 'Asset $assetCode is saved on this device. The server records completion at sync, so late syncs can be classified as late.'
        : 'Asset $assetCode successfully inspected.';
    return SafeArea(
      child: Padding(
        padding: const EdgeInsets.fromLTRB(24, 8, 24, 24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              children: [
                Container(
                  padding: const EdgeInsets.all(10),
                  decoration: const BoxDecoration(
                    color: AppColors.acknowledgedBg,
                    shape: BoxShape.circle,
                  ),
                  child: const Icon(
                    Icons.check_circle,
                    color: AppColors.acknowledgedText,
                    size: 32,
                  ),
                ),
                const SizedBox(width: 14),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        title,
                        style: const TextStyle(
                          fontSize: 18,
                          fontWeight: FontWeight.w800,
                          color: AppColors.textPrimary,
                        ),
                      ),
                      const SizedBox(height: 2),
                      Text(
                        message,
                        style: const TextStyle(
                          fontSize: 13,
                          color: AppColors.textSecondary,
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
            const SizedBox(height: 20),
            Container(
              padding: const EdgeInsets.all(16),
              decoration: BoxDecoration(
                color: AppColors.pageBackground,
                borderRadius: BorderRadius.circular(12),
                border: Border.all(color: AppColors.borderSoft),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Expanded(
                        child: Text(
                          displayAssetCategory(assetCategory),
                          style: const TextStyle(
                            fontSize: 14,
                            fontWeight: FontWeight.w700,
                            color: AppColors.textPrimary,
                          ),
                        ),
                      ),
                      Flexible(
                        child: Text(
                          'PM cycle: ${formatPmCycle(pmCycle)}',
                          textAlign: TextAlign.end,
                          style: const TextStyle(
                            fontSize: 12,
                            fontWeight: FontWeight.w600,
                            color: AppColors.textNeutral,
                          ),
                        ),
                      ),
                    ],
                  ),
                  const SizedBox(height: 4),
                  Text(
                    'Department: $department',
                    style: const TextStyle(
                      fontSize: 13,
                      color: AppColors.textSecondary,
                    ),
                  ),
                  const SizedBox(height: 4),
                  Text('Due date: ${formatPmCycleDueDate(pmCycle)}'),
                  const SizedBox(height: 14),
                  PmProgressIndicator(
                    completed: completedCount,
                    total: totalCount > 0 ? totalCount : completedCount,
                  ),
                ],
              ),
            ),
            const SizedBox(height: 24),
            FilledButton.icon(
              key: const Key('sheet-next-asset'),
              onPressed: onNextAsset,
              icon: const Icon(Icons.qr_code_scanner),
              label: const Text('Next Asset'),
            ),
            const SizedBox(height: 10),
            OutlinedButton(
              key: const Key('sheet-view-batch'),
              onPressed: onViewBatch,
              child: const Text('View Batch'),
            ),
          ],
        ),
      ),
    );
  }
}
