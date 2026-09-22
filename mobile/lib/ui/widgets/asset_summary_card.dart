import 'package:flutter/material.dart';
import '../app_colors.dart';
import '../display_labels.dart';

class AssetSummaryCard extends StatelessWidget {
  const AssetSummaryCard({
    super.key,
    required this.assetCode,
    required this.assetCategory,
    this.building,
    this.department,
    this.location,
    this.status = 'Active',
    this.onTap,
  });

  final String assetCode;
  final String assetCategory;
  final String? building;
  final String? department;
  final String? location;
  final String status;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    final locationText = [
      if (building != null && building!.isNotEmpty) building,
      if (department != null && department!.isNotEmpty) department,
      if (location != null && location!.isNotEmpty) location,
    ].join(' • ');

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
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          assetCode,
                          style: const TextStyle(
                            fontSize: 18,
                            fontWeight: FontWeight.w800,
                            letterSpacing: -0.3,
                            color: AppColors.textPrimary,
                          ),
                        ),
                        const SizedBox(height: 4),
                        Text(
                          displayAssetCategory(assetCategory),
                          style: const TextStyle(
                            fontSize: 14,
                            fontWeight: FontWeight.w600,
                            color: AppColors.textSecondary,
                          ),
                        ),
                      ],
                    ),
                  ),
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                    decoration: BoxDecoration(
                      color: AppColors.acknowledgedBg,
                      borderRadius: BorderRadius.circular(999),
                      border: Border.all(color: AppColors.acknowledgedBorder),
                    ),
                    child: Text(
                      status,
                      style: const TextStyle(
                        fontSize: 11,
                        fontWeight: FontWeight.w700,
                        color: AppColors.acknowledgedText,
                      ),
                    ),
                  ),
                ],
              ),
              if (locationText.isNotEmpty) ...[
                const SizedBox(height: 12),
                Row(
                  children: [
                    const Icon(
                      Icons.place_outlined,
                      size: 16,
                      color: AppColors.textNeutral,
                    ),
                    const SizedBox(width: 6),
                    Expanded(
                      child: Text(
                        locationText,
                        style: const TextStyle(
                          fontSize: 13,
                          color: AppColors.textNeutral,
                        ),
                        maxLines: 2,
                        overflow: TextOverflow.ellipsis,
                      ),
                    ),
                  ],
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}
