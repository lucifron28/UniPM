import 'package:flutter/material.dart';

import '../../auth/auth_models.dart';
import '../../ui/app_colors.dart';
import '../../ui/widgets/batch_pm_card.dart';
import '../preventive_maintenance/preventive_maintenance_models.dart';
import '../preventive_maintenance/preventive_maintenance_repository.dart';

class HomePage extends StatefulWidget {
  const HomePage({
    super.key,
    required this.user,
    this.onScanQr,
    this.onEnterAssetCode,
    this.onSearchAssets,
    this.onStartBatch,
    this.onOpenPreventiveMaintenance,
    this.preventiveMaintenanceRepository,
    this.onOpenForm,
    this.onOpenAcknowledgement,
  });

  final AuthUser user;
  final VoidCallback? onScanQr;
  final VoidCallback? onEnterAssetCode;
  final VoidCallback? onSearchAssets;
  final ValueChanged<PmBatchScope>? onStartBatch;
  final VoidCallback? onOpenPreventiveMaintenance;
  final PreventiveMaintenanceRepository? preventiveMaintenanceRepository;
  final ValueChanged<String>? onOpenForm;
  final ValueChanged<PreventiveMaintenanceForm>? onOpenAcknowledgement;

  @override
  State<HomePage> createState() => _HomePageState();
}

class _HomePageState extends State<HomePage> {
  List<PreventiveMaintenanceForm> _forms = [];
  List<ScheduleOption> _schedules = [];
  bool _isLoadingBatches = false;
  String? _batchError;

  bool get _isGsd => widget.user.roles.contains('GSD');

  @override
  void initState() {
    super.initState();
    _loadBatches();
  }

  Future<void> _loadBatches() async {
    final repo = widget.preventiveMaintenanceRepository;
    if (repo == null) return;

    setState(() {
      _isLoadingBatches = true;
      _batchError = null;
    });

    try {
      final results = await Future.wait([
        repo.listForms(),
        repo.listSchedules(),
      ]);
      if (!mounted) return;
      setState(() {
        _forms = results[0] as List<PreventiveMaintenanceForm>;
        _schedules = results[1] as List<ScheduleOption>;
        _isLoadingBatches = false;
      });
    } catch (e) {
      if (!mounted) return;
      setState(() {
        _isLoadingBatches = false;
        _batchError = 'Could not load active batches.';
      });
    }
  }

  List<PreventiveMaintenanceForm> get _activeDrafts {
    return _forms
        .where((f) => f.isDraft)
        .where((f) => _isGsd || f.createdByUserId == widget.user.id)
        .toList(growable: false);
  }

  List<PreventiveMaintenanceForm> get _awaitingAck {
    return _forms
        .where((f) => f.status == 'Submitted')
        .where((f) => _isGsd || f.createdByUserId == widget.user.id)
        .toList(growable: false);
  }

  static const _activeScheduleStatuses = {'due', 'ongoing', 'overdue'};

  List<ScheduleOption> get _assignedSchedules {
    return _schedules
        .where((s) {
          if (s.status.toLowerCase() == 'cancelled') return false;
          final isAssigned = _isGsd || s.assignedToUserId == widget.user.id;
          final isActiveStatus = _activeScheduleStatuses.contains(
            s.status.trim().toLowerCase(),
          );
          return isAssigned && isActiveStatus;
        })
        .toList(growable: false);
  }

  String _resolvePmCycle({
    String? pmCycle,
    required String periodType,
    int? year,
  }) {
    if (pmCycle != null && pmCycle.trim().isNotEmpty) {
      return pmCycle.trim();
    }
    final yearStr = year != null ? ' $year' : '';
    return '$periodType$yearStr'.trim();
  }

  String _canonicalBatchKey(
    String? department,
    String assetCategory,
    String? pmCycle,
  ) {
    final dept = (department ?? '').trim().toLowerCase();
    final cat = assetCategory.trim().toLowerCase();
    final cycle = (pmCycle ?? '').trim().toLowerCase();
    return '$dept|$cat|$cycle';
  }

  int _calculateTotalForBatch({
    required String? department,
    required String assetCategory,
    required String? pmCycle,
  }) {
    final matching = _schedules
        .where((s) {
          if (s.status.toLowerCase() == 'cancelled') return false;
          final asset = s.asset;
          if (asset == null) return false;
          final deptMatch =
              department == null ||
              asset.department?.toLowerCase() == department.toLowerCase();
          final catMatch =
              asset.assetCategory.toLowerCase() == assetCategory.toLowerCase();
          final cycleMatch =
              pmCycle == null ||
              s.pmCycle?.toLowerCase() == pmCycle.toLowerCase();
          return deptMatch && catMatch && cycleMatch;
        })
        .toList(growable: false);
    return matching.length;
  }

