import 'package:flutter/material.dart';

import '../../auth/auth_models.dart';
import 'preventive_maintenance_controller.dart';
import 'preventive_maintenance_models.dart';
import 'preventive_maintenance_repository.dart';

class PreventiveMaintenancePage extends StatefulWidget {
  const PreventiveMaintenancePage({
    super.key,
    required this.repository,
    required this.user,
  });

  final PreventiveMaintenanceRepository repository;
  final AuthUser user;

  @override
  State<PreventiveMaintenancePage> createState() =>
      _PreventiveMaintenancePageState();
}

class _PreventiveMaintenancePageState extends State<PreventiveMaintenancePage> {
  late final PreventiveMaintenanceController controller;

  @override
  void initState() {
    super.initState();
    controller = PreventiveMaintenanceController(
      repository: widget.repository,
      user: widget.user,
    )..loadForms();
  }

  @override
  void dispose() {
    controller.dispose();
    super.dispose();
  }

  Future<void> _openCreate() async {
    await Navigator.of(context).push<void>(
      MaterialPageRoute<void>(
        builder: (_) => _CreateDraftPage(controller: controller),
      ),
    );
    if (mounted) await controller.loadForms();
  }

  Future<void> _openDraft(String formId) async {
    final form = controller.forms.firstWhere(
      (candidate) => candidate.id == formId,
    );
    controller.selectForm(form);
    await Navigator.of(context).push<void>(
      MaterialPageRoute<void>(
        builder: (_) => PreventiveMaintenanceDraftPage(
          controller: controller,
          formId: formId,
        ),
      ),
    );
    if (mounted) await controller.loadForms();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Preventive-maintenance drafts')),
      body: AnimatedBuilder(
        animation: controller,
        builder: (context, _) {
          if (controller.isLoading) {
            return const Center(
              child: CircularProgressIndicator(key: Key('forms-loading')),
            );
          }

          if (controller.errorMessage != null) {
            return _ErrorState(
              message: controller.errorMessage!,
              onRetry: controller.loadForms,
            );
          }

          final forms = controller.visibleDrafts;
          if (forms.isEmpty) {
            return _EmptyFormsState(onCreate: _openCreate);
          }

          return ListView(
            padding: const EdgeInsets.all(16),
            children: [
              Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [
                  Text(
                    'Draft forms',
                    style: Theme.of(context).textTheme.titleLarge,
                  ),
                  OutlinedButton(
                    onPressed: _openCreate,
                    child: const Text('Create draft'),
                  ),
                ],
              ),
              const SizedBox(height: 8),
              const Text(
                'Draft forms are saved to UniPM as you add and edit inspection rows.',
              ),
              const SizedBox(height: 16),
              ...forms.map(
                (form) => Card(
                  child: ListTile(
                    key: Key('draft-form-${form.id}'),
                    title: Text(form.fileNumber ?? 'Unsubmitted draft'),
                    subtitle: Text(
                      '${form.assetCategory} | ${form.inspections.length} inspection row(s)\n${form.building ?? 'Building not recorded'} / ${form.department ?? 'Department not recorded'}',
                    ),
                    isThreeLine: true,
                    trailing: const Icon(Icons.chevron_right),
                    onTap: () => _openDraft(form.id),
                  ),
                ),
              ),
            ],
          );
        },
      ),
      floatingActionButton: FloatingActionButton.extended(
        onPressed: _openCreate,
        icon: const Icon(Icons.add),
        label: const Text('New draft'),
      ),
    );
  }
}

class _EmptyFormsState extends StatelessWidget {
  const _EmptyFormsState({required this.onCreate});

  final VoidCallback onCreate;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.assignment_outlined, size: 48),
            const SizedBox(height: 16),
            const Text(
              'No draft forms yet.',
              style: TextStyle(fontSize: 20, fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: 8),
            const Text(
              'Create a draft to start recording field inspection rows.',
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: 20),
            FilledButton(
              onPressed: onCreate,
              child: const Text('Create draft'),
            ),
          ],
        ),
      ),
    );
  }
}

class _ErrorState extends StatelessWidget {
  const _ErrorState({required this.message, required this.onRetry});

