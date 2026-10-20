import 'dart:convert';
import 'dart:typed_data';

import 'package:flutter/material.dart';

import '../../ui/display_labels.dart';
import 'preventive_maintenance_controller.dart';
import 'preventive_maintenance_models.dart';

String _formStatusLabel(String status) =>
    status == 'Submitted' ? 'Awaiting acknowledgement' : status;

class PreventiveMaintenanceAcknowledgementPage extends StatefulWidget {
  const PreventiveMaintenanceAcknowledgementPage({
    super.key,
    required this.controller,
    required this.form,
  });

  final PreventiveMaintenanceController controller;
  final PreventiveMaintenanceForm form;

  @override
  State<PreventiveMaintenanceAcknowledgementPage> createState() =>
      _PreventiveMaintenanceAcknowledgementPageState();
}

class _PreventiveMaintenanceAcknowledgementPageState
    extends State<PreventiveMaintenanceAcknowledgementPage> {
  final formKey = GlobalKey<FormState>();
  final signaturePadKey = GlobalKey<PmSignaturePadState>();
  final signatoryNameController = TextEditingController();
  final signatoryPositionController = TextEditingController();
  String? localError;
  bool isSignatureGestureActive = false;

  @override
  void dispose() {
    signatoryNameController.dispose();
    signatoryPositionController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return AnimatedBuilder(
      animation: widget.controller,
      builder: (context, _) {
        final selectedForm = widget.controller.selectedForm;
        final form = selectedForm?.id == widget.form.id
            ? selectedForm!
            : widget.form;
        final isAcknowledged =
            form.status == 'Acknowledged' ||
            widget.controller.acknowledgement != null;

        return Scaffold(
          appBar: AppBar(
            leading: IconButton(
              tooltip: 'Back to PM forms',
              icon: const Icon(Icons.arrow_back),
              onPressed: () => Navigator.of(context).maybePop(),
            ),
            title: Text(
              isAcknowledged ? 'PM form details' : 'Acknowledge PM form',
            ),
          ),
          body: SafeArea(
            child: _buildContent(context, form, isAcknowledged),
          ),
        );
      },
    );
  }

  Widget _buildContent(
    BuildContext context,
    PreventiveMaintenanceForm form,
    bool isAcknowledged,
  ) {
    final acknowledgement = widget.controller.acknowledgement;

    return ListView(
      key: const Key('acknowledgement-page'),
      padding: const EdgeInsets.all(16),
      physics: isSignatureGestureActive
          ? const NeverScrollableScrollPhysics()
          : null,
      children: [
        _SubmittedFormSummary(form: form),
        const SizedBox(height: 16),
        if (widget.controller.errorMessage != null && !isAcknowledged)
          _AcknowledgementInlineError(message: widget.controller.errorMessage!),
        if (isAcknowledged)
          _AcknowledgementReceipt(
            acknowledgement: acknowledgement,
            formNumber: form.fileNumber ?? 'Preventive-maintenance form',
          )
        else if (form.status == 'Submitted')
          _AcknowledgementForm(
            formKey: formKey,
            signaturePadKey: signaturePadKey,
            signatoryNameController: signatoryNameController,
            signatoryPositionController: signatoryPositionController,
            localError: localError,
            isSaving: widget.controller.isSaving,
            onSignatureGestureChanged: (isActive) {
              if (isSignatureGestureActive != isActive) {
                setState(() => isSignatureGestureActive = isActive);
              }
            },
            onSignatureChanged: (hasSignature) {
              if (hasSignature && localError != null) {
                setState(() => localError = null);
              }
            },
            onAcknowledge: _confirmAcknowledge,
          )
        else
          const Card(
            child: Padding(
              padding: EdgeInsets.all(16),
              child: Text(
                'This form is not awaiting acknowledgement and cannot be acknowledged.',
              ),
            ),
          ),
      ],
    );
  }

  Future<void> _confirmAcknowledge() async {
    if (!(formKey.currentState?.validate() ?? false)) return;

    final signatureData = await signaturePadKey.currentState?.toPngBase64();
    if (!mounted) return;
    if (signatureData == null) {
      setState(() => localError = 'Capture the Department Head signature.');
      return;
    }

    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Acknowledge whole PM form?'),
        content: const Text(
          'This records receipt of the findings, locks the form, and makes its '
          'inspection rows eligible for official history. It does not '
          'authorize repairs, funding, or an RMRF.',
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('Cancel'),
          ),
          FilledButton(
            key: const Key('confirm-acknowledge-form'),
            onPressed: () => Navigator.pop(context, true),
            child: const Text('Acknowledge form'),
          ),
        ],
      ),
    );
    if (confirmed != true || !mounted) return;

    await widget.controller.acknowledgeForm(
      AcknowledgePreventiveMaintenanceInput(
        signatoryName: signatoryNameController.text.trim(),
        signatoryPosition: signatoryPositionController.text.trim(),
        signatureData: signatureData,
        signatureContentType: 'image/png',
      ),
    );
    if (mounted) setState(() {});
  }
}

