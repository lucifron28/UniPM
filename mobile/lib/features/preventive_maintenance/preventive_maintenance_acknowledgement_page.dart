import 'dart:convert';
import 'dart:typed_data';

import 'package:flutter/gestures.dart';
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
  final pageScrollController = ScrollController();
  final formKey = GlobalKey<FormState>();
  final signaturePadKey = GlobalKey<PmSignaturePadState>();
  final signatoryNameController = TextEditingController();
  final signatoryPositionController = TextEditingController();
  String? localError;
  bool isSignatureGestureActive = false;

  @override
  void dispose() {
    pageScrollController.dispose();
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
              key: const Key('back-to-pm-forms'),
              tooltip: 'Back',
              onPressed: () => Navigator.of(context).maybePop(),
              icon: const Icon(Icons.arrow_back),
            ),
            title: Text(
              isAcknowledged ? 'PM form details' : 'Acknowledge PM form',
            ),
            actions: [
              if (!isAcknowledged && form.status == 'Submitted')
                IconButton(
                  key: const Key('jump-to-acknowledgement-form'),
                  tooltip: 'Go to signatory fields',
                  onPressed: _jumpToAcknowledgementForm,
                  icon: const Icon(Icons.draw_outlined),
                ),
            ],
          ),
          body: SafeArea(child: _buildContent(context, form)),
        );
      },
    );
  }

  Widget _buildContent(BuildContext context, PreventiveMaintenanceForm form) {
    final acknowledgement = widget.controller.acknowledgement;
    final isAcknowledged =
        form.status == 'Acknowledged' || acknowledgement != null;

    return ListView(
      key: const Key('acknowledgement-page'),
      controller: pageScrollController,
      padding: const EdgeInsets.all(16),
      physics: isSignatureGestureActive
          ? const NeverScrollableScrollPhysics()
          : null,
      children: [
        _SubmittedFormSummary(form: form),
        const SizedBox(height: 12),
        _InspectionDetails(form: form),
        const SizedBox(height: 16),
        if (widget.controller.errorMessage != null && !isAcknowledged)
          _AcknowledgementInlineError(message: widget.controller.errorMessage!),
        if (isAcknowledged)
          _AcknowledgementReceipt(
            acknowledgement: acknowledgement,
            alreadyAcknowledged: form.status == 'Acknowledged',
            formNumber: form.fileNumber ?? 'Preventive-maintenance form',
            operatorUserId: widget.controller.user.id,
            operatorDisplayName: widget.controller.user.displayName,
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

  Future<void> _jumpToAcknowledgementForm() async {
    for (var attempt = 0; attempt < 8; attempt++) {
      final target = formKey.currentContext;
      if (target != null) {
        if (!target.mounted) {
          _showScrollFailure();
          return;
        }
        await Scrollable.ensureVisible(
          target,
          duration: const Duration(milliseconds: 250),
          curve: Curves.easeInOut,
          alignment: 0.04,
        );
        return;
      }

      if (!pageScrollController.hasClients) {
        _showScrollFailure();
        return;
      }
      final position = pageScrollController.position;
      if (!position.hasContentDimensions) {
        await WidgetsBinding.instance.endOfFrame;
        if (!mounted) return;
        continue;
      }
      await position.animateTo(
        position.maxScrollExtent,
        duration: const Duration(milliseconds: 150),
        curve: Curves.easeOut,
      );
      await WidgetsBinding.instance.endOfFrame;
      if (!mounted) return;
    }

    final target = formKey.currentContext;
    if (target != null) {
      if (!target.mounted) {
        _showScrollFailure();
        return;
      }
      await Scrollable.ensureVisible(
        target,
        duration: const Duration(milliseconds: 250),
        curve: Curves.easeInOut,
        alignment: 0.04,
      );
    } else {
      _showScrollFailure();
    }
  }

  void _showScrollFailure() {
    if (!mounted) return;
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(content: Text('Could not reach the signatory fields.')),
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
          'Field-work completion and acknowledgement are separate. '
          'Acknowledgement records receipt/noting of the findings and the '
          'Department Head signatory details and signature, changes the form '
          'to Acknowledged, and locks it. Its inspection rows become eligible '
          'for official history. It does not approve corrective work, funding, '
          'or an RMRF.',
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
    final operationalCount = form.inspections
        .where((row) => row.isOperational)
        .length;
    final nonOperational = form.inspections
        .where((row) => !row.isOperational)
        .length;
    final colors = Theme.of(context).colorScheme;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Wrap(
              spacing: 8,
              runSpacing: 6,
              crossAxisAlignment: WrapCrossAlignment.center,
              children: [
                Text(
                  form.fileNumber ?? 'Preventive-maintenance form',
                  style: Theme.of(context).textTheme.titleLarge,
                  softWrap: true,
                ),
                _FormStatusBadge(status: form.status),
              ],
            ),
            const SizedBox(height: 6),
            Text(
              displayAssetCategory(form.assetCategory),
              style: Theme.of(
                context,
              ).textTheme.labelLarge?.copyWith(color: colors.primary),
            ),
            const SizedBox(height: 2),
            Text(
              '${form.department ?? 'Department not recorded'} · '
              '${form.building ?? 'Building not recorded'}',
              style: Theme.of(context).textTheme.bodyMedium,
              softWrap: true,
            ),
            if (form.submittedAt != null) ...[
              const SizedBox(height: 4),
              Text('Submitted to server: ${_dateTimeText(form.submittedAt!)}'),
            ],
            const SizedBox(height: 12),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                _InspectionCountChip(
                  icon: Icons.fact_check_outlined,
                  label: '${form.inspections.length} total',
                ),
                _InspectionCountChip(
                  icon: Icons.check_circle_outline,
                  label: '$operationalCount operational',
                ),
                _InspectionCountChip(
                  icon: nonOperational == 0
                      ? Icons.check_circle_outline
                      : Icons.warning_amber_rounded,
                  label: '$nonOperational non-operational',
                  isWarning: nonOperational > 0,
                ),
              ],
            ),
          ],
        ),
      ),
    );
  }
}

