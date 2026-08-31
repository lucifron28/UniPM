import 'dart:async';

import 'package:flutter/material.dart';

import '../../auth/auth_models.dart';
import '../assets/asset_models.dart';
import 'preventive_maintenance_controller.dart';
import 'preventive_maintenance_models.dart';
import 'preventive_maintenance_page.dart';
import 'preventive_maintenance_repository.dart';

class ScannedAssetPmEntry extends StatefulWidget {
  const ScannedAssetPmEntry({
    super.key,
    required this.asset,
    required this.repository,
    required this.user,
  });

  final Asset asset;
  final PreventiveMaintenanceRepository repository;
  final AuthUser user;

  @override
  State<ScannedAssetPmEntry> createState() => _ScannedAssetPmEntryState();
}

class _ScannedAssetPmEntryState extends State<ScannedAssetPmEntry> {
  late final PreventiveMaintenanceController controller =
      PreventiveMaintenanceController(
        repository: widget.repository,
        user: widget.user,
      );

  List<ScheduleOption> schedules = const [];
  ScheduleOption? selectedSchedule;
  PmDraftResolution? resolution;
  PreventiveMaintenanceForm? chosenDraft;
  bool isLoadingSchedules = true;
  bool isResolving = false;
  bool isOpening = false;
  String? errorMessage;

  bool get isActiveAsset => widget.asset.status == 'Active';

  @override
  void initState() {
    super.initState();
    unawaited(_loadSchedules());
  }

  @override
  void dispose() {
    controller.dispose();
    super.dispose();
  }

  Future<void> _loadSchedules() async {
    setState(() {
      isLoadingSchedules = true;
      errorMessage = null;
      schedules = const [];
      selectedSchedule = null;
      resolution = null;
      chosenDraft = null;
    });
    try {
      final values = await widget.repository.listSchedules(
        assetId: widget.asset.id,
      );
      if (!mounted) return;
      final applicable = values
          .where((schedule) => schedule.assetId == widget.asset.id)
          .where((schedule) => _applicableStatuses.contains(schedule.status))
          .toList(growable: false);
      setState(() {
        schedules = applicable;
        isLoadingSchedules = false;
        if (applicable.length == 1) selectedSchedule = applicable.single;
      });
      if (applicable.length == 1) await _resolveSelectedSchedule();
    } catch (error) {
      if (!mounted) return;
      setState(() {
        isLoadingSchedules = false;
        errorMessage = friendlyError(error);
      });
    }
  }

  Future<void> _selectSchedule(String? scheduleId) async {
    if (scheduleId == null) return;
    setState(() {
      selectedSchedule = schedules.singleWhere(
        (schedule) => schedule.id == scheduleId,
      );
      resolution = null;
      chosenDraft = null;
      errorMessage = null;
    });
    await _resolveSelectedSchedule();
  }

  Future<void> _resolveSelectedSchedule() async {
    final schedule = selectedSchedule;
    if (schedule == null) return;
    setState(() {
      isResolving = true;
      resolution = null;
      chosenDraft = null;
      errorMessage = null;
    });
    await controller.loadForms();
    if (!mounted) return;
    if (controller.errorMessage != null) {
      setState(() {
        isResolving = false;
        errorMessage = controller.errorMessage;
      });
      return;
    }
    final nextResolution = controller.resolveDraftFor(widget.asset, schedule);
    setState(() {
      resolution = nextResolution;
      chosenDraft = nextResolution.form;
      isResolving = false;
    });
  }