class _SubmittedFormSummary extends StatelessWidget {
  const _SubmittedFormSummary({required this.form});

  final PreventiveMaintenanceForm form;

  @override
  Widget build(BuildContext context) {
    final total = form.inspections.length;
    final operational = form.inspections.where((row) => row.isOperational).length;
    final nonOperational = total - operational;
    final department = _displayValue(form.department);
    final building = _displayValue(form.building);

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  form.fileNumber ?? 'Preventive-maintenance form',
                  style: Theme.of(context).textTheme.titleLarge,
                  softWrap: true,
                ),
                const SizedBox(height: 6),
                _FormStatusBadge(label: _formStatusLabel(form.status)),
                const SizedBox(height: 8),
                Text(
                  displayAssetCategory(form.assetCategory),
                  style: Theme.of(context).textTheme.titleSmall,
                  softWrap: true,
                ),
                const SizedBox(height: 3),
                Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Icon(
                      Icons.apartment_outlined,
                      size: 18,
                      color: Theme.of(context).colorScheme.onSurfaceVariant,
                    ),
                    const SizedBox(width: 6),
                    Expanded(
                      child: Text(
                        '$department · $building',
                        style: Theme.of(context).textTheme.bodyMedium,
                        softWrap: true,
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 14),
                Text(
                  'Inspection totals',
                  style: Theme.of(context).textTheme.labelLarge,
                ),
                const SizedBox(height: 8),
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: [
                    _InspectionCount(label: 'Total', value: total),
                    _InspectionCount(label: 'Operational', value: operational),
                    _InspectionCount(
                      label: 'Non-operational',
                      value: nonOperational,
                      isWarning: nonOperational > 0,
                    ),
                  ],
                ),
              ],
            ),
          ),
        ),
        const SizedBox(height: 16),
        Text(
          'Inspection findings',
          style: Theme.of(context).textTheme.titleMedium,
        ),
        const SizedBox(height: 4),
        ...form.inspections.map((row) => _InspectionSummary(row: row)),
      ],
    );
  }
}

class _FormStatusBadge extends StatelessWidget {
  const _FormStatusBadge({required this.label});

  final String label;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    final isAcknowledged = label == 'Acknowledged';
    final background = isAcknowledged
        ? colorScheme.tertiaryContainer
        : colorScheme.primaryContainer;
    final foreground = isAcknowledged
        ? colorScheme.onTertiaryContainer
        : colorScheme.onPrimaryContainer;

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
      decoration: BoxDecoration(
        color: background,
        borderRadius: BorderRadius.circular(20),
      ),
      child: Text(
        label,
        style: Theme.of(context).textTheme.labelMedium?.copyWith(
          color: foreground,
          fontWeight: FontWeight.w600,
        ),
      ),
    );
  }
}

class _InspectionCount extends StatelessWidget {
  const _InspectionCount({
    required this.label,
    required this.value,
    this.isWarning = false,
  });

  final String label;
  final int value;
  final bool isWarning;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    return Container(
      constraints: const BoxConstraints(minWidth: 76),
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
      decoration: BoxDecoration(
        color: isWarning
            ? colorScheme.errorContainer.withValues(alpha: 0.55)
            : colorScheme.surfaceContainerHigh,
        borderRadius: BorderRadius.circular(8),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Text('$value', style: Theme.of(context).textTheme.titleSmall),
          Text(
            label,
            style: Theme.of(context).textTheme.labelSmall?.copyWith(
              color: isWarning
                  ? colorScheme.onErrorContainer
                  : colorScheme.onSurfaceVariant,
            ),
          ),
        ],
      ),
    );
  }
}

class _InspectionSummary extends StatefulWidget {
  const _InspectionSummary({required this.row});

  final PreventiveMaintenanceInspection row;

