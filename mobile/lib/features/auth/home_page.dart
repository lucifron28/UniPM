import 'dart:async';

import 'package:flutter/material.dart';

import '../../api/api_exception.dart';
import '../../auth/auth_models.dart';
import '../../ui/app_colors.dart';
import '../../ui/widgets/batch_pm_card.dart';
import '../preventive_maintenance/offline_inspection_repository.dart';
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
  final FutureOr<void> Function()? onScanQr;
  final FutureOr<void> Function()? onEnterAssetCode;
  final FutureOr<void> Function()? onSearchAssets;
  final FutureOr<void> Function(PmBatchScope)? onStartBatch;
  final FutureOr<void> Function()? onOpenPreventiveMaintenance;
  final PreventiveMaintenanceRepository? preventiveMaintenanceRepository;
  final FutureOr<void> Function(String)? onOpenForm;
  final FutureOr<void> Function(PreventiveMaintenanceForm)?
  onOpenAcknowledgement;

  @override
  State<HomePage> createState() => _HomePageState();
}

class _HomePageState extends State<HomePage> {
  List<PreventiveMaintenanceForm> _forms = [];
  List<ScheduleOption> _schedules = [];
  bool _isLoadingBatches = false;
  String? _formsError;
  String? _schedulesError;

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
      _formsError = null;
      _schedulesError = null;
    });

    await Future.wait<void>([_loadForms(repo), _loadSchedules(repo)]);
    if (!mounted) return;
    setState(() => _isLoadingBatches = false);
  }

  Future<void> _loadForms(PreventiveMaintenanceRepository repo) async {
    try {
      final forms = await repo.listForms();
      if (!mounted) return;
      setState(() => _forms = forms);
    } catch (error) {
      if (!mounted || _isTerminalAuthenticationFailure(error)) return;
      setState(() => _formsError = 'Could not load active PM forms.');
    }
  }

  Future<void> _loadSchedules(PreventiveMaintenanceRepository repo) async {
    try {
      final schedules = await repo.listSchedules();
      if (!mounted) return;
      setState(() => _schedules = schedules);
    } catch (error) {
      if (!mounted || _isTerminalAuthenticationFailure(error)) return;
      setState(() => _schedulesError = 'Could not load assigned PM schedules.');
    }
  }

  bool _isTerminalAuthenticationFailure(Object error) =>
      error is ApiException && error.isUnauthorized;

  Future<void> _runChildWorkflow(
    FutureOr<void> Function()? openWorkflow,
  ) async {
    if (openWorkflow == null) return;
    try {
      await openWorkflow();
    } finally {
      if (mounted) await _loadBatches();
    }
  }

  Future<void> _runFromList(FutureOr<void> Function()? openWorkflow) async {
    if (openWorkflow == null) return;
    await Navigator.of(context).maybePop();
    await _runChildWorkflow(openWorkflow);
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

  List<ScheduleOption> get _assignedSchedules {
    return _schedules
        .where((s) {
          final isAssigned = _isGsd || s.assignedToUserId == widget.user.id;
          return isAssigned && s.isEligibleForFieldWork;
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

  int _statusOrder(String status) => switch (status.toLowerCase()) {
    'overdue' => 0,
    'ongoing' || 'in progress' => 1,
    _ => 2,
  };

  int _comparePmCycles(String left, String right) {
    final cyclePattern = RegExp(r'^([0-9]{4})-(0[1-9]|1[0-2])$');
    final leftMatch = cyclePattern.firstMatch(left);
    final rightMatch = cyclePattern.firstMatch(right);
    if (leftMatch != null && rightMatch != null) {
      final yearOrder = leftMatch.group(1)!.compareTo(rightMatch.group(1)!);
      if (yearOrder != 0) return yearOrder;
      return leftMatch.group(2)!.compareTo(rightMatch.group(2)!);
    }
    if (leftMatch != null) return -1;
    if (rightMatch != null) return 1;
    return left.toLowerCase().compareTo(right.toLowerCase());
  }

  List<_AssignedPmBatch> get _assignedBatches =>
      _groupSchedules(_assignedSchedules);
  List<_HomePmTask> _buildPmTasks({bool closeListBeforeAction = false}) {
    final tasks = <_HomePmTask>[
      ..._activeDrafts.map((draft) {
        final cycle = _resolvePmCycle(
          pmCycle: draft.pmCycle,
          periodType: draft.periodType,
          year: draft.year,
        );
        return _HomePmTask(
          status: 'In Progress',
          pmCycle: cycle,
          department: draft.department ?? 'General Department',
          assetCategory: draft.assetCategory,
          card: _draftCard(
            draft,
            cycle,
            closeListBeforeAction: closeListBeforeAction,
          ),
        );
      }),
      ..._unstartedAssignedBatches.map(
        (batch) => _HomePmTask(
          status: batch.status,
          pmCycle: batch.pmCycle,
          department: batch.department,
          assetCategory: batch.assetCategory,
          card: _assignedBatchCard(
            batch,
            closeListBeforeAction: closeListBeforeAction,
          ),
        ),
      ),
    ];
    tasks.sort((left, right) {
      final statusOrder = _statusOrder(
        left.status,
      ).compareTo(_statusOrder(right.status));
      if (statusOrder != 0) return statusOrder;

      final cycleOrder = _comparePmCycles(left.pmCycle, right.pmCycle);
      if (cycleOrder != 0) return cycleOrder;

      final departmentOrder = left.department.toLowerCase().compareTo(
        right.department.toLowerCase(),
      );
      if (departmentOrder != 0) return departmentOrder;
      return left.assetCategory.toLowerCase().compareTo(
        right.assetCategory.toLowerCase(),
      );
    });
    return tasks;
  }

  List<_HomePmTask> get _orderedPmTasks => _buildPmTasks();

  Widget _draftCard(
    PreventiveMaintenanceForm draft,
    String cycle, {
    bool closeListBeforeAction = false,
  }) {
    final total = _calculateTotalForDraft(draft);
    Future<void> openDraft() async {
      if (closeListBeforeAction) {
        await _runFromList(() => widget.onOpenForm?.call(draft.id));
      } else {
        await _runChildWorkflow(() => widget.onOpenForm?.call(draft.id));
      }
    }

    return Padding(
      padding: const EdgeInsets.only(bottom: 12),
      child: BatchPmCard(
        key: ValueKey('draft-card-${draft.id}'),
        department: draft.department ?? 'General Department',
        assetCategory: draft.assetCategory,
        pmCycle: cycle,
        status: 'In Progress',
        completedCount: draft.inspections.length,
        totalCount: total,
        building: draft.building,
        actionLabel: 'Continue PM batch',
        onAction: widget.onOpenForm == null ? null : openDraft,
        onTap: widget.onOpenForm == null ? null : openDraft,
      ),
    );
  }

  Widget _assignedBatchCard(
    _AssignedPmBatch batch, {
    bool closeListBeforeAction = false,
  }) {
    Future<void> startBatch() async {
      if (closeListBeforeAction) {
        await Navigator.of(context).maybePop();
      }
      await _startBatch(batch);
    }

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
        actionLabel: (widget.onStartBatch != null || widget.onScanQr != null)
            ? 'Start inspection'
            : null,
        onAction: startBatch,
        onTap: startBatch,
      ),
    );
  }

  Widget _acknowledgementCard(
    PreventiveMaintenanceForm form, {
    bool closeListBeforeAction = false,
  }) {
    Future<void> openAcknowledgement() async {
      if (closeListBeforeAction) {
        await _runFromList(() => widget.onOpenAcknowledgement?.call(form));
      } else {
        await _runChildWorkflow(() => widget.onOpenAcknowledgement?.call(form));
      }
    }

    final cycle = _resolvePmCycle(
      pmCycle: form.pmCycle,
      periodType: form.periodType,
      year: form.year,
    );
    return _PendingAcknowledgementCard(
      key: ValueKey('ack-card-${form.id}'),
      form: form,
      cycle: cycle,
      onOpen: widget.onOpenAcknowledgement == null ? null : openAcknowledgement,
    );
  }

  Future<void> _openItemList({
    required String title,
    required List<Widget> children,
  }) async {
    await Navigator.of(context).push<void>(
      MaterialPageRoute<void>(
        builder: (_) => _HomeItemListPage(title: title, children: children),
      ),
    );
  }

  Widget _pendingAcknowledgementsSection() {
    final pending = _awaitingAck;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            const Expanded(
              child: Text(
                'Pending acknowledgements',
                style: TextStyle(
                  fontSize: 16,
                  fontWeight: FontWeight.w800,
                  color: AppColors.textPrimary,
                ),
              ),
            ),
            Container(
              key: const Key('pending-ack-count'),
              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
              decoration: BoxDecoration(
                color: pending.isEmpty
                    ? AppColors.surfaceMuted
                    : AppColors.warning.withValues(alpha: 0.14),
                borderRadius: BorderRadius.circular(999),
              ),
              child: Text(
                '${pending.length}',
                style: TextStyle(
                  color: pending.isEmpty
                      ? AppColors.textNeutral
                      : AppColors.warning,
                  fontWeight: FontWeight.w800,
                ),
              ),
            ),
            if (_isLoadingBatches) ...[
              const SizedBox(width: 10),
              const SizedBox(
                width: 16,
                height: 16,
                child: CircularProgressIndicator(strokeWidth: 2),
              ),
            ],
          ],
        ),
        if (_formsError != null) ...[
          const SizedBox(height: 8),
          _BatchLoadErrorCard(message: _formsError!, onRetry: _loadBatches),
        ] else if (pending.isNotEmpty) ...[
          const SizedBox(height: 8),
          ...pending.take(2).map(_acknowledgementCard),
          if (pending.isNotEmpty)
            Align(
              alignment: Alignment.centerLeft,
              child: TextButton.icon(
                key: const Key('view-all-pending-forms'),
                onPressed: () => _openItemList(
                  title: 'Pending acknowledgements',
                  children: pending
                      .map(
                        (form) => _acknowledgementCard(
                          form,
                          closeListBeforeAction: true,
                        ),
                      )
                      .toList(growable: false),
                ),
                icon: const Icon(Icons.list_alt_outlined),
                label: Text('View all pending forms (${pending.length})'),
              ),
            ),
        ],
        const SizedBox(height: 20),
      ],
    );
  }

  Widget _pmTasksSection() {
    final tasks = _orderedPmTasks;
    final visibleTasks = tasks.take(2).toList(growable: false);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          mainAxisAlignment: MainAxisAlignment.spaceBetween,
          children: [
            const Text(
              'Urgent PM tasks',
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
        if (_schedulesError != null)
          _BatchLoadErrorCard(message: _schedulesError!, onRetry: _loadBatches),
        if (!_isLoadingBatches &&
            _formsError == null &&
            _schedulesError == null &&
            tasks.isEmpty)
          Card(
            child: Padding(
              padding: const EdgeInsets.all(20),
              child: Row(
                children: [
                  const Icon(
                    Icons.checklist_rtl_outlined,
                    color: AppColors.textNeutral,
                  ),
                  const SizedBox(width: 12),
                  const Expanded(
                    child: Text(
                      'No open PM tasks',
                      style: TextStyle(fontWeight: FontWeight.w700),
                    ),
                  ),
                ],
              ),
            ),
          ),
        ...visibleTasks.map((task) => task.card),
        if (tasks.length > visibleTasks.length)
          Align(
            alignment: Alignment.centerLeft,
            child: TextButton.icon(
              key: const Key('view-all-pm-tasks'),
              onPressed: () => _openItemList(
                title: 'All PM tasks',
                children: _buildPmTasks(
                  closeListBeforeAction: true,
                ).map((task) => task.card).toList(growable: false),
              ),
              icon: const Icon(Icons.view_list_outlined),
              label: Text('View all PM tasks (${tasks.length})'),
            ),
          ),
      ],
    );
  }

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

  Future<void> _startBatch(_AssignedPmBatch batch) async {
    final callback = widget.onStartBatch;
    if (callback != null) {
      await _runChildWorkflow(
        () => callback(
          PmBatchScope(
            department: batch.department,
            assetCategory: batch.assetCategory,
            pmCycle: batch.pmCycle,
          ),
        ),
      );
    } else {
      await _runChildWorkflow(widget.onScanQr);
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
          const SizedBox(height: 16),

          if (widget.preventiveMaintenanceRepository != null) ...[
            _pendingAcknowledgementsSection(),
          ],
          if (widget.preventiveMaintenanceRepository
              case final OfflineInspectionRepository offlineRepository) ...[
            _OfflineSyncBanner(
              repository: offlineRepository,
              onReviewDrafts: () =>
                  _runChildWorkflow(widget.onOpenPreventiveMaintenance),
            ),
            const SizedBox(height: 16),
          ],

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
                  onTap: widget.onScanQr == null
                      ? null
                      : () => _runChildWorkflow(widget.onScanQr),
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
                  onTap: widget.onEnterAssetCode == null
                      ? null
                      : () => _runChildWorkflow(widget.onEnterAssetCode),
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
              onTap: widget.onSearchAssets == null
                  ? null
                  : () => _runChildWorkflow(widget.onSearchAssets),
            ),
          ),
          const SizedBox(height: 20),
          if (widget.preventiveMaintenanceRepository != null) ...[
            _pmTasksSection(),
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
                onTap: () =>
                    _runChildWorkflow(widget.onOpenPreventiveMaintenance),
              ),
            ),
        ],
      ),
    );
  }
}