class _FormStatusBadge extends StatelessWidget {
  const _FormStatusBadge({required this.status});

  final String status;

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    final isAcknowledged = status == 'Acknowledged';
    final background = isAcknowledged
        ? colors.primaryContainer
        : status == 'Submitted'
        ? colors.secondaryContainer
        : colors.surfaceContainerHighest;
    final foreground = isAcknowledged
        ? colors.onPrimaryContainer
        : status == 'Submitted'
        ? colors.onSecondaryContainer
        : colors.onSurfaceVariant;
    return Container(
      constraints: const BoxConstraints(maxWidth: 180),
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      decoration: BoxDecoration(
        color: background,
        borderRadius: BorderRadius.circular(999),
      ),
      child: Text(
        _formStatusLabel(status),
        style: Theme.of(context).textTheme.labelMedium?.copyWith(
          color: foreground,
          fontWeight: FontWeight.w700,
        ),
        textAlign: TextAlign.center,
        softWrap: true,
      ),
    );
  }
}

class _InspectionCountChip extends StatelessWidget {
  const _InspectionCountChip({
    required this.icon,
    required this.label,
    this.isWarning = false,
  });

  final IconData icon;
  final String label;
  final bool isWarning;

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    final color = isWarning ? colors.error : colors.primary;
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      decoration: BoxDecoration(
        color: color.withValues(alpha: 0.10),
        borderRadius: BorderRadius.circular(999),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          Icon(icon, size: 16, color: color),
          const SizedBox(width: 6),
          Flexible(
            child: Text(
              label,
              style: TextStyle(
                color: color,
                fontWeight: FontWeight.w700,
                fontSize: 12,
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class _InspectionDetails extends StatelessWidget {
  const _InspectionDetails({required this.form});

  final PreventiveMaintenanceForm form;

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Padding(
          padding: const EdgeInsets.symmetric(vertical: 4),
          child: Text(
            'Inspection details (${form.inspections.length})',
            style: Theme.of(context).textTheme.titleMedium,
          ),
        ),
        if (form.inspections.isNotEmpty)
          const Padding(
            padding: EdgeInsets.only(bottom: 6),
            child: Text(
              'Asset code and location below reflect current asset records.',
            ),
          ),
        if (form.inspections.isEmpty)
          const Padding(
            padding: EdgeInsets.symmetric(vertical: 12),
            child: Text('No inspection rows are attached to this form.'),
          )
        else
          ...form.inspections.map((row) => _InspectionSummary(row: row)),
      ],
    );
  }
}

class _InspectionSummary extends StatelessWidget {
  const _InspectionSummary({required this.row});

  final PreventiveMaintenanceInspection row;

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    final warningColor = colors.error;
    return Container(
      key: Key('inspection-row-${row.id}'),
      width: double.infinity,
      margin: const EdgeInsets.only(bottom: 2),
      child: Material(
        color: row.isOperational
            ? colors.surface
            : warningColor.withValues(alpha: 0.04),
        shape: row.isOperational
            ? Border(bottom: BorderSide(color: colors.outlineVariant))
            : RoundedRectangleBorder(
                borderRadius: BorderRadius.circular(8),
                side: BorderSide(color: warningColor.withValues(alpha: 0.22)),
              ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            ExpansionTile(
              key: Key('inspection-row-expansion-${row.id}'),
              tilePadding: const EdgeInsets.symmetric(
                horizontal: 12,
                vertical: 2,
              ),
              childrenPadding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
              title: Text(
                _assetCodeLabel(row.assetCode),
                style: Theme.of(
                  context,
                ).textTheme.titleSmall?.copyWith(fontWeight: FontWeight.w700),
                softWrap: true,
              ),
              subtitle: Padding(
                padding: const EdgeInsets.only(top: 4),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      row.location?.trim().isNotEmpty == true
                          ? 'Current asset location: ${row.location!.trim()}'
                          : 'Current asset location not recorded',
                      style: Theme.of(context).textTheme.bodySmall?.copyWith(
                        color: colors.onSurfaceVariant,
                      ),
                      softWrap: true,
                    ),
                    const SizedBox(height: 3),
                    Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 8,
                        vertical: 4,
                      ),
                      decoration: BoxDecoration(
                        color: row.isOperational
                            ? const Color(0xFFE2F3EA)
                            : colors.errorContainer,
                        borderRadius: BorderRadius.circular(6),
                      ),
                      child: Text(
                        row.isOperational ? 'Operational' : 'Non-operational',
                        style: Theme.of(context).textTheme.labelMedium
                            ?.copyWith(
                              color: row.isOperational
                                  ? const Color(0xFF176B4B)
                                  : colors.onErrorContainer,
                              fontWeight: FontWeight.w700,
                            ),
                      ),
                    ),
                  ],
                ),
              ),
              children: [_InspectionExpandedDetails(row: row)],
            ),
            if (!row.isOperational)
              Padding(
                key: Key('non-operational-preview-${row.id}'),
                padding: const EdgeInsets.fromLTRB(16, 0, 16, 12),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    _InspectionDetailLine(
                      label: 'Remarks',
                      value: _displayValue(row.remarks),
                      isNote: true,
                    ),
                    const SizedBox(height: 4),
                    _InspectionDetailLine(
                      label: 'Recommendations',
                      value: _displayValue(row.actionsRecommendations),
                      isNote: true,
                    ),
                  ],
                ),
              ),
          ],
        ),
      ),
    );
  }
}

