import 'dart:typed_data';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:image/image.dart' as image;
import 'package:mobile/api/api_client.dart';
import 'package:mobile/features/preventive_maintenance/inspection_photo_evidence_card.dart';
import 'package:mobile/features/preventive_maintenance/inspection_photo_evidence_repository.dart';
import 'package:mobile/features/preventive_maintenance/preventive_maintenance_repository.dart';

void main() {
  test('photo repository uses authenticated binary API requests', () async {
    final transport = _BinaryTransport();
    final client =
        ApiClient(
          baseUrl: Uri.parse('http://localhost:5000/'),
          httpClient: transport,
        )..configureSession(
          accessTokenProvider: () => 'test-access-token',
          terminalAuthFailureHandler: () async {},
        );
    final repository = ApiPreventiveMaintenanceRepository(client);

    expect(await repository.getInspectionPhoto('inspection-1'), [0xff, 0xd8]);
    await repository.saveInspectionPhoto('inspection-1', _tinyPng);
    await repository.deleteInspectionPhoto('inspection-1');

    expect(transport.requests.map((request) => request.method), [
      'GET',
      'PUT',
      'DELETE',
    ]);
    expect(
      transport.requests.map((request) => request.url.path),
      everyElement('/api/v1/inspections/inspection-1/photo'),
    );
    expect(
      transport.requests[0].headers['authorization'],
      'Bearer test-access-token',
    );
    expect(transport.requests[0].headers['accept'], 'image/jpeg');
    expect(transport.requests[1].headers['content-type'], 'image/jpeg');
    expect(transport.requests[1].bodyBytes, _tinyPng);
    expect(
      transport.requests[1].headers['authorization'],
      'Bearer test-access-token',
    );
  });

  testWidgets('camera cancel and camera failure do not create pending evidence', (
    tester,
  ) async {
    final pendingStates = <bool>[];
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: InspectionPhotoEvidenceCard(
            inspectionId: 'inspection-1',
            hasSavedPhoto: false,
            editable: true,
            repository: _PhotoRepository(),
            onPendingStateChanged: pendingStates.add,
            capturePhoto: (_) async => null,
          ),
        ),
      ),
    );

    await tester.tap(
      find.byKey(const Key('capture-inspection-photo-inspection-1')),
    );
    await tester.pumpAndSettle();
    expect(pendingStates, isEmpty);

    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: InspectionPhotoEvidenceCard(
            inspectionId: 'inspection-1',
            hasSavedPhoto: false,
            editable: true,
            repository: _PhotoRepository(),
            onPendingStateChanged: pendingStates.add,
            capturePhoto: (_) async =>
                throw StateError('Camera permission denied'),
          ),
        ),
      ),
    );
    await tester.tap(
      find.byKey(const Key('capture-inspection-photo-inspection-1')),
    );
    await tester.pumpAndSettle();

    expect(
      find.text(
        'The camera could not capture a photo. Check camera access and try again.',
      ),
      findsOneWidget,
    );
    expect(pendingStates, isEmpty);
  });

  testWidgets('removing a captured photo clears its pending submit gate', (
    tester,
  ) async {
    final repository = _PhotoRepository();
    final pendingStates = <bool>[];
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: InspectionPhotoEvidenceCard(
            inspectionId: 'inspection-1',
            hasSavedPhoto: false,
            editable: true,
            repository: repository,
            onPendingStateChanged: pendingStates.add,
            capturePhoto: (_) async => _tinyPng,
          ),
        ),
      ),
    );

    await tester.tap(
      find.byKey(const Key('capture-inspection-photo-inspection-1')),
    );
    await tester.pumpAndSettle();
    await tester.tap(
      find.byKey(const Key('discard-inspection-photo-inspection-1')),
    );
    await tester.pumpAndSettle();

    expect(pendingStates, [true, false]);
    expect(repository.savedPhoto, isNull);
    expect(
      find.byKey(const Key('inspection-photo-candidate-preview')),
      findsNothing,
    );
  });

  testWidgets(
    'failed upload retains the candidate; retry, preview, and remove work',
    (tester) async {
      final repository = _PhotoRepository()..failNextUpload = true;
      final pendingStates = <bool>[];
      var captureCount = 0;
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: SingleChildScrollView(
              child: InspectionPhotoEvidenceCard(
                inspectionId: 'inspection-1',
                hasSavedPhoto: false,
                editable: true,
                repository: repository,
                onPendingStateChanged: pendingStates.add,
                capturePhoto: (_) async {
                  captureCount++;
                  return _tinyPng;
                },
              ),
            ),
          ),
        ),
      );

      await tester.tap(
        find.byKey(const Key('capture-inspection-photo-inspection-1')),
      );
      await tester.pumpAndSettle();
      expect(pendingStates, [true]);
      expect(
        find.byKey(const Key('inspection-photo-candidate-preview')),
        findsOneWidget,
      );
      expect(captureCount, 1);

      await tester.tap(
        find.byKey(const Key('capture-inspection-photo-inspection-1')),
      );
      await tester.pumpAndSettle();
      expect(captureCount, 2);
      expect(pendingStates, [true, true]);
      expect(
        find.byKey(const Key('inspection-photo-candidate-preview')),
        findsOneWidget,
      );

      await tester.tap(
        find.byKey(const Key('save-inspection-photo-inspection-1')),
      );
      await tester.pumpAndSettle();
      expect(
        find.text(
          'The photo was not saved. Your inspection row is unchanged; retry or discard the photo.',
        ),
        findsOneWidget,
      );
      expect(
        find.byKey(const Key('inspection-photo-candidate-preview')),
        findsOneWidget,
      );
      expect(pendingStates, [true, true]);

      await tester.tap(
        find.byKey(const Key('save-inspection-photo-inspection-1')),
      );
      await tester.pumpAndSettle();
      expect(pendingStates, [true, true, false]);
      expect(repository.savedPhoto, isNotNull);

      await tester.tap(
        find.byKey(const Key('view-inspection-photo-inspection-1')),
      );
      await tester.pumpAndSettle();
      expect(
        repository.readCount,
        0,
      ); // The just-saved image is already available locally.
      expect(find.byType(InteractiveViewer), findsOneWidget);
      await tester.tapAt(const Offset(10, 10));
      await tester.pumpAndSettle();

      await tester.tap(
        find.byKey(const Key('remove-inspection-photo-inspection-1')),
      );
      await tester.pumpAndSettle();
      expect(repository.deleteCount, 1);
      expect(find.text('No photo evidence recorded.'), findsNothing);
      expect(
        find.byKey(const Key('view-inspection-photo-inspection-1')),
        findsNothing,
      );
    },
  );

  testWidgets('read-only row displays saved evidence without edit controls', (
    tester,
  ) async {
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: InspectionPhotoEvidenceCard(
            inspectionId: 'inspection-2',
            hasSavedPhoto: false,
            editable: false,
            repository: _PhotoRepository(),
            onPendingStateChanged: (_) {},
            capturePhoto: (_) async => _tinyPng,
          ),
        ),
      ),
    );

    expect(find.text('No photo evidence recorded.'), findsOneWidget);
    expect(find.text('Take photo'), findsNothing);
    expect(find.text('Remove saved photo'), findsNothing);
  });

  testWidgets(
    'photo card shows asset and condition with a stacked narrow layout',
    (tester) async {
      tester.view.physicalSize = const Size(360, 800);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: SingleChildScrollView(
              child: InspectionPhotoEvidenceCard(
                inspectionId: 'inspection-1',
                assetCode: 'FE-001',
                conditionLabel: 'Non-operational',
                hasSavedPhoto: false,
                editable: true,
                repository: _PhotoRepository(),
                onPendingStateChanged: (_) {},
                capturePhoto: (_) async => null,
              ),
            ),
          ),
        ),
      );

      expect(find.text('Inspection Photo Evidence'), findsOneWidget);
      expect(find.text('Optional'), findsOneWidget);
      expect(find.text('Asset: FE-001'), findsOneWidget);
      expect(find.text('Non-operational'), findsOneWidget);
      expect(
        find.byKey(const Key('inspection-photo-camera-placeholder')),
        findsOneWidget,
      );
      expect(
        tester.getTopLeft(find.text('Asset: FE-001')).dy,
        greaterThan(
          tester
              .getBottomLeft(
                find.byKey(
                  const Key('inspection-photo-thumbnail-inspection-1'),
                ),
              )
              .dy,
        ),
      );
    },
  );

  testWidgets('saved evidence loads a real thumbnail for its inspection row', (
    tester,
  ) async {
    final repository = _PhotoRepository()..savedPhoto = _tinyPng;
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: InspectionPhotoEvidenceCard(
            inspectionId: 'inspection-2',
            assetCode: 'FE-002',
            conditionLabel: 'Operational',
            hasSavedPhoto: true,
            editable: false,
            repository: repository,
            onPendingStateChanged: (_) {},
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(repository.readCount, 1);
    expect(
      find.byKey(const Key('inspection-photo-saved-preview-inspection-2')),
      findsOneWidget,
    );
    expect(find.text('Asset: FE-002'), findsOneWidget);
  });
}

