import 'dart:math' as math;
import 'dart:io' as io;
import 'dart:typed_data';

import 'package:camera/camera.dart';
import 'package:flutter/material.dart';
import 'package:image/image.dart' as image;

import 'inspection_photo_evidence_repository.dart';

typedef InspectionPhotoCapture =
    Future<Uint8List?> Function(BuildContext context);

class InspectionPhotoEvidenceCard extends StatefulWidget {
  const InspectionPhotoEvidenceCard({
    super.key,
    required this.inspectionId,
    this.assetCode = 'Asset code unavailable',
    this.conditionLabel = 'Not selected',
    required this.hasSavedPhoto,
    required this.editable,
    required this.repository,
    required this.onPendingStateChanged,
    this.capturePhoto = captureInspectionPhoto,
  });

  final String inspectionId;
  final String assetCode;
  final String conditionLabel;
  final bool hasSavedPhoto;
  final bool editable;
  final InspectionPhotoEvidenceRepository repository;
  final ValueChanged<bool> onPendingStateChanged;
  final InspectionPhotoCapture capturePhoto;

  @override
  State<InspectionPhotoEvidenceCard> createState() =>
      _InspectionPhotoEvidenceCardState();
}

class _InspectionPhotoEvidenceCardState
    extends State<InspectionPhotoEvidenceCard>
    with AutomaticKeepAliveClientMixin<InspectionPhotoEvidenceCard> {
  Uint8List? candidate;
  Uint8List? savedPreview;
  bool hasSavedPhoto = false;
  bool isSaving = false;
  bool isLoadingPhoto = false;
  String? errorMessage;

  @override
  bool get wantKeepAlive => candidate != null;

  @override
  void initState() {
    super.initState();
    hasSavedPhoto = widget.hasSavedPhoto;
    if (hasSavedPhoto) _loadSavedPreview();
  }

  @override
  void didUpdateWidget(covariant InspectionPhotoEvidenceCard oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.editable && !widget.editable && candidate != null) {
      candidate = null;
      errorMessage = null;
      updateKeepAlive();
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (mounted) widget.onPendingStateChanged(false);
      });
    }
    if (oldWidget.hasSavedPhoto != widget.hasSavedPhoto && candidate == null) {
      hasSavedPhoto = widget.hasSavedPhoto;
      if (hasSavedPhoto) {
        WidgetsBinding.instance.addPostFrameCallback((_) {
          if (mounted) _loadSavedPreview();
        });
      } else {
        savedPreview = null;
      }
    }
  }

  Future<void> _capture() async {
    setState(() => errorMessage = null);
    try {
      final photo = await widget.capturePhoto(context);
      if (!mounted || photo == null) return;
      setState(() => candidate = photo);
      updateKeepAlive();
      widget.onPendingStateChanged(true);
    } catch (_) {
      if (!mounted) return;
      setState(
        () => errorMessage =
            'The camera could not capture a photo. Check camera access and try again.',
      );
    }
  }

  Future<void> _save() async {
    final photo = candidate;
    if (photo == null || isSaving) return;
    setState(() {
      isSaving = true;
      errorMessage = null;
    });
    try {
      await widget.repository.saveInspectionPhoto(widget.inspectionId, photo);
      if (!mounted) return;
      setState(() {
        hasSavedPhoto = true;
        savedPreview = photo;
        candidate = null;
      });
      updateKeepAlive();
      widget.onPendingStateChanged(false);
    } catch (_) {
      if (!mounted) return;
      setState(
        () => errorMessage =
            'The photo was not saved. Your inspection row is unchanged; retry or discard the photo.',
      );
    } finally {
      if (mounted) setState(() => isSaving = false);
    }
  }

  void _discardCandidate() {
    setState(() {
      candidate = null;
      errorMessage = null;
    });
    updateKeepAlive();
    widget.onPendingStateChanged(false);
  }

  Future<void> _removeSavedPhoto() async {
    setState(() {
      isSaving = true;
      errorMessage = null;
    });
    try {
      await widget.repository.deleteInspectionPhoto(widget.inspectionId);
      if (!mounted) return;
      setState(() {
        hasSavedPhoto = false;
        savedPreview = null;
      });
    } catch (_) {
      if (!mounted) return;
      setState(() => errorMessage = 'The saved photo could not be removed.');
    } finally {
      if (mounted) setState(() => isSaving = false);
    }
  }

  Future<void> _viewSavedPhoto() async {
    if (savedPreview != null) {
      await _showPhoto(savedPreview!);
      return;
    }
    setState(() {
      isLoadingPhoto = true;
      errorMessage = null;
    });
    try {
      final photo = await widget.repository.getInspectionPhoto(
        widget.inspectionId,
      );
      if (!mounted) return;
      setState(() => savedPreview = photo);
      await _showPhoto(photo);
    } catch (_) {
      if (!mounted) return;
      setState(() => errorMessage = 'The saved photo could not be loaded.');
    } finally {
      if (mounted) setState(() => isLoadingPhoto = false);
    }
  }

  Future<void> _loadSavedPreview() async {
    if (isLoadingPhoto || savedPreview != null) return;
    setState(() {
      isLoadingPhoto = true;
      errorMessage = null;
    });
    try {
      final photo = await widget.repository.getInspectionPhoto(
        widget.inspectionId,
      );
      if (!mounted) return;
      setState(() => savedPreview = photo);
    } catch (_) {
      if (!mounted) return;
      setState(() => errorMessage = 'The saved photo could not be loaded.');
    } finally {
      if (mounted) setState(() => isLoadingPhoto = false);
    }
  }

  Future<void> _showPhoto(Uint8List photo) => showDialog<void>(
    context: context,
    builder: (context) => Dialog(
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: InteractiveViewer(
          child: Image.memory(photo, fit: BoxFit.contain),
        ),
      ),
    ),
  );

  @override
  Widget build(BuildContext context) {
    super.build(context);
    final preview = candidate ?? savedPreview;
    final thumbnail = SizedBox.square(
      key: Key('inspection-photo-thumbnail-${widget.inspectionId}'),
      dimension: 112,
      child: ClipRRect(
        borderRadius: BorderRadius.circular(8),
        child: preview == null
            ? DecoratedBox(
                decoration: BoxDecoration(
                  color: Theme.of(context).colorScheme.surfaceContainerHighest,
                ),
                child: const Center(
                  child: Icon(
                    Icons.camera_alt_outlined,
                    key: Key('inspection-photo-camera-placeholder'),
                    size: 36,
                  ),
                ),
              )
            : InkWell(
                onTap: () => _showPhoto(preview),
                child: Image.memory(
                  preview,
                  key: Key(
                    candidate != null
                        ? 'inspection-photo-candidate-preview'
                        : 'inspection-photo-saved-preview-${widget.inspectionId}',
                  ),
                  fit: BoxFit.cover,
                ),
              ),
      ),
    );

    final details = Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          'Asset: ${widget.assetCode}',
          style: Theme.of(context).textTheme.bodyMedium,
        ),
        const SizedBox(height: 4),
        Chip(
          visualDensity: VisualDensity.compact,
          label: Text(widget.conditionLabel),
        ),
        const Text('Photo documentation'),
        if (isLoadingPhoto && candidate == null) ...[
          const SizedBox(height: 6),
          const Text('Loading saved photo...'),
        ],
        if (errorMessage != null) ...[
          const SizedBox(height: 6),
          Text(
            errorMessage!,
            style: TextStyle(color: Theme.of(context).colorScheme.error),
          ),
        ],
        const SizedBox(height: 8),
        Wrap(
          spacing: 8,
          runSpacing: 8,
          children: [
            if (widget.editable)
              OutlinedButton.icon(
                key: Key('capture-inspection-photo-${widget.inspectionId}'),
                onPressed: isSaving ? null : _capture,
                icon: const Icon(Icons.camera_alt_outlined),
                label: Text(
                  candidate != null || hasSavedPhoto
                      ? 'Retake'
                      : 'Capture photo',
                ),
              ),
            if (candidate != null && widget.editable) ...[
              FilledButton(
                key: Key('save-inspection-photo-${widget.inspectionId}'),
                onPressed: isSaving ? null : _save,
                child: Text(isSaving ? 'Saving photo...' : 'Save photo'),
              ),
              TextButton(
                key: Key('discard-inspection-photo-${widget.inspectionId}'),
                onPressed: isSaving ? null : _discardCandidate,
                child: const Text('Remove'),
              ),
            ],
            if (hasSavedPhoto && candidate == null)
              OutlinedButton.icon(
                key: Key('view-inspection-photo-${widget.inspectionId}'),
                onPressed: isLoadingPhoto ? null : _viewSavedPhoto,
                icon: const Icon(Icons.visibility_outlined),
                label: const Text('View photo'),
              ),
            if (hasSavedPhoto && widget.editable && candidate == null)
              TextButton(
                key: Key('remove-inspection-photo-${widget.inspectionId}'),
                onPressed: isSaving ? null : _removeSavedPhoto,
                child: const Text('Remove'),
              ),
            if (!widget.editable && !hasSavedPhoto)
              const Text('No photo evidence recorded.'),
          ],
        ),
      ],
    );

    return Card(
      key: Key('inspection-photo-evidence-${widget.inspectionId}'),
      color: Theme.of(context).colorScheme.surfaceContainerLow,
      child: Padding(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Expanded(
                  child: Text(
                    'Inspection Photo Evidence',
                    style: Theme.of(context).textTheme.titleSmall,
                  ),
                ),
                const Chip(
                  visualDensity: VisualDensity.compact,
                  label: Text('Optional'),
                ),
              ],
            ),
            const SizedBox(height: 8),
            LayoutBuilder(
              builder: (context, constraints) {
                if (constraints.maxWidth < 480) {
                  return Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Center(child: thumbnail),
                      const SizedBox(height: 8),
                      details,
                    ],
                  );
                }
                return Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    thumbnail,
                    const SizedBox(width: 12),
                    Expanded(child: details),
                  ],
                );
              },
            ),
          ],
        ),
      ),
    );
  }
}