class _InspectionExpandedDetails extends StatelessWidget {
  const _InspectionExpandedDetails({required this.row});

  final PreventiveMaintenanceInspection row;

  @override
  Widget build(BuildContext context) {
    final hasWaterStationDetails =
        row.waterReplaceCarbonFilter != null ||
        row.waterReplaceSedimentFilter != null ||
        row.waterCheckUvLight != null;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Container(
          width: double.infinity,
          margin: const EdgeInsets.only(top: 8, bottom: 4),
          padding: const EdgeInsets.fromLTRB(12, 4, 12, 10),
          decoration: BoxDecoration(
            color: Theme.of(context).colorScheme.surfaceContainerLow,
            borderRadius: BorderRadius.circular(8),
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              _InspectionDetailLine(
                label: 'Inspection date entered by inspector',
                value: _dateText(row.dateInspected),
              ),
              if (row.dateAccomplished != null)
                _InspectionDetailLine(
                  label: 'Date accomplished',
                  value: _dateText(row.dateAccomplished!),
                ),
              if (row.startedAt != null)
                _InspectionDetailLine(
                  label: 'Started',
                  value: _dateTimeText(row.startedAt!),
                ),
              if (row.completedAt != null)
                _InspectionDetailLine(
                  label: 'Server recorded completion',
                  value: _dateTimeText(row.completedAt!),
                ),
            ],
          ),
        ),
        if (row.isOperational) ...[
          _InspectionDetailLine(
            label: 'Remarks',
            value: _displayValue(row.remarks),
            isNote: true,
          ),
          _InspectionDetailLine(
            label: 'Recommendations',
            value: _displayValue(row.actionsRecommendations),
            isNote: true,
          ),
        ],
        if (row.hasPhotoEvidence)
          const _InspectionDetailLine(
            label: 'Photo evidence',
            value: 'Available',
          ),
        if (hasWaterStationDetails) ...[
          _InspectionDetailLine(
            label: 'Replace carbon filter',
            value: _inspectionBooleanValue(row.waterReplaceCarbonFilter),
          ),
          _InspectionDetailLine(
            label: 'Replace sediment filter',
            value: _inspectionBooleanValue(row.waterReplaceSedimentFilter),
          ),
          _InspectionDetailLine(
            label: 'Check UV light',
            value: _inspectionBooleanValue(row.waterCheckUvLight),
          ),
        ],
      ],
    );
  }
}

