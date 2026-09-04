import 'dart:async';

import 'package:flutter/material.dart';

import '../../ui/display_labels.dart';
import '../assets/asset_models.dart';
import 'asset_maintenance_history_controller.dart';
import 'asset_maintenance_history_models.dart';
import 'asset_maintenance_history_repository.dart';

class AssetMaintenanceHistoryPage extends StatefulWidget {
  const AssetMaintenanceHistoryPage({
    super.key,
    required this.asset,
    required this.repository,
    this.controller,
  });

  final Asset asset;
  final AssetMaintenanceHistoryRepository repository;
  final AssetMaintenanceHistoryController? controller;

  @override
  State<AssetMaintenanceHistoryPage> createState() =>
      _AssetMaintenanceHistoryPageState();
}

class _AssetMaintenanceHistoryPageState
    extends State<AssetMaintenanceHistoryPage> {
  late final AssetMaintenanceHistoryController controller =
      widget.controller ??
      AssetMaintenanceHistoryController(
        repository: widget.repository,
        assetId: widget.asset.id,
      );
  late final bool ownsController = widget.controller == null;

  @override
  void initState() {
    super.initState();
    unawaited(controller.load());
  }

  @override
  void dispose() {
    if (ownsController) controller.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Maintenance history')),
      body: SafeArea(
        child: AnimatedBuilder(
          animation: controller,
          builder: (context, _) {
            return switch (controller.status) {
              AssetMaintenanceHistoryStatus.idle ||
              AssetMaintenanceHistoryStatus.loading => const _HistoryLoading(),
              AssetMaintenanceHistoryStatus.failure => _HistoryError(
                message: controller.errorMessage!,
                onRetry: controller.load,
              ),
              AssetMaintenanceHistoryStatus.success => _HistoryContent(
                asset: widget.asset,
                records: controller.records,
              ),
            };
          },
        ),
      ),
    );
  }
}

class _HistoryLoading extends StatelessWidget {
  const _HistoryLoading();

  @override
  Widget build(BuildContext context) {
    return const Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          CircularProgressIndicator(),
          SizedBox(height: 16),
          Text('Loading maintenance history...'),
        ],
      ),
    );
  }
}

class _HistoryError extends StatelessWidget {
  const _HistoryError({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: SingleChildScrollView(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(
              Icons.error_outline,
              size: 64,
              color: Theme.of(context).colorScheme.error,
            ),
            const SizedBox(height: 16),
            Text(
              'Unable to load maintenance history',
              key: const Key('asset-history-error-title'),
              style: Theme.of(context).textTheme.headlineSmall,
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: 8),
            Text(
              message,
              key: const Key('asset-history-error'),
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: 24),
            FilledButton(
              key: const Key('retry-asset-history'),
              onPressed: onRetry,
              child: const Text('Retry'),
            ),
          ],
        ),
      ),
    );
  }
}

class _HistoryContent extends StatelessWidget {
  const _HistoryContent({required this.asset, required this.records});

  final Asset asset;
  final List<AssetMaintenanceHistoryRecord> records;

  @override
  Widget build(BuildContext context) {
    return ListView(
      key: const Key('asset-history-list'),
      padding: const EdgeInsets.all(24),
      children: [
        Text(asset.assetCode, style: Theme.of(context).textTheme.headlineSmall),
        const SizedBox(height: 4),
        Text(displayAssetCategory(asset.assetCategory)),
        const SizedBox(height: 12),
        const Text('Official history contains acknowledged records only.'),
        const SizedBox(height: 24),
        if (records.isEmpty)
          const Card(
            child: Padding(
              padding: EdgeInsets.all(20),
              child: Text(
                'No acknowledged maintenance history has been recorded for this asset.',
                key: Key('asset-history-empty'),
              ),
            ),
          )
        else
          ...records.map(
            (record) => Padding(
              padding: const EdgeInsets.only(bottom: 16),
              child: _HistoryRecordCard(record: record),
            ),
          ),
      ],
    );
  }
}

class _HistoryRecordCard extends StatelessWidget {
  const _HistoryRecordCard({required this.record});

  final AssetMaintenanceHistoryRecord record;

  @override
  Widget build(BuildContext context) {
    return Card(
      key: Key('asset-history-record-${record.id}'),
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(
                  child: Text(
                    _dateText(record.dateInspected),
                    style: Theme.of(context).textTheme.titleLarge,
                  ),
                ),
                Text(
                  record.isOperational ? 'Operational' : 'Non-operational',
                  key: Key('asset-history-result-${record.id}'),
                  style: TextStyle(
                    color: record.isOperational
                        ? Theme.of(context).colorScheme.primary
                        : Theme.of(context).colorScheme.error,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ],
            ),
            const SizedBox(height: 16),
            _HistoryField(label: 'PM inspection reference', value: record.id),
            _HistoryField(
              label: 'Remarks',
              value: _displayValue(record.remarks),
            ),
            _HistoryField(
              label: 'Recommendations',
              value: _displayValue(record.actionsRecommendations),
            ),
            const SizedBox(height: 4),
            Text(
              'Acknowledged official record',
              style: Theme.of(context).textTheme.labelMedium,
            ),
          ],
        ),
      ),
    );
  }
}

class _HistoryField extends StatelessWidget {
  const _HistoryField({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: Theme.of(context).textTheme.labelMedium),
          const SizedBox(height: 2),
          Text(value),
        ],
      ),
    );
  }
}

String _displayValue(String? value) {
  final normalized = value?.trim();
  return normalized == null || normalized.isEmpty ? 'Not recorded' : normalized;
}

String _dateText(DateTime value) {
  final local = value.toLocal();
  final month = local.month.toString().padLeft(2, '0');
  final day = local.day.toString().padLeft(2, '0');
  return '${local.year}-$month-$day';
}