  final String message;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.cloud_off, size: 48),
            const SizedBox(height: 16),
            Text(message, textAlign: TextAlign.center),
            const SizedBox(height: 20),
            OutlinedButton(onPressed: onRetry, child: const Text('Retry')),
          ],
        ),
      ),
    );
  }
}

class _CreateDraftPage extends StatefulWidget {
  const _CreateDraftPage({required this.controller});

  final PreventiveMaintenanceController controller;

  @override
  State<_CreateDraftPage> createState() => _CreateDraftPageState();
}

class _CreateDraftPageState extends State<_CreateDraftPage> {
  late Future<_DraftReferences> references;
  final formKey = GlobalKey<FormState>();
  final buildingController = TextEditingController();
  final departmentController = TextEditingController();
  final semesterController = TextEditingController();
  final yearController = TextEditingController();
  final academicYearController = TextEditingController();
  String? assetCategory;
  String? periodType;
  String? quarter;

  @override
  void initState() {
    super.initState();
    references = _loadReferences();
  }

  @override
  void dispose() {
    buildingController.dispose();
    departmentController.dispose();
    semesterController.dispose();
    yearController.dispose();
    academicYearController.dispose();
    super.dispose();
  }

  Future<_DraftReferences> _loadReferences() async {
    final repository = widget.controller.repository;
    final values = await Future.wait<Object>([
      repository.listAssetCategories(),
      repository.listPeriodTypes(),
      repository.listQuarters(),
    ]);
    return _DraftReferences(
      assetCategories: values[0] as List<ReferenceOption>,
      periodTypes: values[1] as List<ReferenceOption>,
      quarters: values[2] as List<ReferenceOption>,
    );
  }