  int _calculateTotalForDraft(PreventiveMaintenanceForm form) {
    if (_schedules.isEmpty) return form.inspections.length;
    final total = _calculateTotalForBatch(
      department: form.department,
      assetCategory: form.assetCategory,
      pmCycle: form.pmCycle,
    );
    return total > 0 ? total : form.inspections.length;
  }

  List<_AssignedPmBatch> _groupSchedules(List<ScheduleOption> schedsList) {
    if (schedsList.isEmpty) return const [];

    final groups = <String, List<ScheduleOption>>{};
    for (final s in schedsList) {
      final dept = s.asset?.department ?? 'General Department';
      final cat = s.asset?.assetCategory ?? '';
      final cycle = _resolvePmCycle(
        pmCycle: s.pmCycle,
        periodType: s.periodType,
        year: s.year,
      );
      final key = _canonicalBatchKey(dept, cat, cycle);
      groups.putIfAbsent(key, () => []).add(s);
    }

    final batches = <_AssignedPmBatch>[];
    for (final entry in groups.entries) {
      final scheds = entry.value;
      final first = scheds.first;
      final dept = first.asset?.department ?? 'General Department';
      final cat = first.asset?.assetCategory ?? '';
      final cycle = _resolvePmCycle(
        pmCycle: first.pmCycle,
        periodType: first.periodType,
        year: first.year,
      );

      final hasOverdue = scheds.any(
        (s) => s.status.trim().toLowerCase() == 'overdue',
      );
      final hasOngoing = scheds.any(
        (s) => s.status.trim().toLowerCase() == 'ongoing',
      );
      final status = hasOverdue ? 'Overdue' : (hasOngoing ? 'Ongoing' : 'Due');

      final total = _calculateTotalForBatch(
        department: first.asset?.department,
        assetCategory: cat,
        pmCycle: first.pmCycle,
      );

      batches.add(
        _AssignedPmBatch(
          department: dept,
          assetCategory: cat,
          pmCycle: cycle,
          status: status,
          totalCount: total > 0 ? total : scheds.length,
          building: first.asset?.building,
        ),
      );
    }

    return batches;
  }

  List<_AssignedPmBatch> get _assignedBatches =>
      _groupSchedules(_assignedSchedules);
  List<_AssignedPmBatch> get _unstartedAssignedBatches {
    final draftKeys = _activeDrafts.map((d) {
      return _canonicalBatchKey(
        d.department,
        d.assetCategory,
        _resolvePmCycle(
          pmCycle: d.pmCycle,
          periodType: d.periodType,
          year: d.year,
        ),
      );
    }).toSet();
    return _assignedBatches
        .where((b) {
          final key = _canonicalBatchKey(
            b.department,
            b.assetCategory,
            b.pmCycle,
          );
          return !draftKeys.contains(key);
        })
        .toList(growable: false);
  }

  void _startBatch(_AssignedPmBatch batch) {
    final callback = widget.onStartBatch;
    if (callback != null) {
      callback(
        PmBatchScope(
          department: batch.department,
          assetCategory: batch.assetCategory,
          pmCycle: batch.pmCycle,
        ),
      );
    } else {
      widget.onScanQr?.call();
    }
  }