  @override
  State<_InspectionSummary> createState() => _InspectionSummaryState();
}

class _InspectionSummaryState extends State<_InspectionSummary> {
  bool isExpanded = false;

  @override
  Widget build(BuildContext context) {
    final row = widget.row;
    final colorScheme = Theme.of(context).colorScheme;

    return Column(
      children: [
        Semantics(
          button: true,
          expanded: isExpanded,
          child: InkWell(
            key: Key('inspection-row-' + row.id),
            onTap: () => setState(() => isExpanded = !isExpanded),
            child: Padding(
              padding: const EdgeInsets.symmetric(vertical: 12),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          _assetCodeLabel(row.assetCode),
                          style: Theme.of(context).textTheme.titleSmall,
                          softWrap: true,
                        ),
                        const SizedBox(height: 2),
                        Text(
                          _displayValue(row.location),
                          style: Theme.of(context).textTheme.bodySmall?.copyWith(
                            color: colorScheme.onSurfaceVariant,
                          ),
                          softWrap: true,
                        ),
                        const SizedBox(height: 6),
                        _ConditionBadge(isOperational: row.isOperational),
                        if (!row.isOperational && !isExpanded)
                          _NonOperationalPreview(row: row),
                      ],
                    ),
                  ),
                  const SizedBox(width: 8),
                  Padding(
                    padding: const EdgeInsets.only(top: 2),
                    child: Icon(
                      isExpanded ? Icons.expand_less : Icons.expand_more,
                      color: colorScheme.onSurfaceVariant,
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
        if (isExpanded)
          Padding(
            padding: const EdgeInsets.only(left: 8, right: 36, bottom: 12),
            child: _InspectionDetails(row: row),
          ),
        const Divider(height: 1),
      ],
    );
  }
}

class _ConditionBadge extends StatelessWidget {
  const _ConditionBadge({required this.isOperational});

  final bool isOperational;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    final color = isOperational
        ? colorScheme.surfaceContainerHigh
        : colorScheme.errorContainer.withValues(alpha: 0.7);
    final foreground = isOperational
        ? colorScheme.onSurfaceVariant
        : colorScheme.onErrorContainer;

    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
      decoration: BoxDecoration(
        color: color,
        borderRadius: BorderRadius.circular(20),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(
            isOperational ? Icons.check_circle_outline : Icons.warning_amber,
            size: 15,
            color: foreground,
          ),
          const SizedBox(width: 4),
          Text(
            isOperational ? 'Operational' : 'Non-operational',
            style: Theme.of(context).textTheme.labelSmall?.copyWith(
              color: foreground,
              fontWeight: FontWeight.w600,
            ),
          ),
        ],
      ),
    );
  }
}

class _NonOperationalPreview extends StatelessWidget {
  const _NonOperationalPreview({required this.row});

  final PreventiveMaintenanceInspection row;

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    final labelStyle = Theme.of(context).textTheme.labelSmall?.copyWith(
      color: colorScheme.error,
      fontWeight: FontWeight.w600,
    );

    return Container(
      margin: const EdgeInsets.only(top: 8),
      padding: const EdgeInsets.only(left: 8),
      decoration: BoxDecoration(
        border: Border(left: BorderSide(color: colorScheme.error, width: 2)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('Finding', style: labelStyle),
          Text(_displayValue(row.remarks)),
          const SizedBox(height: 6),
          Text('Recommendation', style: labelStyle),
          Text(_displayValue(row.actionsRecommendations)),
        ],
      ),
    );
  }
}

class _InspectionDetails extends StatelessWidget {
  const _InspectionDetails({required this.row});

  final PreventiveMaintenanceInspection row;

