import 'dart:convert';
import 'dart:typed_data';

import 'package:flutter/material.dart';

import 'preventive_maintenance_controller.dart';
import 'preventive_maintenance_models.dart';

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

  @override
  void dispose() {
    signatoryNameController.dispose();
    signatoryPositionController.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(title: const Text('Acknowledge PM form')),
      body: SafeArea(
        child: AnimatedBuilder(
          animation: widget.controller,
          builder: (context, _) {
            final selectedForm = widget.controller.selectedForm;
            final form = selectedForm?.id == widget.form.id
                ? selectedForm!
                : widget.form;
            return _buildContent(context, form);
          },
        ),
      ),
    );
  }

  Widget _buildContent(BuildContext context, PreventiveMaintenanceForm form) {
    final acknowledgement = widget.controller.acknowledgement;
    final isAcknowledged =
        form.status == 'Acknowledged' || acknowledgement != null;

    return ListView(
      key: const Key('acknowledgement-page'),
      padding: const EdgeInsets.all(16),
      children: [
        _SubmittedFormSummary(form: form),
        const SizedBox(height: 16),
        if (widget.controller.errorMessage != null && !isAcknowledged)
          _AcknowledgementInlineError(message: widget.controller.errorMessage!),
        if (isAcknowledged)
          _AcknowledgementReceipt(
            acknowledgement: acknowledgement,
            alreadyAcknowledged: form.status == 'Acknowledged',
          )
        else if (form.status == 'Submitted')
          _AcknowledgementForm(
            formKey: formKey,
            signaturePadKey: signaturePadKey,
            signatoryNameController: signatoryNameController,
            signatoryPositionController: signatoryPositionController,
            localError: localError,
            isSaving: widget.controller.isSaving,
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
                'This form is not in Submitted state and cannot be acknowledged.',
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
          'This records the Department Head signatory details and signature, changes the form to Acknowledged, and completes its linked schedules.',
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
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              form.fileNumber ?? 'Preventive-maintenance form',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            const SizedBox(height: 8),
            Text('Status: ${form.status}'),
            Text('Asset category: ${form.assetCategory}'),
            Text('Building: ${form.building ?? 'Not recorded'}'),
            Text('Department: ${form.department ?? 'Not recorded'}'),
            const SizedBox(height: 16),
            Text(
              'Review inspection rows (${form.inspections.length})',
              style: Theme.of(context).textTheme.titleMedium,
            ),
            const SizedBox(height: 8),
            ...form.inspections.map((row) => _InspectionSummary(row: row)),
          ],
        ),
      ),
    );
  }
}

class _InspectionSummary extends StatelessWidget {
  const _InspectionSummary({required this.row});

  final PreventiveMaintenanceInspection row;

  @override
  Widget build(BuildContext context) {
    return Container(
      width: double.infinity,
      margin: const EdgeInsets.only(bottom: 8),
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        border: Border.all(color: Theme.of(context).colorScheme.outline),
        borderRadius: BorderRadius.circular(8),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text('Inspection ${row.id}'),
          Text('Schedule: ${row.scheduleId}'),
          Text(
            'Condition: ${row.isOperational ? 'Operational' : 'Non-operational'}',
          ),
          Text('Remarks: ${_displayValue(row.remarks)}'),
          Text('Recommendation: ${_displayValue(row.actionsRecommendations)}'),
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
    required this.onSignatureChanged,
    required this.onAcknowledge,
  });