class _InspectionDetailLine extends StatelessWidget {
  const _InspectionDetailLine({
    required this.label,
    required this.value,
    this.isNote = false,
  });

  final String label;
  final String value;
  final bool isNote;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Container(
      width: double.infinity,
      margin: EdgeInsets.only(top: isNote ? 8 : 6),
      padding: isNote ? const EdgeInsets.all(12) : EdgeInsets.zero,
      decoration: isNote
          ? BoxDecoration(
              color: theme.colorScheme.surfaceContainerLow,
              border: Border(
                left: BorderSide(color: theme.colorScheme.primary, width: 3),
              ),
            )
          : null,
      child: Text.rich(
        TextSpan(
          children: [
            TextSpan(
              text: '$label: ',
              style: TextStyle(
                color: isNote
                    ? theme.colorScheme.primary
                    : theme.colorScheme.onSurfaceVariant,
                fontWeight: isNote ? FontWeight.w700 : FontWeight.w400,
              ),
            ),
            TextSpan(
              text: value,
              style: TextStyle(
                fontWeight: isNote ? FontWeight.w400 : FontWeight.w600,
              ),
            ),
          ],
        ),
        style: theme.textTheme.bodyMedium?.copyWith(height: 1.5),
        softWrap: true,
      ),
    );
  }
}

String _inspectionBooleanValue(bool? value) => switch (value) {
  true => 'Yes',
  false => 'No',
  null => 'Not recorded',
};

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
              const SizedBox(height: 8),
              const Text(
                'Enter the Department Head\'s name, position, and signature. '
                'UniPM records the signed-in account submitting this receipt separately.',
              ),
              const SizedBox(height: 16),
              TextFormField(
                key: const Key('ack-signatory-name'),
                controller: signatoryNameController,
                enabled: !isSaving,
                textInputAction: TextInputAction.next,
                decoration: const InputDecoration(labelText: 'Full name'),
                validator: (value) => value == null || value.trim().isEmpty
                    ? 'Enter the Department Head name.'
                    : null,
              ),
              const SizedBox(height: 12),
              TextFormField(
                key: const Key('ack-signatory-position'),
                controller: signatoryPositionController,
                enabled: !isSaving,
                decoration: const InputDecoration(labelText: 'Position'),
                validator: (value) => value == null || value.trim().isEmpty
                    ? 'Enter the Department Head position.'
                    : null,
              ),
              const SizedBox(height: 16),
              Text(
                'Sign inside the box',
                style: Theme.of(context).textTheme.labelLarge,
              ),
              const SizedBox(height: 8),
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
                style: Theme.of(context).textTheme.bodySmall,
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
    required this.alreadyAcknowledged,
    required this.formNumber,
    required this.operatorUserId,
    required this.operatorDisplayName,
  });

  final PreventiveMaintenanceAcknowledgement? acknowledgement;
  final bool alreadyAcknowledged;
  final String formNumber;
  final String operatorUserId;
  final String operatorDisplayName;

  @override
  Widget build(BuildContext context) {
    final colors = Theme.of(context).colorScheme;
    final value = acknowledgement;
    return Card(
      key: const Key('acknowledgement-receipt'),
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Row(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Icon(Icons.check_circle, color: colors.primary, size: 24),
            const SizedBox(width: 12),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Form acknowledged',
                    style: Theme.of(context).textTheme.titleMedium,
                  ),
                  const SizedBox(height: 4),
                  Text(
                    formNumber,
                    style: Theme.of(context).textTheme.bodyLarge,
                  ),
                  const SizedBox(height: 8),
                  Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 10,
                      vertical: 5,
                    ),
                    decoration: BoxDecoration(
                      color: colors.primaryContainer,
                      borderRadius: BorderRadius.circular(999),
                    ),
                    child: Text(
                      'Acknowledged',
                      style: Theme.of(context).textTheme.labelMedium?.copyWith(
                        color: colors.onPrimaryContainer,
                        fontWeight: FontWeight.w700,
                      ),
                    ),
                  ),
                  if (value != null) ...[
                    const SizedBox(height: 8),
                    Text('Department Head: ${value.signatoryName}'),
                    Text('Position: ${value.signatoryPosition}'),
                    Text(
                      'Recorded by: ${_operatorLabel(value, operatorUserId, operatorDisplayName)}',
                    ),
                    Text(
                      'Acknowledged: ${_dateTimeText(value.acknowledgedAt)}',
                    ),
                  ],
                  if (alreadyAcknowledged || value != null) ...[
                    const SizedBox(height: 6),
                    Text(
                      'Read-only form',
                      style: Theme.of(context).textTheme.labelLarge?.copyWith(
                        color: colors.onSurfaceVariant,
                        fontWeight: FontWeight.w600,
                      ),
                    ),
                  ],
                ],
              ),
            ),
          ],
        ),
      ),
    );
  }
}