  @override
  Widget build(BuildContext context) {
    final details = <Widget>[
      _InspectionDetail(label: 'Inspection date', value: _dateText(row.dateInspected)),
      _InspectionDetail(label: 'Remarks', value: _displayValue(row.remarks)),
      _InspectionDetail(
        label: 'Recommendations',
        value: _displayValue(row.actionsRecommendations),
      ),
    ];
    if (row.startedAt != null) {
      details.add(
        _InspectionDetail(label: 'Started', value: _dateTimeText(row.startedAt!)),
      );
    }
    if (row.completedAt != null) {
      details.add(
        _InspectionDetail(label: 'Completed', value: _dateTimeText(row.completedAt!)),
      );
    }
    if (row.dateAccomplished != null) {
      details.add(
        _InspectionDetail(
          label: 'Date accomplished',
          value: _dateText(row.dateAccomplished!),
        ),
      );
    }
    if (row.waterReplaceCarbonFilter != null ||
        row.waterReplaceSedimentFilter != null ||
        row.waterCheckUvLight != null) {
      details.add(const SizedBox(height: 8));
      details.add(
        Text(
          'Water station checks',
          style: Theme.of(context).textTheme.labelLarge,
        ),
      );
      if (row.waterReplaceCarbonFilter != null) {
        details.add(
          _InspectionDetail(
            label: 'Carbon filter replaced',
            value: row.waterReplaceCarbonFilter! ? 'Yes' : 'No',
          ),
        );
      }
      if (row.waterReplaceSedimentFilter != null) {
        details.add(
          _InspectionDetail(
            label: 'Sediment filter replaced',
            value: row.waterReplaceSedimentFilter! ? 'Yes' : 'No',
          ),
        );
      }
      if (row.waterCheckUvLight != null) {
        details.add(
          _InspectionDetail(
            label: 'UV light checked',
            value: row.waterCheckUvLight! ? 'Yes' : 'No',
          ),
        );
      }
    }
    if (row.hasPhotoEvidence) {
      details.add(
        const Padding(
          padding: EdgeInsets.only(top: 8),
          child: Row(
            children: [
              Icon(Icons.photo_outlined, size: 18),
              SizedBox(width: 6),
              Text('Photo evidence attached'),
            ],
          ),
        ),
      );
    }

    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: details,
    );
  }
}

class _InspectionDetail extends StatelessWidget {
  const _InspectionDetail({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    return Padding(
      padding: const EdgeInsets.only(top: 6),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            label,
            style: Theme.of(context).textTheme.labelSmall?.copyWith(
              color: Theme.of(context).colorScheme.onSurfaceVariant,
            ),
          ),
          Text(value, softWrap: true),
        ],
      ),
    );
  }
}

class _AcknowledgementForm extends StatelessWidget {
  const _AcknowledgementForm({
    required this.formKey,
    required this.signaturePadKey,
    required this.signatoryNameController,
    required this.signatoryPositionController,
    required this.localError,
    required this.isSaving,
    required this.onSignatureGestureChanged,
    required this.onSignatureChanged,
    required this.onAcknowledge,
  });