  Future<void> _create() async {
    if (!(formKey.currentState?.validate() ?? false)) return;
    final created = await widget.controller.createDraft(
      CreatePreventiveMaintenanceFormInput(
        assetCategory: assetCategory!,
        building: _blankToNull(buildingController.text),
        department: _blankToNull(departmentController.text),
        periodType: periodType!,
        quarter: quarter,
        semester: _blankToNull(semesterController.text),
        year: _parseYear(yearController.text),
        academicYear: _blankToNull(academicYearController.text),
      ),
    );
    if (!mounted || created == null) return;
    await Navigator.of(context).pushReplacement<void, void>(
      MaterialPageRoute<void>(
        builder: (_) => PreventiveMaintenanceDraftPage(
          controller: widget.controller,
          formId: created.id,
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Create draft form')),
      body: AnimatedBuilder(
        animation: widget.controller,
        builder: (context, _) => FutureBuilder<_DraftReferences>(
          future: references,
          builder: (context, snapshot) {
            if (snapshot.connectionState != ConnectionState.done) {
              return const Center(child: CircularProgressIndicator());
            }
            if (snapshot.hasError) {
              return _ErrorState(
                message: friendlyError(snapshot.error!),
                onRetry: _retryReferences,
              );
            }
            return _buildForm(context, snapshot.data!);
          },
        ),
      ),
    );
  }

  void _retryReferences() {
    setState(() {
      references = _loadReferences();
    });
  }

  Widget _buildForm(BuildContext context, _DraftReferences values) {
    return Form(
      key: formKey,
      child: ListView(
        padding: const EdgeInsets.all(16),
        children: [
          const Text(
            'Form header',
            style: TextStyle(fontSize: 20, fontWeight: FontWeight.bold),
          ),
          const SizedBox(height: 8),
          const Text(
            'The backend validates the form values. Building and department remain free-text fields until the institutional reference lists are confirmed.',
          ),
          if (widget.controller.errorMessage != null) ...[
            const SizedBox(height: 12),
            _InlineError(message: widget.controller.errorMessage!),
          ],
          const SizedBox(height: 16),
          DropdownButtonFormField<String>(
            key: const Key('form-asset-category'),
            initialValue: assetCategory,
            decoration: const InputDecoration(labelText: 'Asset category'),
            items: values.assetCategories
                .map(
                  (option) => DropdownMenuItem(
                    value: option.code,
                    child: Text(option.displayName),
                  ),
                )
                .toList(),
            onChanged: (value) => setState(() => assetCategory = value),
            validator: (value) =>
                value == null ? 'Select an asset category.' : null,
          ),
          const SizedBox(height: 12),
          TextFormField(
            key: const Key('form-building'),
            controller: buildingController,
            decoration: const InputDecoration(labelText: 'Building'),
          ),
          const SizedBox(height: 12),
          TextFormField(
            key: const Key('form-department'),
            controller: departmentController,
            decoration: const InputDecoration(labelText: 'Department'),
          ),
          const SizedBox(height: 12),
          DropdownButtonFormField<String>(
            key: const Key('form-period-type'),
            initialValue: periodType,
            decoration: const InputDecoration(labelText: 'Period type'),
            items: values.periodTypes
                .map(
                  (option) => DropdownMenuItem(
                    value: option.code,
                    child: Text(option.displayName),
                  ),
                )
                .toList(),
            onChanged: (value) => setState(() => periodType = value),
            validator: (value) =>
                value == null ? 'Select a period type.' : null,
          ),
          const SizedBox(height: 12),
          DropdownButtonFormField<String>(
            key: const Key('form-quarter'),
            initialValue: quarter,
            decoration: const InputDecoration(labelText: 'Quarter'),
            items: [
              const DropdownMenuItem<String>(
                value: null,
                child: Text('Not specified'),
              ),
              ...values.quarters.map(
                (option) => DropdownMenuItem(
                  value: option.code,
                  child: Text(option.displayName),
                ),
              ),
            ],
            onChanged: (value) => setState(() => quarter = value),
          ),
          const SizedBox(height: 12),
          TextFormField(
            key: const Key('form-semester'),
            controller: semesterController,
            decoration: const InputDecoration(labelText: 'Semester'),
          ),
          const SizedBox(height: 12),
          TextFormField(
            key: const Key('form-year'),
            controller: yearController,
            keyboardType: TextInputType.number,
            decoration: const InputDecoration(labelText: 'Year'),
            validator: (value) =>
                value != null &&
                    value.trim().isNotEmpty &&
                    _parseYear(value) == null
                ? 'Enter a whole-number year.'
                : null,
          ),
          const SizedBox(height: 12),
          TextFormField(
            key: const Key('form-academic-year'),
            controller: academicYearController,
            decoration: const InputDecoration(labelText: 'Academic year'),
          ),
          const SizedBox(height: 24),
          FilledButton(
            key: const Key('create-draft-button'),
            onPressed: widget.controller.isSaving ? null : _create,
            child: Text(
              widget.controller.isSaving ? 'Saving...' : 'Create draft',
            ),
          ),
        ],
      ),
    );
  }
}

class PreventiveMaintenanceDraftPage extends StatefulWidget {
  const PreventiveMaintenanceDraftPage({
    super.key,
    required this.controller,
    required this.formId,
    this.preselectedScheduleId,
    this.focusedInspectionId,
  });

  final PreventiveMaintenanceController controller;
  final String formId;
  final String? preselectedScheduleId;
  final String? focusedInspectionId;

  @override
  State<PreventiveMaintenanceDraftPage> createState() =>
      _PreventiveMaintenanceDraftPageState();
}

class _PreventiveMaintenanceDraftPageState
    extends State<PreventiveMaintenanceDraftPage> {
  late Future<List<ScheduleOption>> schedules;
  PreventiveMaintenanceForm? submittedForm;

  @override
  void initState() {
    super.initState();
    schedules = widget.controller.repository.listSchedules();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted || widget.controller.selectedForm?.id == widget.formId) {
        return;
      }
      widget.controller.loadDraft(widget.formId);
    });
  }

  Future<void> _retry() async {
    final nextSchedules = widget.controller.repository.listSchedules();
    setState(() {
      schedules = nextSchedules;
    });
    await widget.controller.loadDraft(widget.formId);
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Draft form')),
      body: AnimatedBuilder(
        animation: widget.controller,
        builder: (context, _) {
          final form = submittedForm ?? widget.controller.selectedForm;
          if (form == null && widget.controller.isLoading) {
            return const Center(child: CircularProgressIndicator());
          }
          if (form == null) {
            return _ErrorState(
              message:
                  widget.controller.errorMessage ??
                  'The draft could not be loaded.',
              onRetry: _retry,
            );
          }
          return FutureBuilder<List<ScheduleOption>>(
            future: schedules,
            builder: (context, snapshot) {
              if (snapshot.connectionState != ConnectionState.done) {
                return const Center(
                  child: CircularProgressIndicator(
                    key: Key('schedules-loading'),
                  ),
                );
              }
              final scheduleError = snapshot.hasError
                  ? friendlyError(snapshot.error!)
                  : null;
              final availableSchedules =
                  snapshot.data ?? const <ScheduleOption>[];
              return _buildEditor(
                context,
                form,
                availableSchedules,
                scheduleError,
              );
            },
          );
        },
      ),
    );
  }

  Widget _buildEditor(
    BuildContext context,
    PreventiveMaintenanceForm form,
    List<ScheduleOption> schedules,
    String? scheduleError,
  ) {
    final canEdit = form.isDraft;
    final matchingSchedules = schedules
        .where((schedule) {
          try {
            return PreventiveMaintenanceGrouping.fromSchedule(
              schedule,
            ).matches(form);
          } on FormatException {
            return false;
          }
        })
        .where(
          (schedule) => !form.inspections.any(
            (inspection) => inspection.scheduleId == schedule.id,
          ),
        )
        .toList(growable: false);
    final preselectedScheduleId =
        matchingSchedules.any(
          (schedule) => schedule.id == widget.preselectedScheduleId,
        )
        ? widget.preselectedScheduleId
        : null;
    final displayedInspections = [...form.inspections];
    final focusedInspectionId = widget.focusedInspectionId;
    if (focusedInspectionId != null) {
      displayedInspections.sort((left, right) {
        if (left.id == focusedInspectionId) return -1;
        if (right.id == focusedInspectionId) return 1;
        return 0;
      });
    }

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        if (widget.controller.errorMessage != null)
          _InlineError(message: widget.controller.errorMessage!),
        _FormMetadata(form: form),
        const SizedBox(height: 16),
        if (!canEdit)
          const Card(
            color: Color(0xFFFFF4E5),
            child: Padding(
              padding: EdgeInsets.all(16),
              child: Text('This form is no longer Draft and cannot be edited.'),
            ),
          ),
        if (scheduleError != null && canEdit)
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Row(
                    children: [
                      Icon(Icons.cloud_off),
                      SizedBox(width: 12),
                      Text('Schedules unavailable'),
                    ],
                  ),
                  const SizedBox(height: 8),
                  Text(scheduleError),
                  Align(
                    alignment: Alignment.centerRight,
                    child: TextButton(
                      key: const Key('schedules-retry'),
                      onPressed: _retry,
                      child: const Text('Retry'),
                    ),
                  ),
                ],
              ),
            ),
          ),
        if (canEdit && scheduleError == null)
          _AddInspectionCard(
            key: ValueKey('add-${form.inspections.length}'),
            schedules: matchingSchedules,
            preselectedScheduleId: preselectedScheduleId,
            inspectorUserId: widget.controller.user.id,
            isSaving: widget.controller.isSaving,
            onAdd: widget.controller.addInspection,
          ),
        const SizedBox(height: 16),
        Text(
          'Inspection rows (${form.inspections.length})',
          style: Theme.of(context).textTheme.titleLarge,
        ),
        const SizedBox(height: 8),
        if (form.inspections.isEmpty)
          const Card(
            child: Padding(
              padding: EdgeInsets.all(16),
              child: Text('No rows yet. Add an inspection row to this draft.'),
            ),
          ),
        ...displayedInspections.map(
          (row) => _InspectionRowEditor(
            key: ValueKey(row.id),
            row: row,
            highlighted: row.id == focusedInspectionId,
            inspectorUserId: widget.controller.user.id,
            isSaving: widget.controller.isSaving,
            editable: canEdit,
            onSave: (input) =>
                widget.controller.updateInspection(row.id, input),
            onDelete: () => widget.controller.deleteInspection(row.id),
          ),
        ),
        if (canEdit) ...[
          const SizedBox(height: 16),
          _SubmitFormCard(
            hasRows: form.inspections.isNotEmpty,
            isSaving: widget.controller.isSaving,
            onSubmit: _confirmSubmit,
          ),
        ],
      ],
    );
  }

  Future<void> _confirmSubmit() async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Submit preventive-maintenance form?'),
        content: const Text(
          'After submission, this form and its inspection rows cannot be edited. The backend will assign a provisional file number.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            key: const Key('confirm-submit-form'),
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Submit form'),
          ),
        ],
      ),
    );
    if (confirmed != true || !mounted) return;

    final submitted = await widget.controller.submitForm();
    if (!mounted || submitted == null) return;
    setState(() => submittedForm = submitted);
    final fileNumber = submitted.fileNumber;
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(
        content: Text(
          fileNumber == null
              ? 'Form submitted.'
              : 'Form submitted with provisional file number $fileNumber.',
        ),
      ),
    );
  }
}