Future<Uint8List?> captureInspectionPhoto(BuildContext context) =>
    Navigator.of(context).push<Uint8List?>(
      MaterialPageRoute<Uint8List?>(
        builder: (_) => const InspectionPhotoCapturePage(),
      ),
    );

class InspectionPhotoCapturePage extends StatefulWidget {
  const InspectionPhotoCapturePage({super.key});

  @override
  State<InspectionPhotoCapturePage> createState() =>
      _InspectionPhotoCapturePageState();
}

class _InspectionPhotoCapturePageState
    extends State<InspectionPhotoCapturePage> {
  CameraController? controller;
  Uint8List? capturedPhoto;
  String? errorMessage;
  bool takingPhoto = false;
  bool initializingCamera = false;

  @override
  void initState() {
    super.initState();
    _initializeCamera();
  }

  Future<void> _initializeCamera() async {
    if (initializingCamera) return;
    final previous = controller;
    setState(() {
      initializingCamera = true;
      errorMessage = null;
      controller = null;
    });
    await previous?.dispose();
    CameraController? next;
    try {
      final cameras = await availableCameras();
      if (cameras.isEmpty) {
        throw StateError('No camera is available.');
      }
      final selectedCamera = cameras.firstWhere(
        (camera) => camera.lensDirection == CameraLensDirection.back,
        orElse: () => cameras.first,
      );
      next = CameraController(
        selectedCamera,
        ResolutionPreset.medium,
        enableAudio: false,
      );
      await next.initialize();
      if (!mounted) {
        await next.dispose();
        return;
      }
      setState(() => controller = next);
    } catch (_) {
      await next?.dispose();
      if (mounted) {
        setState(
          () => errorMessage =
              'Camera access is unavailable. Allow camera permission and try again.',
        );
      }
    } finally {
      if (mounted) setState(() => initializingCamera = false);
    }
  }

  Future<void> _takePhoto() async {
    final current = controller;
    if (current == null || !current.value.isInitialized || takingPhoto) return;
    setState(() => takingPhoto = true);
    try {
      final file = await current.takePicture();
      try {
        final compressed = _compressPhoto(await file.readAsBytes());
        if (mounted) setState(() => capturedPhoto = compressed);
      } finally {
        try {
          await io.File(file.path).delete();
        } catch (_) {
          // The camera plugin's temporary file is best-effort cleanup.
        }
      }
    } catch (_) {
      if (mounted) {
        setState(
          () => errorMessage = 'The photo could not be captured. Try again.',
        );
      }
    } finally {
      if (mounted) {
        setState(() => takingPhoto = false);
      }
    }
  }

  @override
  void dispose() {
    controller?.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final current = controller;
    return Scaffold(
      appBar: AppBar(
        title: const Text('Capture inspection photo'),
        leading: IconButton(
          key: const Key('cancel-inspection-photo-capture'),
          onPressed: () => Navigator.of(context).pop(),
          icon: const Icon(Icons.arrow_back),
        ),
      ),
      body: Column(
        children: [
          Expanded(
            child: Center(
              child: errorMessage != null
                  ? Padding(
                      padding: const EdgeInsets.all(24),
                      child: Column(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Text(errorMessage!, textAlign: TextAlign.center),
                          const SizedBox(height: 12),
                          OutlinedButton(
                            key: const Key('retry-inspection-camera'),
                            onPressed: initializingCamera
                                ? null
                                : _initializeCamera,
                            child: const Text('Retry camera access'),
                          ),
                        ],
                      ),
                    )
                  : capturedPhoto != null
                  ? Image.memory(capturedPhoto!, fit: BoxFit.contain)
                  : current?.value.isInitialized == true
                  ? CameraPreview(current!)
                  : const CircularProgressIndicator(),
            ),
          ),
          Padding(
            padding: const EdgeInsets.all(16),
            child: Wrap(
              spacing: 12,
              children: [
                if (capturedPhoto == null)
                  FilledButton.icon(
                    key: const Key('take-inspection-photo'),
                    onPressed:
                        current?.value.isInitialized == true && !takingPhoto
                        ? _takePhoto
                        : null,
                    icon: const Icon(Icons.camera_alt),
                    label: Text(takingPhoto ? 'Capturing...' : 'Take photo'),
                  )
                else ...[
                  OutlinedButton(
                    key: const Key('retake-inspection-photo'),
                    onPressed: () => setState(() => capturedPhoto = null),
                    child: const Text('Retake'),
                  ),
                  FilledButton(
                    key: const Key('use-inspection-photo'),
                    onPressed: () => Navigator.of(context).pop(capturedPhoto),
                    child: const Text('Use photo'),
                  ),
                ],
              ],
            ),
          ),
        ],
      ),
    );
  }
}

Uint8List _compressPhoto(Uint8List bytes) {
  final decoded = image.decodeImage(bytes);
  if (decoded == null) throw const FormatException('Invalid camera image.');
  var current = image.bakeOrientation(decoded);
  for (var resizeAttempt = 0; resizeAttempt < 4; resizeAttempt++) {
    final longestSide = math.max(current.width, current.height);
    if (longestSide > 1600) {
      final scale = 1600 / longestSide;
      current = image.copyResize(
        current,
        width: (current.width * scale).round(),
        height: (current.height * scale).round(),
      );
    }
    for (final quality in [82, 74, 66, 58]) {
      final encoded = Uint8List.fromList(
        image.encodeJpg(current, quality: quality),
      );
      if (encoded.length <= 4 * 1024 * 1024) return encoded;
    }
    if (current.width <= 800 || current.height <= 800) break;
    current = image.copyResize(
      current,
      width: (current.width * 0.75).round(),
      height: (current.height * 0.75).round(),
    );
  }
  throw const FormatException('The compressed photo exceeds the upload limit.');
}