  final GlobalKey<FormState> formKey;
  final GlobalKey<PmSignaturePadState> signaturePadKey;
  final TextEditingController signatoryNameController;
  final TextEditingController signatoryPositionController;
  final String? localError;
  final bool isSaving;
  final ValueChanged<bool> onSignatureGestureChanged;
  final ValueChanged<bool> onSignatureChanged;
  final VoidCallback onAcknowledge;

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
                'Department Head acknowledgement',
                style: Theme.of(context).textTheme.titleMedium,
              ),
              const SizedBox(height: 4),
              Text(
                'Enter the signatory details and capture their signature.',
                style: Theme.of(context).textTheme.bodySmall?.copyWith(
                  color: Theme.of(context).colorScheme.onSurfaceVariant,
                ),
              ),
              const SizedBox(height: 16),
              TextFormField(
                key: const Key('ack-signatory-name'),
                controller: signatoryNameController,
                enabled: !isSaving,
                textInputAction: TextInputAction.next,
                decoration: const InputDecoration(
                  labelText: 'Full name',
                ),
                validator: (value) => value == null || value.trim().isEmpty
                    ? 'Enter the Department Head name.'
                    : null,
              ),
              const SizedBox(height: 12),
              TextFormField(
                key: const Key('ack-signatory-position'),
                controller: signatoryPositionController,
                enabled: !isSaving,
                decoration: const InputDecoration(
                  labelText: 'Position',
                ),
                validator: (value) => value == null || value.trim().isEmpty
                    ? 'Enter the Department Head position.'
                    : null,
              ),
              const SizedBox(height: 16),
              Text(
                'Department Head signature',
                style: Theme.of(context).textTheme.labelLarge,
              ),
              const SizedBox(height: 8),
              Text(
                'Sign inside the box',
                style: Theme.of(context).textTheme.bodySmall?.copyWith(
                  color: Theme.of(context).colorScheme.onSurfaceVariant,
                ),
              ),
              const SizedBox(height: 6),
              PmSignaturePad(
                key: signaturePadKey,
                enabled: !isSaving,
                onGestureChanged: onSignatureGestureChanged,
                onChanged: onSignatureChanged,
              ),
              const SizedBox(height: 4),
              Text(
                'Acknowledgement confirms receipt of the inspection findings. '
                'It does not authorize repairs.',
                style: Theme.of(context).textTheme.bodySmall?.copyWith(
                  color: Theme.of(context).colorScheme.onSurfaceVariant,
                ),
              ),
              if (localError != null) ...[
                const SizedBox(height: 8),
                _AcknowledgementInlineError(message: localError!),
              ],
              const SizedBox(height: 16),
              FilledButton(
                key: const Key('acknowledge-form-button'),
                onPressed: isSaving ? null : onAcknowledge,
                child: Text(isSaving ? 'Acknowledging...' : 'Acknowledge form'),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _AcknowledgementReceipt extends StatelessWidget {
  const _AcknowledgementReceipt({
    required this.acknowledgement,
    required this.formNumber,
  });

  final PreventiveMaintenanceAcknowledgement? acknowledgement;
  final String formNumber;

  @override
  Widget build(BuildContext context) {
    if (acknowledgement == null) {
      return Card(
        key: Key('acknowledgement-receipt'),
        color: Theme.of(context).colorScheme.surfaceContainerLow,
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              const _AcknowledgedHeading(),
              const SizedBox(height: 8),
              Text(formNumber, style: Theme.of(context).textTheme.titleMedium),
              const SizedBox(height: 8),
              const _FormStatusBadge(label: 'Acknowledged'),
              const SizedBox(height: 12),
              const _ReadOnlyNote(),
            ],
          ),
        ),
      );
    }

    final value = acknowledgement!;
    return Card(
      key: const Key('acknowledgement-receipt'),
      color: Theme.of(context).colorScheme.surfaceContainerLow,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const _AcknowledgedHeading(),
            const SizedBox(height: 8),
            Text(formNumber, style: Theme.of(context).textTheme.titleMedium),
            const SizedBox(height: 8),
            const _FormStatusBadge(label: 'Acknowledged'),
            const SizedBox(height: 12),
            _InspectionDetail(
              label: 'Department Head',
              value: value.signatoryName,
            ),
            _InspectionDetail(label: 'Position', value: value.signatoryPosition),
            _InspectionDetail(
              label: 'Acknowledged',
              value: _dateTimeText(value.acknowledgedAt),
            ),
            const SizedBox(height: 12),
            const _ReadOnlyNote(),
            const SizedBox(height: 6),
            Text(
              'Receipt only. This does not authorize repairs.',
              style: Theme.of(context).textTheme.bodySmall?.copyWith(
                color: Theme.of(context).colorScheme.onSurfaceVariant,
              ),
            ),
          ],
        ),
      ),
    );
  }
}

class _AcknowledgedHeading extends StatelessWidget {
  const _AcknowledgedHeading();

  @override
  Widget build(BuildContext context) {
    final colorScheme = Theme.of(context).colorScheme;
    return Row(
      children: [
        Icon(Icons.check_circle, color: colorScheme.tertiary, size: 24),
        const SizedBox(width: 8),
        Expanded(
          child: Text(
            'Form acknowledged',
            style: Theme.of(context).textTheme.titleLarge,
            softWrap: true,
          ),
        ),
      ],
    );
  }
}

class _ReadOnlyNote extends StatelessWidget {
  const _ReadOnlyNote();

  @override
  Widget build(BuildContext context) {
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(
          Icons.lock_outline,
          size: 18,
          color: Theme.of(context).colorScheme.onSurfaceVariant,
        ),
        const SizedBox(width: 6),
        Expanded(
          child: Text(
            'This form is now read-only.',
            style: Theme.of(context).textTheme.bodyMedium,
          ),
        ),
      ],
    );
  }
}

class _AcknowledgementInlineError extends StatelessWidget {
  const _AcknowledgementInlineError({required this.message});

  final String message;

  @override
  Widget build(BuildContext context) {
    return Text(
      message,
      key: const Key('acknowledgement-error'),
      style: TextStyle(color: Theme.of(context).colorScheme.error),
    );
  }
}

class PmSignaturePad extends StatefulWidget {
  const PmSignaturePad({
    super.key,
    required this.onChanged,
    required this.onGestureChanged,
    this.enabled = true,
  });

  final ValueChanged<bool> onChanged;
  final ValueChanged<bool> onGestureChanged;
  final bool enabled;