class _DraftReferences {
  const _DraftReferences({
    required this.assetCategories,
    required this.periodTypes,
    required this.quarters,
  });

  final List<ReferenceOption> assetCategories;
  final List<ReferenceOption> periodTypes;
  final List<ReferenceOption> quarters;
}

class _FormMetadata extends StatelessWidget {
  const _FormMetadata({required this.form});

  final PreventiveMaintenanceForm form;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              form.fileNumber ?? 'Unsubmitted draft',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            const SizedBox(height: 8),
            Text('Status: ${form.status}'),
            Text('Asset category: ${form.assetCategory}'),
            Text('Building: ${form.building ?? 'Not recorded'}'),
            Text('Department: ${form.department ?? 'Not recorded'}'),
            Text('Period: ${form.periodType}'),
            if (form.quarter != null) Text('Quarter: ${form.quarter}'),
            if (form.semester != null) Text('Semester: ${form.semester}'),
            if (form.year != null) Text('Year: ${form.year}'),
            if (form.academicYear != null)
              Text('Academic year: ${form.academicYear}'),
          ],
        ),
      ),
    );
  }
}

class _AddInspectionCard extends StatefulWidget {
  const _AddInspectionCard({
    super.key,
    required this.schedules,
    this.preselectedScheduleId,
    required this.inspectorUserId,
    required this.isSaving,
    required this.onAdd,
  });