String _operatorLabel(
  PreventiveMaintenanceAcknowledgement acknowledgement,
  String operatorUserId,
  String operatorDisplayName,
) {
  final displayName = acknowledgement.capturedByUserId == operatorUserId
      ? operatorDisplayName.trim()
      : '';
  if (displayName.isEmpty) {
    return 'account ${acknowledgement.capturedByUserId}';
  }
  return '$displayName (account ${acknowledgement.capturedByUserId})';
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
  final repaintKey = GlobalKey();
  final strokes = <List<Offset>>[];
  int? activePointer;
  bool isStrokeActive = false;

  bool get hasSignature => strokes.any((stroke) => stroke.isNotEmpty);

  Offset? _positionInSurface(PointerEvent event) {
    final renderObject = repaintKey.currentContext?.findRenderObject();
    if (renderObject is! RenderBox || !renderObject.hasSize) {
      return null;
    }

    final position = renderObject.globalToLocal(event.position);
    return (Offset.zero & renderObject.size).contains(position)
        ? position
        : null;
  }

  void _startStroke(PointerDownEvent details) {
    if (!widget.enabled ||
        activePointer != null ||
        details.buttons != kPrimaryButton) {
      return;
    }
    final position = _positionInSurface(details);
    if (position == null) return;

    activePointer = details.pointer;
    isStrokeActive = true;
    setState(() => strokes.add([position]));
    widget.onChanged(true);
    widget.onGestureChanged(true);
  }

  void _updateStroke(PointerMoveEvent details) {
    if (details.pointer != activePointer ||
        !isStrokeActive ||
        strokes.isEmpty) {
      return;
    }
    if (!widget.enabled) {
      _finishStroke();
      return;
    }

    final position = _positionInSurface(details);
    if (position == null) {
      _finishStroke();
      return;
    }
    setState(() => strokes.last.add(position));
  }

  void _endStroke(PointerUpEvent details) {
    if (details.pointer != activePointer) return;

    _finishStroke(
      finalPosition: _positionInSurface(details),
      releasePointer: true,
    );
  }

  void _cancelStroke(PointerCancelEvent details) {
    if (details.pointer != activePointer) return;
    _finishStroke(releasePointer: true);
  }

  void _finishStroke({Offset? finalPosition, bool releasePointer = false}) {
    if (activePointer == null) return;
    final wasActive = isStrokeActive;
    setState(() {
      if (wasActive &&
          finalPosition != null &&
          strokes.isNotEmpty &&
          strokes.last.isNotEmpty &&
          strokes.last.last != finalPosition) {
        strokes.last.add(finalPosition);
      }
      isStrokeActive = false;
      if (releasePointer) activePointer = null;
    });
    if (wasActive) widget.onGestureChanged(false);
  }

  void undo() {
    if (!widget.enabled || isStrokeActive || strokes.isEmpty) return;
    setState(strokes.removeLast);
    widget.onChanged(hasSignature);
  }

  void clear() {
    if (!widget.enabled) return;
    final hadActiveStroke = isStrokeActive;
    setState(() {
      isStrokeActive = false;
      strokes.clear();
    });
    widget.onChanged(false);
    if (hadActiveStroke) widget.onGestureChanged(false);
  }

  Future<String?> toPngBase64() async {
    if (!hasSignature) return null;
    final renderObject = repaintKey.currentContext?.findRenderObject();
    final logicalSize = renderObject is RenderBox
        ? renderObject.size
        : const Size(480, 180);
    return base64Encode(_encodeSignaturePng(logicalSize, strokes));
  }

  @override
  Widget build(BuildContext context) {
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Align(
          alignment: Alignment.centerLeft,
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 960),
            child: SizedBox(
              height: 180,
              width: double.infinity,
              child: Listener(
                key: const Key('signature-canvas'),
                behavior: HitTestBehavior.opaque,
                onPointerDown: widget.enabled ? _startStroke : null,
                onPointerMove: widget.enabled || activePointer != null
                    ? _updateStroke
                    : null,
                onPointerUp: widget.enabled || activePointer != null
                    ? _endStroke
                    : null,
                onPointerCancel: widget.enabled || activePointer != null
                    ? _cancelStroke
                    : null,
                child: DecoratedBox(
                  decoration: BoxDecoration(
                    color: const Color(0xFFF3F1EE),
                    border: Border.all(color: const Color(0xFFD0CBC4)),
                    borderRadius: BorderRadius.circular(8),
                  ),
                  child: RepaintBoundary(
                    key: repaintKey,
                    child: ClipRRect(
                      borderRadius: BorderRadius.circular(8),
                      child: Stack(
                        fit: StackFit.expand,
                        children: [
                          IgnorePointer(
                            child: Center(
                              child: Opacity(
                                opacity: 0.08,
                                child: Image.asset(
                                  'assets/unipm-logo.png',
                                  width: 128,
                                  height: 128,
                                  fit: BoxFit.contain,
                                  color: Colors.white,
                                  colorBlendMode: BlendMode.srcIn,
                                  errorBuilder: (context, error, stackTrace) =>
                                      const SizedBox.shrink(),
                                ),
                              ),
                            ),
                          ),
                          IgnorePointer(
                            child: CustomPaint(
                              painter: _SignaturePainter(strokes),
                              child: const SizedBox.expand(),
                            ),
                          ),
                          if (!hasSignature)
                            IgnorePointer(
                              child: Center(
                                child: Text(
                                  'Sign here',
                                  style: Theme.of(context).textTheme.bodySmall
                                      ?.copyWith(
                                        color: const Color(0xFF6E6963),
                                      ),
                                ),
                              ),
                            ),
                        ],
                      ),
                    ),
                  ),
                ),
              ),
            ),
          ),
        ),
        Row(
          mainAxisAlignment: MainAxisAlignment.end,
          children: [
            TextButton(
              key: const Key('undo-signature'),
              onPressed: widget.enabled && hasSignature && !isStrokeActive
                  ? undo
                  : null,
              child: const Text('Undo'),
            ),
            TextButton(
              key: const Key('clear-signature'),
              onPressed: widget.enabled && hasSignature ? clear : null,
              child: const Text('Clear'),
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
  final manila = value.toUtc().add(const Duration(hours: 8));
  final month = manila.month.toString().padLeft(2, '0');
  final day = manila.day.toString().padLeft(2, '0');
  final hour = manila.hour.toString().padLeft(2, '0');
  final minute = manila.minute.toString().padLeft(2, '0');
  return '${manila.year}-$month-$day $hour:$minute Asia/Manila';
}

Uint8List _encodeSignaturePng(Size logicalSize, List<List<Offset>> strokes) {
  final width = logicalSize.width > 0 ? logicalSize.width.ceil() : 480;
  final height = logicalSize.height > 0 ? logicalSize.height.ceil() : 180;
  final pixels = Uint8List(width * height)..fillRange(0, width * height, 255);

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
    final startX = start.dx;
    final startY = start.dy;
    final endX = end.dx;
    final endY = end.dy;
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
      drawPoint(stroke.single.dx, stroke.single.dy);
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