  @override
  PmSignaturePadState createState() => PmSignaturePadState();
}

class PmSignaturePadState extends State<PmSignaturePad> {
  final surfaceKey = GlobalKey();
  final repaintKey = GlobalKey();
  final strokes = <List<Offset>>[];
  final ignoredPointers = <int>{};
  int? activePointer;

  bool get hasSignature => strokes.any((stroke) => stroke.isNotEmpty);

  bool get canUndo {
    for (var index = strokes.length - 1; index >= 0; index--) {
      if (strokes[index].isEmpty) continue;
      if (activePointer != null && index == strokes.length - 1) continue;
      return true;
    }
    return false;
  }

  Offset? _positionInSurface(PointerEvent event) {
    final renderObject = surfaceKey.currentContext?.findRenderObject();
    if (renderObject is! RenderBox || !renderObject.hasSize) return null;

    final localPosition = renderObject.globalToLocal(event.position);
    final bounds = Offset.zero & renderObject.size;
    return bounds.contains(localPosition) ? localPosition : null;
  }

  void _startStroke(PointerDownEvent details) {
    final position = _positionInSurface(details);
    if (!widget.enabled || position == null) return;
    if (activePointer != null || ignoredPointers.isNotEmpty) {
      ignoredPointers.add(details.pointer);
      return;
    }

    activePointer = details.pointer;
    setState(() => strokes.add([position]));
    widget.onChanged(true);
    widget.onGestureChanged(true);
  }

  void _updateStroke(PointerMoveEvent details) {
    if (ignoredPointers.contains(details.pointer) ||
        details.pointer != activePointer ||
        strokes.isEmpty) {
      return;
    }
    if (!widget.enabled) {
      _cancelActiveStroke(details.pointer);
      return;
    }

    final position = _positionInSurface(details);
    if (position == null) {
      activePointer = null;
      ignoredPointers.add(details.pointer);
      setState(() {});
      widget.onGestureChanged(false);
      return;
    }
    setState(() => strokes.last.add(position));
  }

  void _endStroke(PointerUpEvent details) {
    if (ignoredPointers.remove(details.pointer)) return;
    if (details.pointer != activePointer || strokes.isEmpty) {
      return;
    }
    activePointer = null;
    setState(() {});
    widget.onGestureChanged(false);
  }

  void _cancelStroke(PointerCancelEvent details) {
    if (ignoredPointers.remove(details.pointer)) return;
    _cancelActiveStroke(details.pointer);
  }

  void _cancelActiveStroke(int pointer) {
    if (pointer != activePointer) return;
    activePointer = null;
    if (strokes.isNotEmpty) setState(strokes.removeLast);
    widget.onChanged(hasSignature);
    widget.onGestureChanged(false);
  }

  void clear() {
    if (!widget.enabled) return;
    final active = activePointer;
    activePointer = null;
    if (active != null) ignoredPointers.add(active);
    setState(strokes.clear);
    widget.onChanged(false);
    if (active != null) widget.onGestureChanged(false);
  }

  void undo() {
    if (!widget.enabled || !canUndo) return;
    var index = strokes.length - 1;
    while (index >= 0 &&
        (strokes[index].isEmpty ||
            (activePointer != null && index == strokes.length - 1))) {
      index--;
    }
    if (index < 0) return;
    setState(() => strokes.removeAt(index));
    widget.onChanged(hasSignature);
  }