  @override
  Widget build(BuildContext context) {
    return RefreshIndicator(
      onRefresh: _loadBatches,
      child: ListView(
        padding: const EdgeInsets.all(20),
        children: [
          // Greeting & User Identity
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Welcome, ${widget.user.displayName}',
                      style: const TextStyle(
                        fontSize: 22,
                        fontWeight: FontWeight.w800,
                        letterSpacing: -0.5,
                        color: AppColors.textPrimary,
                      ),
                    ),
                    const SizedBox(height: 4),
                    const Text(
                      'Your field-work session is ready.',
                      style: TextStyle(
                        fontSize: 14,
                        color: AppColors.textSecondary,
                      ),
                    ),
                  ],
                ),
              ),
              Container(
                padding: const EdgeInsets.symmetric(
                  horizontal: 10,
                  vertical: 4,
                ),
                decoration: BoxDecoration(
                  color: AppColors.surfaceMuted,
                  borderRadius: BorderRadius.circular(999),
                  border: Border.all(color: AppColors.borderSoft),
                ),
                child: Text(
                  widget.user.roles.join(', '),
                  style: const TextStyle(
                    fontSize: 12,
                    fontWeight: FontWeight.w700,
                    color: AppColors.primary,
                  ),
                ),
              ),
            ],
          ),
          const SizedBox(height: 24),

          // Asset Identification Hero
          const Text(
            'Identify Asset',
            style: TextStyle(
              fontSize: 15,
              fontWeight: FontWeight.w700,
              color: AppColors.textPrimary,
            ),
          ),
          const SizedBox(height: 12),
          Row(
            children: [
              Expanded(
                child: _QuickActionCard(
                  key: const Key('scan-asset-qr'),
                  icon: Icons.qr_code_scanner,
                  title: 'Scan QR',
                  subtitle: 'Fastest path',
                  isPrimary: true,
                  onTap: widget.onScanQr,
                ),
              ),
              const SizedBox(width: 12),
              Expanded(
                child: _QuickActionCard(
                  key: const Key('enter-asset-code'),
                  icon: Icons.keyboard_alt_outlined,
                  title: 'Enter Code',
                  subtitle: 'Damaged QR',
                  isPrimary: false,
                  onTap: widget.onEnterAssetCode,
                ),
              ),
            ],
          ),
          const SizedBox(height: 12),
          Card(
            child: ListTile(
              key: const Key('search-assets'),
              leading: const Icon(Icons.search, color: AppColors.primary),
              title: const Text(
                'Search Assets',
                style: TextStyle(fontWeight: FontWeight.w600),
              ),
              subtitle: const Text(
                'Find by code, building, department, or room',
                style: TextStyle(fontSize: 12),
              ),
              trailing: const Icon(Icons.chevron_right, size: 20),
              onTap: widget.onSearchAssets,
            ),
          ),
          const SizedBox(height: 28),
          if (widget.preventiveMaintenanceRepository != null) ...[
            // My PM Tasks & Active Batches
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceBetween,
              children: [
                const Text(
                  'My PM Tasks',
                  style: TextStyle(
                    fontSize: 16,
                    fontWeight: FontWeight.w800,
                    color: AppColors.textPrimary,
                  ),
                ),
                if (_isLoadingBatches)
                  const SizedBox(
                    width: 16,
                    height: 16,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  ),
              ],
            ),
            const SizedBox(height: 12),

            if (_batchError != null)
              Card(
                color: AppColors.pageBackground,
                child: Padding(
                  padding: const EdgeInsets.all(16),
                  child: Row(
                    children: [
                      const Icon(
                        Icons.error_outline,
                        color: AppColors.error,
                        size: 20,
                      ),
                      const SizedBox(width: 10),
                      Expanded(
                        child: Text(
                          _batchError!,
                          style: const TextStyle(
                            fontSize: 13,
                            color: AppColors.error,
                          ),
                        ),
                      ),
                      TextButton(
                        onPressed: _loadBatches,
                        child: const Text('Retry'),
                      ),
                    ],
                  ),
                ),
              )
            else ...[
              if (!_isLoadingBatches &&
                  _activeDrafts.isEmpty &&
                  _unstartedAssignedBatches.isEmpty &&
                  _awaitingAck.isEmpty)
                Card(
                  child: Padding(
                    padding: const EdgeInsets.all(24),
                    child: Column(
                      children: [
                        const Icon(
                          Icons.checklist_rtl_outlined,
                          size: 44,
                          color: AppColors.textNeutral,
                        ),
                        const SizedBox(height: 12),
                        const Text(
                          'No assigned PM tasks',
                          style: TextStyle(
                            fontSize: 15,
                            fontWeight: FontWeight.w700,
                            color: AppColors.textPrimary,
                          ),
                        ),
                        const SizedBox(height: 4),
                        const Text(
                          'You have no assigned PM tasks for this period.',
                          textAlign: TextAlign.center,
                          style: TextStyle(
                            fontSize: 13,
                            color: AppColors.textNeutral,
                          ),
                        ),
                      ],
                    ),
                  ),
                )
              else ...[
                // Active Drafts
                ..._activeDrafts.map((draft) {
                  final total = _calculateTotalForDraft(draft);
                  return Padding(
                    padding: const EdgeInsets.only(bottom: 12),
                    child: BatchPmCard(
                      key: ValueKey('draft-card-${draft.id}'),
                      department: draft.department ?? 'General Department',
                      assetCategory: draft.assetCategory,
                      pmCycle: _resolvePmCycle(
                        pmCycle: draft.pmCycle,
                        periodType: draft.periodType,
                        year: draft.year,
                      ),
                      status: 'In Progress',
                      completedCount: draft.inspections.length,
                      totalCount: total,
                      building: draft.building,
                      actionLabel: 'Continue PM batch',
                      onAction: () => widget.onOpenForm?.call(draft.id),
                      onTap: () => widget.onOpenForm?.call(draft.id),
                    ),
                  );
                }),

                // Assigned PM Batches without existing drafts
                ..._unstartedAssignedBatches.map((batch) {
                  return Padding(
                    padding: const EdgeInsets.only(bottom: 12),
                    child: BatchPmCard(
                      key: ValueKey(
                        'assigned-batch-${_canonicalBatchKey(batch.department, batch.assetCategory, batch.pmCycle)}',
                      ),
                      department: batch.department,
                      assetCategory: batch.assetCategory,
                      pmCycle: batch.pmCycle,
                      status: batch.status,
                      completedCount: 0,
                      totalCount: batch.totalCount,
                      building: batch.building,
                      actionLabel:
                          (widget.onStartBatch != null ||
                              widget.onScanQr != null)
                          ? 'Start inspection'
                          : null,
                      onAction: () => _startBatch(batch),
                      onTap: () => _startBatch(batch),
                    ),
                  );
                }),

                // Awaiting Acknowledgement
                if (_awaitingAck.isNotEmpty) ...[
                  const SizedBox(height: 16),
                  const Text(
                    'Awaiting Acknowledgement',
                    style: TextStyle(
                      fontSize: 15,
                      fontWeight: FontWeight.w700,
                      color: AppColors.textPrimary,
                    ),
                  ),
                  const SizedBox(height: 10),
                  ..._awaitingAck.map((form) {
                    return Padding(
                      padding: const EdgeInsets.only(bottom: 12),
                      child: BatchPmCard(
                        key: ValueKey('ack-card-${form.id}'),
                        department: form.department ?? 'General Department',
                        assetCategory: form.assetCategory,
                        pmCycle: _resolvePmCycle(
                          pmCycle: form.pmCycle,
                          periodType: form.periodType,
                          year: form.year,
                        ),
                        status: form.status,
                        completedCount: form.inspections.length,
                        totalCount: form.inspections.length,
                        building: form.building,
                        actionLabel: 'Capture Signature',
                        onAction: () =>
                            widget.onOpenAcknowledgement?.call(form),
                        onTap: () => widget.onOpenAcknowledgement?.call(form),
                      ),
                    );
                  }),
                ],
              ],
            ],
          ],

          const SizedBox(height: 20),

          // Secondary/Administrative entry for backward compatibility
          if (_isGsd && widget.onOpenPreventiveMaintenance != null)
            Card(
              child: ListTile(
                leading: const Icon(Icons.assignment_outlined),
                title: const Text('Preventive-maintenance forms'),
                subtitle: const Text(
                  'Create, resume, submit, and acknowledge PM forms.',
                ),
                trailing: const Icon(Icons.chevron_right),
                onTap: widget.onOpenPreventiveMaintenance,
              ),
            ),
        ],
      ),
    );
  }
}