  final GlobalKey<FormState> formKey;
  final GlobalKey<PmSignaturePadState> signaturePadKey;
  final TextEditingController signatoryNameController;
  final TextEditingController signatoryPositionController;
  final String? localError;
  final bool isSaving;
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
                'The skilled worker captures the signatory details and signature in this authenticated mobile session.',
              ),
              const SizedBox(height: 16),
              TextFormField(
                key: const Key('ack-signatory-name'),
                controller: signatoryNameController,
                enabled: !isSaving,
                textInputAction: TextInputAction.next,
                decoration: const InputDecoration(
                  labelText: 'Department Head name',
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
                  labelText: 'Department Head position',
                ),
                validator: (value) => value == null || value.trim().isEmpty
                    ? 'Enter the Department Head position.'
                    : null,
              ),
              const SizedBox(height: 16),
              const Text('Department Head signature'),
              const SizedBox(height: 8),
              PmSignaturePad(
                key: signaturePadKey,
                enabled: !isSaving,
                onChanged: onSignatureChanged,
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
  });

  final PreventiveMaintenanceAcknowledgement? acknowledgement;
  final bool alreadyAcknowledged;

  @override
  Widget build(BuildContext context) {
    if (acknowledgement == null) {
      return const Card(
        key: Key('acknowledgement-receipt'),
        child: Padding(
          padding: EdgeInsets.all(16),
          child: Text('This form is already Acknowledged and is read-only.'),
        ),
      );
    }

    final value = acknowledgement!;
    return Card(
      key: const Key('acknowledgement-receipt'),
      color: Theme.of(context).colorScheme.primaryContainer,
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Form acknowledged',
              style: Theme.of(context).textTheme.titleLarge,
            ),
            const SizedBox(height: 8),
            Text('Department Head: ${value.signatoryName}'),
            Text('Position: ${value.signatoryPosition}'),
            Text('Acknowledged: ${_dateTimeText(value.acknowledgedAt)}'),
            Text('Signature type: ${value.signatureContentType}'),
            const SizedBox(height: 8),
            const Text(
              'The backend recorded the acknowledgement and completed the linked schedules. This action does not approve corrective work, RMRFs, or WMS requests.',
            ),
            if (alreadyAcknowledged) ...[
              const SizedBox(height: 8),
              const Text('This form is now read-only.'),
            ],
          ],
        ),
      ),
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
    this.enabled = true,
  });

  final ValueChanged<bool> onChanged;
  final bool enabled;

  @override
  PmSignaturePadState createState() => PmSignaturePadState();
}

class PmSignaturePadState extends State<PmSignaturePad> {
  final repaintKey = GlobalKey();
  final strokes = <List<Offset>>[];
  int? activePointer;

  bool get hasSignature => strokes.any((stroke) => stroke.isNotEmpty);

  void _startStroke(PointerDownEvent details) {
    if (!widget.enabled || activePointer != null) return;
    activePointer = details.pointer;
    setState(() => strokes.add([details.localPosition]));
    widget.onChanged(true);
  }

  void _updateStroke(PointerMoveEvent details) {
    if (!widget.enabled ||
        details.pointer != activePointer ||
        strokes.isEmpty) {
      return;
    }
    setState(() => strokes.last.add(details.localPosition));
  }

  void _endStroke(PointerUpEvent details) {
    if (!widget.enabled ||
        details.pointer != activePointer ||
        strokes.isEmpty) {
      return;
    }
    activePointer = null;
    setState(() => strokes.add(const []));
  }

  void _cancelStroke(PointerCancelEvent details) {
    if (details.pointer == activePointer) activePointer = null;
  }

  void clear() {
    if (!widget.enabled) return;
    activePointer = null;
    setState(strokes.clear);
    widget.onChanged(false);
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
        SizedBox(
          height: 180,
          width: double.infinity,
          child: Listener(
            key: const Key('signature-canvas'),
            behavior: HitTestBehavior.opaque,
            onPointerDown: widget.enabled ? _startStroke : null,
            onPointerMove: widget.enabled ? _updateStroke : null,
            onPointerUp: widget.enabled ? _endStroke : null,
            onPointerCancel: widget.enabled ? _cancelStroke : null,
            child: RepaintBoundary(
              key: repaintKey,
              child: DecoratedBox(
                decoration: BoxDecoration(
                  color: Colors.white,
                  border: Border.all(
                    color: Theme.of(context).colorScheme.outline,
                  ),
                  borderRadius: BorderRadius.circular(8),
                ),
                child: CustomPaint(
                  painter: _SignaturePainter(strokes),
                  child: const SizedBox.expand(),
                ),
              ),
            ),
          ),
        ),
        Align(
          alignment: Alignment.centerRight,
          child: TextButton(
            key: const Key('clear-signature'),
            onPressed: widget.enabled && hasSignature ? clear : null,
            child: const Text('Clear signature'),
          ),
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
  }

  @override
  bool shouldRepaint(covariant _SignaturePainter oldDelegate) => true;
}

String _displayValue(String? value) {
  final normalized = value?.trim();
  return normalized == null || normalized.isEmpty ? 'Not recorded' : normalized;
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
  const width = 480;
  const height = 180;
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