  Future<void> _openPm() async {
    final schedule = selectedSchedule;
    final currentResolution = resolution;
    if (schedule == null || currentResolution == null || isOpening) return;
    if (!isActiveAsset &&
        currentResolution.kind != PmDraftResolutionKind.resume) {
      return;
    }

    setState(() {
      isOpening = true;
      errorMessage = null;
    });

    PreventiveMaintenanceForm? form;
    String? preselectedScheduleId;
    String? focusedInspectionId;
    switch (currentResolution.kind) {
      case PmDraftResolutionKind.resume:
        form = currentResolution.form;
        focusedInspectionId = currentResolution.inspectionId;
      case PmDraftResolutionKind.reuse:
        form = currentResolution.form;
        preselectedScheduleId = schedule.id;
      case PmDraftResolutionKind.create:
        form = await controller.createDraft(
          currentResolution.grouping!.toCreateInput(),
        );
        preselectedScheduleId = schedule.id;
      case PmDraftResolutionKind.choose:
        form = chosenDraft;
        preselectedScheduleId = schedule.id;
    }

    if (!mounted) return;
    if (form == null) {
      setState(() {
        isOpening = false;
        errorMessage =
            controller.errorMessage ?? 'Select a compatible Draft form.';
      });
      return;
    }

    controller.selectForm(form);
    setState(() => isOpening = false);
    await Navigator.of(context).push<void>(
      MaterialPageRoute<void>(
        builder: (_) => PreventiveMaintenanceDraftPage(
          controller: controller,
          formId: form!.id,
          preselectedScheduleId: preselectedScheduleId,
          focusedInspectionId: focusedInspectionId,
        ),
      ),
    );
    if (mounted) await _resolveSelectedSchedule();
  }

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(20),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(
              'Preventive maintenance',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            const SizedBox(height: 12),
            if (isLoadingSchedules)
              const _PmLoading(
                key: Key('pm-entry-loading'),
                label: 'Loading PM schedules...',
              )
            else if (errorMessage != null)
              _PmError(message: errorMessage!, onRetry: _retry)
            else if (schedules.isEmpty)
              const Text(
                'No applicable PM schedules are available for this asset.',
                key: Key('pm-schedule-empty'),
              )
            else ...[
              if (schedules.length == 1)
                _ScheduleSummary(schedule: schedules.single)
              else
                DropdownButtonFormField<String>(
                  key: const Key('pm-schedule-select'),
                  initialValue: selectedSchedule?.id,
                  decoration: const InputDecoration(labelText: 'PM schedule'),
                  items: schedules
                      .map(
                        (schedule) => DropdownMenuItem(
                          value: schedule.id,
                          child: Text(_scheduleLabel(schedule)),
                        ),
                      )
                      .toList(growable: false),
                  onChanged: isResolving ? null : _selectSchedule,
                ),
              if (isResolving) ...[
                const SizedBox(height: 16),
                const _PmLoading(label: 'Checking available Draft forms...'),
              ] else if (resolution != null) ...[
                const SizedBox(height: 16),
                _buildResolution(context, resolution!),
              ],
            ],
          ],
        ),
      ),
    );
  }

  Widget _buildResolution(
    BuildContext context,
    PmDraftResolution currentResolution,
  ) {
    if (currentResolution.kind == PmDraftResolutionKind.choose) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const Text('Choose the compatible Draft to continue.'),
          const SizedBox(height: 12),
          DropdownButtonFormField<String>(
            key: const Key('compatible-draft-select'),
            initialValue: chosenDraft?.id,
            decoration: const InputDecoration(labelText: 'Draft form'),
            items: currentResolution.forms
                .map(
                  (form) => DropdownMenuItem(
                    value: form.id,
                    child: Text(
                      form.fileNumber ??
                          'Draft from ${_dateText(form.createdAt)}',
                    ),
                  ),
                )
                .toList(growable: false),
            onChanged: (id) {
              if (id == null) return;
              setState(() {
                chosenDraft = currentResolution.forms.singleWhere(
                  (form) => form.id == id,
                );
              });
            },
          ),
          const SizedBox(height: 12),
          _actionButton(currentResolution),
        ],
      );
    }

    final description = switch (currentResolution.kind) {
      PmDraftResolutionKind.resume =>
        'This schedule already has a Draft inspection row.',
      PmDraftResolutionKind.reuse =>
        'A compatible Draft is ready for another inspection row.',
      PmDraftResolutionKind.create =>
        'A new Draft will use this asset and schedule metadata.',
      PmDraftResolutionKind.choose => '',
    };
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(description),
        if (!isActiveAsset &&
            currentResolution.kind != PmDraftResolutionKind.resume) ...[
          const SizedBox(height: 8),
          Text(
            'Start PM is unavailable while this asset is ${widget.asset.status}.',
            key: const Key('pm-entry-blocked'),
            style: TextStyle(color: Theme.of(context).colorScheme.error),
          ),
        ],
        const SizedBox(height: 12),
        _actionButton(currentResolution),
      ],
    );
  }

  Widget _actionButton(PmDraftResolution currentResolution) {
    final isResume = currentResolution.kind == PmDraftResolutionKind.resume;
    final canOpen =
        !isOpening &&
        (isResume || isActiveAsset) &&
        (currentResolution.kind != PmDraftResolutionKind.choose ||
            chosenDraft != null);
    return FilledButton.icon(
      key: Key(isResume ? 'resume-pm' : 'start-pm'),
      onPressed: canOpen ? _openPm : null,
      icon: Icon(isResume ? Icons.edit_note : Icons.play_arrow),
      label: Text(
        isOpening
            ? 'Opening...'
            : isResume
            ? 'Resume PM'
            : 'Start PM',
      ),
    );
  }

  Future<void> _retry() async {
    if (selectedSchedule == null) {
      await _loadSchedules();
    } else {
      await _resolveSelectedSchedule();
    }
  }
}

class _PmLoading extends StatelessWidget {
  const _PmLoading({super.key, required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    return Row(
      children: [
        const SizedBox(
          width: 24,
          height: 24,
          child: CircularProgressIndicator(strokeWidth: 3),
        ),
        const SizedBox(width: 12),
        Expanded(child: Text(label)),
      ],
    );
  }
}

class _PmError extends StatelessWidget {
  const _PmError({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(message, key: const Key('pm-entry-error')),
        const SizedBox(height: 8),
        OutlinedButton(onPressed: onRetry, child: const Text('Retry')),
      ],
    );
  }
}

class _ScheduleSummary extends StatelessWidget {
  const _ScheduleSummary({required this.schedule});

  final ScheduleOption schedule;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          'Selected schedule',
          style: Theme.of(context).textTheme.labelLarge,
        ),
        const SizedBox(height: 4),
        Text(_scheduleLabel(schedule), key: const Key('selected-pm-schedule')),
      ],
    );
  }
}

const _applicableStatuses = {'Due', 'Ongoing', 'Overdue'};

String _scheduleLabel(ScheduleOption schedule) =>
    '${_dateText(schedule.scheduleDate)} · ${schedule.periodType} · ${schedule.status}';

String _dateText(DateTime value) {
  final local = value.toLocal();
  final month = local.month.toString().padLeft(2, '0');
  final day = local.day.toString().padLeft(2, '0');
  return '${local.year}-$month-$day';
}