  Future<String?> toPngBase64() async {
    if (!hasSignature) return null;
    final surfaceObject = surfaceKey.currentContext?.findRenderObject();
    final repaintObject = repaintKey.currentContext?.findRenderObject();
    if (surfaceObject is! RenderBox ||
        repaintObject is! RenderBox ||
        !surfaceObject.hasSize ||
        !repaintObject.hasSize) {
      return null;
    }
    final logicalSize = repaintObject.size;
    if (logicalSize != surfaceObject.size) return null;
    if (logicalSize.width <= 0 || logicalSize.height <= 0) return null;
    return base64Encode(_encodeSignaturePng(logicalSize, strokes));
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        SizedBox(
          key: const Key('signature-canvas'),
          height: 180,
          width: double.infinity,
          child: Container(
            clipBehavior: Clip.antiAlias,
            decoration: BoxDecoration(
              color: Theme.of(context).colorScheme.surfaceContainerHigh,
              border: Border.all(
                color: Theme.of(context).colorScheme.outlineVariant,
              ),
              borderRadius: BorderRadius.circular(10),
            ),
            child: Listener(
              key: surfaceKey,
              behavior: HitTestBehavior.opaque,
              onPointerDown: _startStroke,
              onPointerMove: _updateStroke,
              onPointerUp: _endStroke,
              onPointerCancel: _cancelStroke,
              child: Stack(
                fit: StackFit.expand,
                children: [
                  IgnorePointer(
                    child: ExcludeSemantics(
                      child: Opacity(
                        opacity: 0.07,
                        child: ColorFiltered(
                          colorFilter: const ColorFilter.mode(
                            Colors.white,
                            BlendMode.srcIn,
                          ),
                          child: Image.asset(
                            'assets/unipm-logo.png',
                            width: 112,
                            height: 112,
                            fit: BoxFit.contain,
                          ),
                        ),
                      ),
                    ),
                  ),
                  RepaintBoundary(
                    key: repaintKey,
                    child: CustomPaint(
                      painter: _SignaturePainter(strokes),
                      child: const SizedBox.expand(),
                    ),
                  ),
                  if (!hasSignature)
                    const IgnorePointer(
                      child: Center(
                        child: Text(
                          'Sign here',
                          key: Key('signature-placeholder'),
                        ),
                      ),
                    ),
                ],
              ),
            ),
          ),
        ),
        Row(
          mainAxisAlignment: MainAxisAlignment.end,
          children: [
            TextButton.icon(
              key: const Key('undo-signature'),
              onPressed: widget.enabled && canUndo ? undo : null,
              icon: const Icon(Icons.undo),
              label: const Text('Undo'),
            ),
            TextButton.icon(
              key: const Key('clear-signature'),
              onPressed: widget.enabled && hasSignature ? clear : null,
              icon: const Icon(Icons.delete_outline),
              label: const Text('Clear'),
            ),
          ],
        ),
      ],
    );
  }
}

class _SignaturePainter extends CustomPainter {
  _SignaturePainter(this.strokes);

  final List<List<Offset>> strokes;

  @override
  void paint(Canvas canvas, Size size) {
    canvas.save();
    canvas.clipRect(Offset.zero & size);

    final paint = Paint()
      ..color = Colors.black
      ..strokeWidth = 3
      ..strokeCap = StrokeCap.round
      ..style = PaintingStyle.stroke;

    for (final stroke in strokes) {
      if (stroke.isEmpty) continue;
      if (stroke.length == 1) {
        canvas.drawCircle(stroke.single, 1.5, paint);
        continue;
      }
      for (var index = 1; index < stroke.length; index++) {
        canvas.drawLine(stroke[index - 1], stroke[index], paint);
      }
    }

    canvas.restore();
  }

  @override
  bool shouldRepaint(covariant _SignaturePainter oldDelegate) => true;
}

String _displayValue(String? value) {
  final normalized = value?.trim();
  return normalized == null || normalized.isEmpty ? 'Not recorded' : normalized;
}

String _assetCodeLabel(String? value) {
  final normalized = value?.trim();
  return normalized == null || normalized.isEmpty
      ? 'Asset details unavailable'
      : normalized;
}

String _dateText(DateTime value) {
  final local = value.toLocal();
  final month = local.month.toString().padLeft(2, '0');
  final day = local.day.toString().padLeft(2, '0');
  return '${local.year}-$month-$day';
}

String _dateTimeText(DateTime value) {
  final local = value.toLocal();
  final month = local.month.toString().padLeft(2, '0');
  final day = local.day.toString().padLeft(2, '0');
  final hour = local.hour.toString().padLeft(2, '0');
  final minute = local.minute.toString().padLeft(2, '0');
  return '${local.year}-$month-$day $hour:$minute';
}