final Uint8List _tinyPng = Uint8List.fromList(
  image.encodePng(image.Image(width: 1, height: 1)),
);

class _PhotoRepository implements InspectionPhotoEvidenceRepository {
  bool failNextUpload = false;
  Uint8List? savedPhoto;
  int readCount = 0;
  int deleteCount = 0;

  @override
  Future<Uint8List> getInspectionPhoto(String inspectionId) async {
    readCount++;
    return savedPhoto ?? _tinyPng;
  }

  @override
  Future<void> saveInspectionPhoto(
    String inspectionId,
    Uint8List jpegBytes,
  ) async {
    if (failNextUpload) {
      failNextUpload = false;
      throw StateError('Upload failed');
    }
    savedPhoto = jpegBytes;
  }

  @override
  Future<void> deleteInspectionPhoto(String inspectionId) async {
    deleteCount++;
    savedPhoto = null;
  }
}

class _BinaryTransport extends http.BaseClient {
  final requests = <http.Request>[];

  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    final binaryRequest = request as http.Request;
    requests.add(binaryRequest);
    final responseBytes = binaryRequest.method == 'GET'
        ? Uint8List.fromList([0xff, 0xd8])
        : Uint8List(0);
    return http.StreamedResponse(
      Stream.value(responseBytes),
      200,
      headers: binaryRequest.method == 'GET'
          ? {'content-type': 'image/jpeg'}
          : const {},
      request: binaryRequest,
    );
  }
}