  final List<ScheduleOption> schedules;
  final String? preselectedScheduleId;
  final String inspectorUserId;
  final bool isSaving;
  final Future<bool> Function(AddInspectionInput input) onAdd;

  @override
  State<_AddInspectionCard> createState() => _AddInspectionCardState();
}

class _AddInspectionCardState extends State<_AddInspectionCard> {
  final formKey = GlobalKey<FormState>();
  final dateController = TextEditingController(text: _dateText(DateTime.now()));
  final remarksController = TextEditingController();
  final actionsController = TextEditingController();
  late String? scheduleId;
  bool isOperational = false;
  String? localError;

  @override
  void initState() {
    super.initState();
    scheduleId = widget.preselectedScheduleId;
  }

  @override
  void dispose() {
    dateController.dispose();
    remarksController.dispose();
    actionsController.dispose();
    super.dispose();
  }

  Future<void> _add() async {
    if (!(formKey.currentState?.validate() ?? false)) return;
    final date = _parseDate(dateController.text);
    if (date == null) {
      setState(() => localError = 'Enter the inspection date as YYYY-MM-DD.');
      return;
    }
    final added = await widget.onAdd(
      AddInspectionInput(
        scheduleId: scheduleId!,
        inspectorUserId: widget.inspectorUserId,
        dateInspected: date,
        isOperational: isOperational,
        remarks: _blankToNull(remarksController.text),
        actionsRecommendations: _blankToNull(actionsController.text),
      ),
    );
    if (added && mounted) {
      setState(() {
        scheduleId = null;
        remarksController.clear();
        actionsController.clear();
        localError = null;
      });
    }
  }

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Form(
          key: formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                'Add inspection row',
                style: Theme.of(context).textTheme.titleMedium,
              ),
              const SizedBox(height: 12),
              if (localError != null) _InlineError(message: localError!),
              if (widget.schedules.isEmpty)
                const Text(
                  'No compatible schedules are available for this Draft.',
                )
              else ...[
                DropdownButtonFormField<String>(
                  key: const Key('inspection-schedule'),
                  initialValue: scheduleId,
                  decoration: const InputDecoration(labelText: 'Schedule'),
                  items: widget.schedules
                      .map(
                        (schedule) => DropdownMenuItem(
                          value: schedule.id,
                          child: Text(
                            '${schedule.asset!.assetCode} - ${_dateText(schedule.scheduleDate)}',
                          ),
                        ),
                      )
                      .toList(),
                  onChanged: (value) => setState(() => scheduleId = value),
                  validator: (value) =>
                      value == null ? 'Select a schedule.' : null,
                ),
                const SizedBox(height: 12),
                TextFormField(
                  key: const Key('new-inspection-date'),
                  controller: dateController,
                  decoration: const InputDecoration(
                    labelText: 'Inspection date (YYYY-MM-DD)',
                  ),
                  validator: (value) => _parseDate(value ?? '') == null
                      ? 'Enter a valid date.'
                      : null,
                ),
                _ConditionSelector(
                  value: isOperational,
                  onChanged: (value) => setState(() => isOperational = value),
                ),
                TextField(
                  key: const Key('new-inspection-remarks'),
                  controller: remarksController,
                  maxLines: 3,
                  decoration: const InputDecoration(labelText: 'Remarks'),
                ),
                const SizedBox(height: 8),
                TextField(
                  key: const Key('new-inspection-actions'),
                  controller: actionsController,
                  maxLines: 3,
                  decoration: const InputDecoration(
                    labelText: 'Recommended corrective action',
                  ),
                ),
                const SizedBox(height: 12),
                FilledButton(
                  key: const Key('add-inspection-button'),
                  onPressed: widget.isSaving ? null : _add,
                  child: Text(widget.isSaving ? 'Saving...' : 'Add row'),
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}

class _InspectionRowEditor extends StatefulWidget {
  const _InspectionRowEditor({
    super.key,
    required this.row,
    required this.highlighted,
    required this.inspectorUserId,
    required this.isSaving,
    required this.editable,
    required this.onSave,
    required this.onDelete,
  });

  final PreventiveMaintenanceInspection row;
  final bool highlighted;
  final String inspectorUserId;
  final bool isSaving;
  final bool editable;
  final Future<bool> Function(UpdateInspectionInput input) onSave;
  final Future<bool> Function() onDelete;

  @override
  State<_InspectionRowEditor> createState() => _InspectionRowEditorState();
}

class _SubmitFormCard extends StatelessWidget {
  const _SubmitFormCard({
    required this.hasRows,
    required this.isSaving,
    required this.onSubmit,
  });

  final bool hasRows;
  final bool isSaving;
  final VoidCallback onSubmit;

  @override
  Widget build(BuildContext context) {
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Submit whole form',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 8),
            const Text(
              'Submission locks the form and its rows for department-head acknowledgement.',
            ),
            if (!hasRows) ...[
              const SizedBox(height: 8),
              const Text('Add at least one inspection row before submitting.'),
            ],
            const SizedBox(height: 12),
            FilledButton(
              key: const Key('submit-form-button'),
              onPressed: isSaving || !hasRows ? null : onSubmit,
              child: Text(isSaving ? 'Submitting...' : 'Submit form'),
            ),
          ],
        ),
      ),
    );
  }
}