class _QuickActionCard extends StatelessWidget {
  const _QuickActionCard({
    super.key,
    required this.icon,
    required this.title,
    required this.subtitle,
    required this.isPrimary,
    this.onTap,
  });

  final IconData icon;
  final String title;
  final String subtitle;
  final bool isPrimary;
  final VoidCallback? onTap;

  @override
  Widget build(BuildContext context) {
    return Card(
      color: isPrimary ? AppColors.primary : Colors.white,
      child: InkWell(
        onTap: onTap,
        borderRadius: BorderRadius.circular(12),
        child: Padding(
          padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 18),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Icon(
                icon,
                size: 28,
                color: isPrimary ? Colors.white : AppColors.primary,
              ),
              const SizedBox(height: 14),
              Text(
                title,
                style: TextStyle(
                  fontSize: 16,
                  fontWeight: FontWeight.w800,
                  color: isPrimary ? Colors.white : AppColors.textPrimary,
                  letterSpacing: -0.3,
                ),
              ),
              const SizedBox(height: 2),
              Text(
                subtitle,
                style: TextStyle(
                  fontSize: 12,
                  fontWeight: FontWeight.w500,
                  color: isPrimary
                      ? Colors.white.withValues(alpha: 0.85)
                      : AppColors.textNeutral,
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _AssignedPmBatch {
  const _AssignedPmBatch({
    required this.department,
    required this.assetCategory,
    required this.pmCycle,
    required this.status,
    required this.totalCount,
    this.building,
  });

  final String department;
  final String assetCategory;
  final String pmCycle;
  final String status;
  final int totalCount;
  final String? building;
}