Uint8List _encodeSignaturePng(Size logicalSize, List<List<Offset>> strokes) {
  var width = logicalSize.width.ceil();
  var height = logicalSize.height.ceil();
  if (width <= 0) width = 480;
  if (height <= 0) height = 180;
  final pixels = Uint8List(width * height)..fillRange(0, width * height, 255);
  final sourceWidth = logicalSize.width > 0 ? logicalSize.width : width;
  final sourceHeight = logicalSize.height > 0 ? logicalSize.height : height;
  final scaleX = width / sourceWidth;
  final scaleY = height / sourceHeight;

  void drawPoint(double x, double y) {
    final centerX = x.round();
    final centerY = y.round();
    for (var offsetY = -2; offsetY <= 2; offsetY++) {
      for (var offsetX = -2; offsetX <= 2; offsetX++) {
        if (offsetX * offsetX + offsetY * offsetY > 5) continue;
        final pixelX = centerX + offsetX;
        final pixelY = centerY + offsetY;
        if (pixelX < 0 || pixelX >= width || pixelY < 0 || pixelY >= height) {
          continue;
        }
        pixels[pixelY * width + pixelX] = 0;
      }
    }
  }

  void drawSegment(Offset start, Offset end) {
    final startX = start.dx * scaleX;
    final startY = start.dy * scaleY;
    final endX = end.dx * scaleX;
    final endY = end.dy * scaleY;
    final distance = (endX - startX).abs() > (endY - startY).abs()
        ? (endX - startX).abs()
        : (endY - startY).abs();
    final steps = distance.ceil();
    if (steps == 0) {
      drawPoint(startX, startY);
      return;
    }
    for (var step = 0; step <= steps; step++) {
      final fraction = step / steps;
      drawPoint(
        startX + (endX - startX) * fraction,
        startY + (endY - startY) * fraction,
      );
    }
  }

  for (final stroke in strokes) {
    if (stroke.isEmpty) continue;
    if (stroke.length == 1) {
      drawPoint(stroke.single.dx * scaleX, stroke.single.dy * scaleY);
      continue;
    }
    for (var index = 1; index < stroke.length; index++) {
      drawSegment(stroke[index - 1], stroke[index]);
    }
  }

  final scanlines = Uint8List((width + 1) * height);
  for (var row = 0; row < height; row++) {
    final rowStart = row * (width + 1);
    scanlines[rowStart] = 0;
    scanlines.setRange(rowStart + 1, rowStart + width + 1, pixels, row * width);
  }

  final png = BytesBuilder();
  png.add(const [137, 80, 78, 71, 13, 10, 26, 10]);
  final header = BytesBuilder();
  _addUint32(header, width);
  _addUint32(header, height);
  header.add(const [8, 0, 0, 0, 0]);
  png.add(_pngChunk('IHDR', header.takeBytes()));
  png.add(_pngChunk('IDAT', _zlibStore(scanlines)));
  png.add(_pngChunk('IEND', Uint8List(0)));
  return png.takeBytes();
}

Uint8List _zlibStore(Uint8List data) {
  final output = BytesBuilder()..add(const [0x78, 0x01]);
  var offset = 0;
  do {
    final length = (data.length - offset).clamp(0, 65535);
    final isFinal = offset + length == data.length;
    final inverseLength = (~length) & 0xffff;
    output.add([
      isFinal ? 0x01 : 0x00,
      length & 0xff,
      (length >> 8) & 0xff,
      inverseLength & 0xff,
      (inverseLength >> 8) & 0xff,
    ]);
    output.add(data.sublist(offset, offset + length));
    offset += length;
  } while (offset < data.length);

  final checksum = _adler32(data);
  output.add([
    (checksum >> 24) & 0xff,
    (checksum >> 16) & 0xff,
    (checksum >> 8) & 0xff,
    checksum & 0xff,
  ]);
  return output.takeBytes();
}

Uint8List _pngChunk(String type, Uint8List data) {
  final output = BytesBuilder();
  _addUint32(output, data.length);
  final typeBytes = type.codeUnits;
  output.add(typeBytes);
  output.add(data);
  _addUint32(output, _crc32([...typeBytes, ...data]));
  return output.takeBytes();
}

void _addUint32(BytesBuilder builder, int value) {
  builder.add([
    (value >> 24) & 0xff,
    (value >> 16) & 0xff,
    (value >> 8) & 0xff,
    value & 0xff,
  ]);
}

int _adler32(List<int> bytes) {
  var sumA = 1;
  var sumB = 0;
  for (final byte in bytes) {
    sumA = (sumA + byte) % 65521;
    sumB = (sumB + sumA) % 65521;
  }
  return (sumB << 16) | sumA;
}

int _crc32(List<int> bytes) {
  var crc = 0xffffffff;
  for (final byte in bytes) {
    crc ^= byte;
    for (var bit = 0; bit < 8; bit++) {
      crc = (crc & 1) == 1 ? (crc >>> 1) ^ 0xedb88320 : crc >>> 1;
    }
  }
  return (~crc) & 0xffffffff;
}