class _InspectionRowEditorState extends State<_InspectionRowEditor> {
  final formKey = GlobalKey<FormState>();
  late final TextEditingController dateController;
  late final TextEditingController remarksController;
  late final TextEditingController actionsController;
  late bool isOperational;

  @override
  void initState() {
    super.initState();
    dateController = TextEditingController(
      text: _dateText(widget.row.dateInspected),
    );
    remarksController = TextEditingController(text: widget.row.remarks ?? '');
    actionsController = TextEditingController(
      text: widget.row.actionsRecommendations ?? '',
    );
    isOperational = widget.row.isOperational;
  }

  @override
  void dispose() {
    dateController.dispose();
    remarksController.dispose();
    actionsController.dispose();
    super.dispose();
  }

  Future<void> _save() async {
    if (!(formKey.currentState?.validate() ?? false)) return;
    final date = _parseDate(dateController.text);
    if (date == null) return;
    await widget.onSave(
      UpdateInspectionInput(
        inspectorUserId: widget.inspectorUserId,
        dateInspected: date,
        isOperational: isOperational,
        remarks: _blankToNull(remarksController.text),
        actionsRecommendations: _blankToNull(actionsController.text),
      ),
    );
  }

  Future<void> _confirmDelete() async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Delete inspection row?'),
        content: const Text('This removes the row from the Draft form.'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Delete draft row'),
          ),
        ],
      ),
    );
    if (confirmed == true) await widget.onDelete();
  }

  @override
  Widget build(BuildContext context) {
    return Card(
      color: widget.highlighted
          ? Theme.of(context).colorScheme.primaryContainer
          : null,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Form(
          key: formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                widget.highlighted ? 'Resume inspection row' : 'Inspection row',
                style: Theme.of(context).textTheme.titleMedium,
              ),
              const SizedBox(height: 8),
              Text('Schedule ID: ${widget.row.scheduleId}'),
              Text('Asset ID: ${widget.row.assetId}'),
              Text('Inspector ID: ${widget.row.inspectorUserId}'),
              const SizedBox(height: 12),
              TextFormField(
                key: Key('inspection-date-${widget.row.id}'),
                controller: dateController,
                readOnly: !widget.editable,
                decoration: const InputDecoration(
                  labelText: 'Inspection date (YYYY-MM-DD)',
                ),
                validator: (value) => _parseDate(value ?? '') == null
                    ? 'Enter a valid date.'
                    : null,
              ),
              _ConditionSelector(
                value: isOperational,
                enabled: widget.editable,
                onChanged: (value) => setState(() => isOperational = value),
              ),
              TextField(
                key: Key('inspection-remarks-${widget.row.id}'),
                controller: remarksController,
                enabled: widget.editable,
                maxLines: 3,
                decoration: const InputDecoration(labelText: 'Remarks'),
              ),
              const SizedBox(height: 8),
              TextField(
                key: Key('inspection-actions-${widget.row.id}'),
                controller: actionsController,
                enabled: widget.editable,
                maxLines: 3,
                decoration: const InputDecoration(
                  labelText: 'Recommended corrective action',
                ),
              ),
              if (widget.editable) ...[
                const SizedBox(height: 12),
                Row(
                  children: [
                    FilledButton(
                      key: Key('save-inspection-${widget.row.id}'),
                      onPressed: widget.isSaving ? null : _save,
                      child: const Text('Save row'),
                    ),
                    const SizedBox(width: 8),
                    TextButton(
                      onPressed: widget.isSaving ? null : _confirmDelete,
                      child: const Text('Delete row'),
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

class _ConditionSelector extends StatelessWidget {
  const _ConditionSelector({
    required this.value,
    required this.onChanged,
    this.enabled = true,
  });

  final bool value;
  final bool enabled;
  final ValueChanged<bool> onChanged;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        const SizedBox(height: 8),
        const Text('Condition'),
        SegmentedButton<bool>(
          segments: const [
            ButtonSegment<bool>(value: true, label: Text('Operational')),
            ButtonSegment<bool>(value: false, label: Text('Non-operational')),
          ],
          selected: {value},
          onSelectionChanged: enabled
              ? (selection) => onChanged(selection.single)
              : null,
          expandedInsets: EdgeInsets.zero,
        ),
      ],
    );
  }
}

class _InlineError extends StatelessWidget {
  const _InlineError({required this.message});

  final String message;

  @override
  Widget build(BuildContext context) {
    return Card(
      color: const Color(0xFFFFEBEE),
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Text(message, key: const Key('draft-form-error')),
      ),
    );
  }
}

String? _blankToNull(String value) {
  final normalized = value.trim();
  return normalized.isEmpty ? null : normalized;
}

int? _parseYear(String value) {
  final normalized = value.trim();
  return normalized.isEmpty ? null : int.tryParse(normalized);
}

DateTime? _parseDate(String value) {
  final match = RegExp(r'^([0-9]{4})-([0-9]{2})-([0-9]{2})$').firstMatch(value);
  if (match == null) return null;

  final year = int.parse(match.group(1)!);
  final month = int.parse(match.group(2)!);
  final day = int.parse(match.group(3)!);
  if (year < 1) return null;

  final parsed = DateTime(year, month, day);
  if (parsed.year != year || parsed.month != month || parsed.day != day) {
    return null;
  }
  return parsed;
}

String _dateText(DateTime value) {
  final local = value.toLocal();
  final month = local.month.toString().padLeft(2, '0');
  final day = local.day.toString().padLeft(2, '0');
  return '${local.year}-$month-$day';
}