class _OfflineSyncBanner extends StatelessWidget {
  const _OfflineSyncBanner({
    required this.repository,
    required this.onReviewDrafts,
  });

  final OfflineInspectionRepository repository;
  final FutureOr<void> Function()? onReviewDrafts;

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: repository,
      builder: (context, _) {
        final state = repository.syncState;
        if (state == OfflineSyncState.ready) return const SizedBox.shrink();
        final (icon, title, detail) = switch (state) {
          OfflineSyncState.offline => (
            Icons.cloud_off_outlined,
            'Offline mode',
            repository.pendingCount > 0
                ? '${repository.pendingCount} saved change(s) will sync when connected.'
                : repository.hasCachedSchedules
                ? 'Cached schedules and drafts are available on this device.'
                : 'Connect once to cache assigned schedules before field work.',
          ),
          OfflineSyncState.queued => (
            Icons.cloud_upload_outlined,
            'Changes waiting to sync',
            '${repository.pendingCount} saved change(s) are queued on this device.',
          ),
          OfflineSyncState.syncing => (
            Icons.sync,
            'Syncing saved changes',
            '${repository.pendingCount} change(s) remaining.',
          ),
          OfflineSyncState.attention => (
            Icons.warning_amber_outlined,
            'Sync needs review',
            repository.lastIssue ??
                'Your local changes are preserved on this device.',
          ),
          OfflineSyncState.ready => (Icons.cloud_done_outlined, '', ''),
        };
        return Card(
          color: state == OfflineSyncState.attention
              ? Theme.of(context).colorScheme.errorContainer
              : Theme.of(context).colorScheme.surfaceContainerLow,
          child: Padding(
            padding: const EdgeInsets.all(14),
            child: Row(
              children: [
                Icon(icon),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        title,
                        style: Theme.of(context).textTheme.titleSmall,
                      ),
                      const SizedBox(height: 2),
                      Text(detail),
                    ],
                  ),
                ),
                if (repository.pendingCount > 0 &&
                    repository.conflictCount == 0)
                  TextButton(
                    key: const Key('offline-sync-retry'),
                    onPressed: repository.syncState == OfflineSyncState.syncing
                        ? null
                        : () => unawaited(repository.syncNow()),
                    child: const Text('Retry sync'),
                  ),
                if (state == OfflineSyncState.attention &&
                    onReviewDrafts != null)
                  TextButton(
                    key: const Key('offline-review-drafts'),
                    onPressed: _openReviewDrafts,
                    child: const Text('Review saved drafts'),
                  ),
              ],
            ),
          ),
        );
      },
    );
  }

  Future<void> _openReviewDrafts() async {
    await onReviewDrafts?.call();
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

class _PendingAcknowledgementCard extends StatelessWidget {
  const _PendingAcknowledgementCard({
    super.key,
    required this.form,
    required this.cycle,
    required this.onOpen,
  });

  final PreventiveMaintenanceForm form;
  final String cycle;
  final VoidCallback? onOpen;

  @override
  Widget build(BuildContext context) {
    return Card(
      margin: const EdgeInsets.only(bottom: 8),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
        child: Row(
          children: [
            const Icon(
              Icons.assignment_late_outlined,
              color: AppColors.warning,
            ),
            const SizedBox(width: 10),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    form.fileNumber ?? 'PM form awaiting signature',
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      fontWeight: FontWeight.w700,
                      color: AppColors.textPrimary,
                    ),
                  ),
                  const SizedBox(height: 2),
                  Text(
                    '${form.department ?? 'General Department'} · $cycle · '
                    '${form.inspections.length} inspections',
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      fontSize: 12,
                      color: AppColors.textNeutral,
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(width: 4),
            TextButton.icon(
              onPressed: onOpen,
              icon: const Icon(Icons.draw_outlined, size: 18),
              label: const Text('Sign'),
              style: TextButton.styleFrom(
                padding: const EdgeInsets.symmetric(horizontal: 8),
                visualDensity: VisualDensity.compact,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _HomeItemListPage extends StatelessWidget {
  const _HomeItemListPage({required this.title, required this.children});

  final String title;
  final List<Widget> children;

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: Text(title)),
      body: SafeArea(
        child: ListView(padding: const EdgeInsets.all(16), children: children),
      ),
    );
  }
}

class _BatchLoadErrorCard extends StatelessWidget {
  const _BatchLoadErrorCard({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Card(
      color: AppColors.pageBackground,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Row(
          children: [
            const Icon(Icons.error_outline, color: AppColors.error, size: 20),
            const SizedBox(width: 10),
            Expanded(
              child: Text(
                message,
                style: const TextStyle(fontSize: 13, color: AppColors.error),
              ),
            ),
            TextButton(onPressed: onRetry, child: const Text('Retry')),
          ],
        ),
      ),
    );
  }
}

class _HomePmTask {
  const _HomePmTask({
    required this.status,
    required this.pmCycle,
    required this.department,
    required this.assetCategory,
    required this.card,
  });

  final String status;
  final String pmCycle;
  final String department;
  final String assetCategory;
  final Widget card;
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
