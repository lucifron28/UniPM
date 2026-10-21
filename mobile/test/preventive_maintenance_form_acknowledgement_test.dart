import 'dart:convert';
import 'dart:ui' as ui;

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

import 'package:mobile/api/api_client.dart';
import 'package:mobile/api/api_exception.dart';
import 'package:mobile/auth/auth_models.dart';
import 'package:mobile/features/preventive_maintenance/preventive_maintenance_acknowledgement_page.dart';
import 'package:mobile/features/preventive_maintenance/preventive_maintenance_controller.dart';
import 'package:mobile/features/preventive_maintenance/preventive_maintenance_models.dart';
import 'package:mobile/features/preventive_maintenance/preventive_maintenance_page.dart';
import 'package:mobile/features/preventive_maintenance/preventive_maintenance_repository.dart';

const inspectorId = '11111111-1111-4111-8111-111111111111';
const formId = '22222222-2222-4222-8222-222222222222';
const inspectionId = '33333333-3333-4333-8333-333333333333';
const scheduleId = '44444444-4444-4444-8444-444444444444';
const acknowledgementId = '55555555-5555-4555-8555-555555555555';

AuthUser testUser() => const AuthUser(
  id: inspectorId,
  email: 'inspector@example.test',
  displayName: 'Synthetic Inspector',
  roles: ['Inspector'],
);

PreventiveMaintenanceForm submittedForm({bool includeAssetMetadata = true}) =>
    PreventiveMaintenanceForm.fromJson(
      _submittedFormJson(includeAssetMetadata: includeAssetMetadata),
    );

Map<String, dynamic> _submittedFormJson({bool includeAssetMetadata = true}) =>
    <String, dynamic>{
      'id': formId,
      'fileNumber': 'PM-2026-0001',
      'assetCategory': 'fire-extinguisher',
      'building': 'Main Building',
      'department': 'GSD',
      'periodType': 'Quarter',
      'quarter': 'Q1',
      'semester': null,
      'year': 2026,
      'academicYear': '2026-2027',
      'status': 'Submitted',
      'createdByUserId': inspectorId,
      'submittedByUserId': inspectorId,
      'submittedAt': '2026-02-10T00:00:00Z',
      'createdAt': '2026-02-10T00:00:00Z',
      'updatedAt': '2026-02-10T00:00:00Z',
      'inspections': [
        <String, dynamic>{
          'id': inspectionId,
          'scheduleId': scheduleId,
          'assetId': '66666666-6666-4666-8666-666666666666',
          if (includeAssetMetadata) 'assetCode': 'FE-2026-0001',
          if (includeAssetMetadata) 'location': 'Room 101',
          'inspectorUserId': inspectorId,
          'dateInspected': '2026-02-09T16:30:00Z',
          'isOperational': false,
          'remarks': 'Low pressure',
          'actionsRecommendations': 'Inspect gauge',
          'createdAt': '2026-02-10T00:00:00Z',
          'updatedAt': '2026-02-10T00:00:00Z',
        },
      ],
    };

PreventiveMaintenanceForm submittedFormWithInspectionCount(
  int count, {
  int nonOperationalInspectionCount = 1,
}) {
  final json = _submittedFormJson();
  final template =
      (json['inspections'] as List<dynamic>).single as Map<String, dynamic>;
  json['inspections'] = List<Map<String, dynamic>>.generate(count, (index) {
    final date = DateTime.utc(2026, 2, 9, 16, 30 + index);
    final isOperational = index >= nonOperationalInspectionCount;
    return {
      ...template,
      'id': _syntheticUuid('10000000', index),
      'scheduleId': _syntheticUuid('20000000', index),
      'assetId': _syntheticUuid('30000000', index),
      'assetCode': 'FE-2026-${(index + 1).toString().padLeft(4, '0')}',
      'dateInspected': date.toIso8601String(),
      'isOperational': isOperational,
      'remarks': isOperational
          ? 'Routine check'
          : 'Low pressure detected at the gauge. Verify pressure stability and inspect the valve assembly.',
      'actionsRecommendations': isOperational
          ? 'None'
          : 'Inspect the gauge and valve assembly with the approved test instrument, then record the follow-up maintenance action.',
    };
  });
  return PreventiveMaintenanceForm.fromJson(json);
}

String _syntheticUuid(String namespace, int index) =>
    '$namespace-0000-4000-8000-${(index + 1).toRadixString(16).padLeft(12, '0')}';

PreventiveMaintenanceAcknowledgement testAcknowledgement() {
  return PreventiveMaintenanceAcknowledgement(
    id: acknowledgementId,
    formId: formId,
    signatoryName: 'Synthetic Department Head',
    signatoryPosition: 'Department Head',
    signatureContentType: 'image/png',
    signatureChecksum: 'SYNTHETIC-CHECKSUM',
    capturedByUserId: inspectorId,
    acknowledgedAt: DateTime.utc(2026, 2, 10, 9),
  );
}

Map<String, dynamic> acknowledgementJson() => <String, dynamic>{
  'id': acknowledgementId,
  'formId': formId,
  'signatoryName': 'Synthetic Department Head',
  'signatoryPosition': 'Department Head',
  'signatureContentType': 'image/png',
  'signatureChecksum': 'SYNTHETIC-CHECKSUM',
  'capturedByUserId': inspectorId,
  'acknowledgedAt': '2026-02-10T09:00:00Z',
};

class FakeAcknowledgementRepository implements PreventiveMaintenanceRepository {
  FakeAcknowledgementRepository({this.nextFailure}) : form = submittedForm();

  PreventiveMaintenanceForm form;
  Object? nextFailure;
  int acknowledgementCallCount = 0;
  AcknowledgePreventiveMaintenanceInput? acknowledgementInput;

  @override
  Future<PreventiveMaintenanceAcknowledgement> acknowledgeForm(
    String formId,
    AcknowledgePreventiveMaintenanceInput input,
  ) async {
    acknowledgementCallCount++;
    acknowledgementInput = input;
    final failure = nextFailure;
    nextFailure = null;
    if (failure != null) throw failure;
    form = form.copyWith(status: 'Acknowledged');
    return testAcknowledgement();
  }

  @override
  Future<List<PreventiveMaintenanceForm>> listForms() async => [form];

  @override
  Future<PreventiveMaintenanceForm> getForm(String id) async => form;

  @override
  Future<List<ScheduleOption>> listSchedules({String? assetId}) async => [];

  @override
  Future<PreventiveMaintenanceForm> createForm(
    CreatePreventiveMaintenanceFormInput input,
  ) => throw UnimplementedError();

  @override
  Future<PreventiveMaintenanceForm> submitForm(String formId) =>
      throw UnimplementedError();

  @override
  Future<List<ReferenceOption>> listAssetCategories() =>
      throw UnimplementedError();

  @override
  Future<List<ReferenceOption>> listPeriodTypes() => throw UnimplementedError();

  @override
  Future<List<ReferenceOption>> listQuarters() => throw UnimplementedError();

  @override
  Future<PreventiveMaintenanceInspection> addInspection(
    String formId,
    AddInspectionInput input,
  ) => throw UnimplementedError();

  @override
  Future<PreventiveMaintenanceInspection> updateInspection(
    String formId,
    String inspectionId,
    UpdateInspectionInput input,
  ) => throw UnimplementedError();

  @override
  Future<void> deleteInspection(String formId, String inspectionId) =>
      throw UnimplementedError();
}

void main() {
  test(
    'acknowledgement repository posts the approved backend contract',
    () async {
      http.Request? capturedRequest;
      final client = ApiClient(
        baseUrl: Uri.parse('http://localhost:5000/'),
        httpClient: MockClient((request) async {
          capturedRequest = request;
          return http.Response(jsonEncode(acknowledgementJson()), 200);
        }),
      );
      client.configureSession(
        accessTokenProvider: () => 'inspector-access-token',
        terminalAuthFailureHandler: () async {},
      );

      final repository = ApiPreventiveMaintenanceRepository(client);
      final response = await repository.acknowledgeForm(
        formId,
        const AcknowledgePreventiveMaintenanceInput(
          signatoryName: 'Synthetic Department Head',
          signatoryPosition: 'Department Head',
          signatureData: 'iVBORw0KGgo=',
          signatureContentType: 'image/png',
        ),
      );

      expect(response.formId, formId);
      expect(
        capturedRequest?.url.path,
        '/api/v1/preventive-maintenance-forms/$formId/acknowledge',
      );
      expect(
        capturedRequest?.headers['authorization'],
        'Bearer inspector-access-token',
      );
      expect(jsonDecode(capturedRequest!.body), {
        'signatoryName': 'Synthetic Department Head',
        'signatoryPosition': 'Department Head',
        'signatureData': 'iVBORw0KGgo=',
        'signatureContentType': 'image/png',
      });
      client.dispose();
    },
  );

  testWidgets('submitted form can be reviewed from the mobile registry', (
    tester,
  ) async {
    final repository = FakeAcknowledgementRepository();

    await tester.pumpWidget(
      MaterialApp(
        home: PreventiveMaintenancePage(
          repository: repository,
          user: testUser(),
        ),
      ),
    );
    await tester.pumpAndSettle();

    expect(find.textContaining('Awaiting acknowledgement |'), findsOneWidget);
    expect(find.byKey(Key('review-form-$formId')), findsOneWidget);
    await tester.tap(find.byKey(Key('review-form-$formId')));
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('open-acknowledgement')), findsOneWidget);
    await tester.tap(find.byKey(const Key('open-acknowledgement')));
    await tester.pumpAndSettle();
    expect(find.text('Acknowledge PM form'), findsOneWidget);
    expect(find.text('Department Head acknowledgement'), findsOneWidget);
    expect(find.text('Awaiting acknowledgement'), findsOneWidget);
    expect(find.text('Fire Extinguisher'), findsOneWidget);
    expect(find.text('GSD · Main Building'), findsOneWidget);
    expect(find.text('1 total'), findsOneWidget);
    expect(find.text('0 operational'), findsOneWidget);
    expect(find.text('1 non-operational'), findsOneWidget);
    expect(find.text('FE-2026-0001'), findsOneWidget);
    expect(find.text('Room 101'), findsOneWidget);
    expect(find.text('Non-operational'), findsOneWidget);
    expect(find.text('Remarks: Low pressure'), findsOneWidget);
    expect(find.text('Recommendations: Inspect gauge'), findsOneWidget);
    final row = find.byKey(
      const Key(
        'inspection-row-expansion-33333333-3333-4333-8333-333333333333',
      ),
    );
    await tester.ensureVisible(row);
    await tester.tap(row);
    await tester.pumpAndSettle();
    final localInspectionDate = DateTime.parse(
      '2026-02-09T16:30:00Z',
    ).toLocal().toIso8601String().substring(0, 10);
    expect(find.text('Inspection date: $localInspectionDate'), findsOneWidget);
    expect(find.text('Remarks: Low pressure'), findsOneWidget);
    expect(find.text('Recommendations: Inspect gauge'), findsOneWidget);
    expect(find.textContaining(inspectionId), findsNothing);
    expect(find.textContaining(scheduleId), findsNothing);
    expect(
      find.textContaining('66666666-6666-4666-8666-666666666666'),
      findsNothing,
    );

    await tester.tap(find.byKey(const Key('back-to-pm-forms')));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('open-acknowledgement')), findsOneWidget);
  });

  testWidgets('missing asset metadata does not expose row identifiers', (
    tester,
  ) async {
    final repository = FakeAcknowledgementRepository()
      ..form = submittedForm(includeAssetMetadata: false);
    final controller = PreventiveMaintenanceController(
      repository: repository,
      user: testUser(),
    );
    controller.selectForm(repository.form);

    await tester.pumpWidget(
      MaterialApp(
        home: PreventiveMaintenanceAcknowledgementPage(
          controller: controller,
          form: repository.form,
        ),
      ),
    );
    await tester.pumpAndSettle();

    final row = find.byKey(
      const Key(
        'inspection-row-expansion-33333333-3333-4333-8333-333333333333',
      ),
    );
    await tester.ensureVisible(row);
    await tester.tap(row);
    await tester.pumpAndSettle();
    expect(find.text('Asset details unavailable'), findsOneWidget);
    expect(find.text('Location not recorded'), findsOneWidget);
    expect(find.textContaining(inspectionId), findsNothing);
    expect(find.textContaining(scheduleId), findsNothing);
    controller.dispose();
  });

  testWidgets(
    'long forms with multiple findings keep signature access reachable',
    (tester) async {
      tester.view.physicalSize = const Size(360, 640);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      final repository = FakeAcknowledgementRepository()
        ..form = submittedFormWithInspectionCount(
          24,
          nonOperationalInspectionCount: 8,
        );
      final controller = PreventiveMaintenanceController(
        repository: repository,
        user: testUser(),
      );
      controller.selectForm(repository.form);

      await tester.pumpWidget(
        MaterialApp(
          home: Builder(
            builder: (context) => MediaQuery(
              data: MediaQuery.of(
                context,
              ).copyWith(textScaler: TextScaler.linear(1.25)),
              child: PreventiveMaintenanceAcknowledgementPage(
                controller: controller,
                form: repository.form,
              ),
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();

      expect(find.text('24 total'), findsOneWidget);
      expect(find.text('16 operational'), findsOneWidget);
      expect(find.text('8 non-operational'), findsOneWidget);
      expect(
        find.byKey(
          Key('non-operational-preview-${_syntheticUuid('10000000', 7)}'),
        ),
        findsOneWidget,
      );
      expect(
        find.text(
          'Recommendations: Inspect the gauge and valve assembly with the '
          'approved test instrument, then record the follow-up maintenance action.',
        ),
        findsNWidgets(8),
      );

      final scrollable = find
          .descendant(
            of: find.byKey(const Key('acknowledgement-page')),
            matching: find.byType(Scrollable),
          )
          .first;
      final finalRow = find
          .byKey(
            Key('inspection-row-expansion-${_syntheticUuid('10000000', 23)}'),
          )
          .hitTestable();
      await tester.scrollUntilVisible(finalRow, 200, scrollable: scrollable);
      await tester.tap(finalRow);
      await tester.pumpAndSettle();
      final finalDate = DateTime.parse(
        '2026-02-09T16:53:00Z',
      ).toLocal().toIso8601String().substring(0, 10);
      final finalInspection = find.text('Inspection date: $finalDate');
      await tester.scrollUntilVisible(
        finalInspection.hitTestable(),
        200,
        scrollable: scrollable,
      );
      expect(finalInspection, findsOneWidget);

      await tester.tap(find.byKey(const Key('jump-to-acknowledgement-form')));
      await tester.pumpAndSettle();
      final nameField = find.byKey(const Key('ack-signatory-name'));
      expect(nameField, findsOneWidget);
      expect(tester.getRect(nameField).bottom, lessThan(640));

      final canvas = find.byKey(const Key('signature-canvas'));
      await tester.ensureVisible(canvas);
      await tester.pumpAndSettle();
      expect(tester.getRect(canvas).bottom, lessThan(640));
      controller.dispose();
    },
  );

  testWidgets(
    'signature drawing locks and restores acknowledgement scrolling',
    (tester) async {
      final repository = FakeAcknowledgementRepository();
      final controller = PreventiveMaintenanceController(
        repository: repository,
        user: testUser(),
      );
      controller.selectForm(repository.form);

      await tester.pumpWidget(
        MaterialApp(
          home: PreventiveMaintenanceAcknowledgementPage(
            controller: controller,
            form: repository.form,
          ),
        ),
      );
      await tester.pumpAndSettle();

      final page = find.byKey(const Key('acknowledgement-page'));
      final canvas = find.byKey(const Key('signature-canvas'));
      await _ensureVisible(tester, canvas);
      final initialOffset = _acknowledgementScrollPosition(tester).pixels;

      final drawing = await tester.startGesture(tester.getCenter(canvas));
      await tester.pump();
      expect(
        tester.widget<ListView>(page).physics,
        isA<NeverScrollableScrollPhysics>(),
      );
      await drawing.moveBy(const Offset(0, -80));
      await tester.pump();
      expect(_acknowledgementScrollPosition(tester).pixels, initialOffset);
      expect(
        tester
            .state<PmSignaturePadState>(find.byType(PmSignaturePad))
            .hasSignature,
        isTrue,
      );

      await drawing.up();
      await tester.pump();
      expect(
        tester.widget<ListView>(page).physics,
        isNot(isA<NeverScrollableScrollPhysics>()),
      );
      await _dragAcknowledgementList(tester);

      final clearButton = find.byKey(const Key('clear-signature'));
      await _ensureVisible(tester, clearButton);
      final drawingDuringClear = await tester.startGesture(
        tester.getCenter(canvas),
        pointer: 10,
      );
      await tester.pump();
      expect(
        tester.widget<ListView>(page).physics,
        isA<NeverScrollableScrollPhysics>(),
      );
      final clearTap = await tester.startGesture(
        tester.getCenter(clearButton),
        pointer: 11,
      );
      await clearTap.up();
      await tester.pump();
      expect(
        tester
            .state<PmSignaturePadState>(find.byType(PmSignaturePad))
            .hasSignature,
        isFalse,
      );
      expect(
        tester.widget<ListView>(page).physics,
        isNot(isA<NeverScrollableScrollPhysics>()),
      );
      await drawingDuringClear.up();
      await tester.pump();
      await _dragAcknowledgementList(tester);

      await _ensureVisible(tester, canvas);
      final cancellation = await tester.startGesture(tester.getCenter(canvas));
      await tester.pump();
      expect(
        tester.widget<ListView>(page).physics,
        isA<NeverScrollableScrollPhysics>(),
      );
      await cancellation.cancel();
      await tester.pump();
      expect(
        tester.widget<ListView>(page).physics,
        isNot(isA<NeverScrollableScrollPhysics>()),
      );
      await _dragAcknowledgementList(tester);

      controller.dispose();
    },
  );

  testWidgets(
    'signature strokes stay inside the pad and ignore secondary pointers',
    (tester) async {
      tester.view.physicalSize = const Size(360, 640);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      final repository = FakeAcknowledgementRepository();
      final controller = PreventiveMaintenanceController(
        repository: repository,
        user: testUser(),
      );
      controller.selectForm(repository.form);
      await tester.pumpWidget(
        MaterialApp(
          home: PreventiveMaintenanceAcknowledgementPage(
            controller: controller,
            form: repository.form,
          ),
        ),
      );
      await tester.pumpAndSettle();

      final page = find.byKey(const Key('acknowledgement-page'));
      final canvas = find.byKey(const Key('signature-canvas'));
      await _ensureVisible(tester, canvas);
      final bounds = tester.getRect(canvas);
      final state = tester.state<PmSignaturePadState>(
        find.byType(PmSignaturePad),
      );

      expect(await state.toPngBase64(), isNull);
      final outsideStart = bounds.centerLeft - const Offset(2, 0);
      final outsideDrawing = await tester.startGesture(
        outsideStart,
        pointer: 20,
      );
      await outsideDrawing.moveTo(bounds.centerLeft + const Offset(8, 0));
      await outsideDrawing.up();
      await tester.pump();
      expect(state.hasSignature, isFalse);

      final start = bounds.topLeft + const Offset(28, 32);
      final inside = bounds.topLeft + const Offset(70, 46);
      final drawing = await tester.startGesture(start, pointer: 21);
      await drawing.moveTo(inside);
      await tester.pump();
      expect(state.activePointer, 21);
      expect(
        tester.widget<ListView>(page).physics,
        isA<NeverScrollableScrollPhysics>(),
      );

      final secondary = await tester.startGesture(
        bounds.topLeft + const Offset(120, 40),
        pointer: 22,
      );
      await secondary.moveTo(bounds.topLeft + const Offset(180, 70));
      await secondary.up();
      await tester.pump();
      expect(state.strokes, hasLength(1));
      expect(state.strokes.single, hasLength(2));

      await drawing.moveTo(Offset(bounds.right + 8, inside.dy));
      await tester.pump();
      expect(
        tester.widget<ListView>(page).physics,
        isNot(isA<NeverScrollableScrollPhysics>()),
      );

      final secondaryAfterExit = await tester.startGesture(
        bounds.topLeft + const Offset(150, 60),
        pointer: 23,
      );
      await secondaryAfterExit.moveTo(bounds.topLeft + const Offset(210, 80));
      await secondaryAfterExit.up();
      await drawing.moveTo(bounds.topLeft + const Offset(220, 90));
      await tester.pump();
      expect(state.strokes.single, hasLength(2));
      await drawing.up();
      await tester.pump();
      expect(state.activePointer, isNull);
      expect(state.hasSignature, isTrue);
      controller.dispose();
    },
  );

  testWidgets('undo and clear update signature state and export only ink', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(360, 640);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);

    final repository = FakeAcknowledgementRepository();
    final controller = PreventiveMaintenanceController(
      repository: repository,
      user: testUser(),
    );
    controller.selectForm(repository.form);
    await tester.pumpWidget(
      MaterialApp(
        home: PreventiveMaintenanceAcknowledgementPage(
          controller: controller,
          form: repository.form,
        ),
      ),
    );
    await tester.pumpAndSettle();

    final canvas = find.byKey(const Key('signature-canvas'));
    await _ensureVisible(tester, canvas);
    final bounds = tester.getRect(canvas);
    final state = tester.state<PmSignaturePadState>(
      find.byType(PmSignaturePad),
    );
    final renderBox =
        state.repaintKey.currentContext!.findRenderObject() as RenderBox;
    final surfaceSize = renderBox.size;
    final start = bounds.topLeft + const Offset(28, 28);
    final end = bounds.topLeft + const Offset(74, 46);
    final expectedStartLocal = renderBox.globalToLocal(start);
    final drawing = await tester.startGesture(start, pointer: 30);
    await drawing.moveTo(end);
    await drawing.up();
    await tester.pump();
    expect(state.strokes, hasLength(1));
    expect(state.strokes.first.first, expectedStartLocal);

    final pngSnapshot = await tester.runAsync(() async {
      final encoded = await state.toPngBase64();
      if (encoded == null) return null;
      final codec = await ui.instantiateImageCodec(base64Decode(encoded));
      try {
        final frame = await codec.getNextFrame();
        try {
          final pixelData = await frame.image.toByteData(
            format: ui.ImageByteFormat.rawRgba,
          );
          if (pixelData == null) return null;
          return (
            encoded: encoded,
            width: frame.image.width,
            height: frame.image.height,
            pixels: pixelData.buffer.asUint8List(
              pixelData.offsetInBytes,
              pixelData.lengthInBytes,
            ),
          );
        } finally {
          frame.image.dispose();
        }
      } finally {
        codec.dispose();
      }
    });
    expect(pngSnapshot, isNotNull);
    final snapshot = pngSnapshot!;
    expect(snapshot.encoded, isNotEmpty);
    expect(snapshot.width, surfaceSize.width.ceil());
    expect(snapshot.height, surfaceSize.height.ceil());
    int redAt(int x, int y) => snapshot.pixels[(y * snapshot.width + x) * 4];
    expect(
      redAt(expectedStartLocal.dx.round(), expectedStartLocal.dy.round()),
      0,
    );
    expect(redAt(snapshot.width ~/ 2, snapshot.height ~/ 2), 255);

    Future<void> drawStroke(int pointer, int verticalOffset) async {
      await _ensureVisible(tester, canvas);
      final currentBounds = tester.getRect(canvas);
      final strokeStart =
          currentBounds.topLeft + Offset(30, 50 + verticalOffset.toDouble());
      final gesture = await tester.startGesture(strokeStart, pointer: pointer);
      await gesture.moveTo(strokeStart + const Offset(36, 14));
      await gesture.up();
      await tester.pump();
    }

    await drawStroke(31, 0);
    await drawStroke(32, 25);
    expect(state.strokes, hasLength(3));
    final undo = find.byKey(const Key('undo-signature'));
    await _ensureVisible(tester, undo);
    await tester.tap(undo);
    await tester.pump();
    expect(state.strokes, hasLength(2));
    expect(state.hasSignature, isTrue);
    await tester.tap(undo);
    await tester.pump();
    expect(state.strokes, hasLength(1));
    expect(state.hasSignature, isTrue);
    await tester.tap(undo);
    await tester.pump();
    expect(state.hasSignature, isFalse);
    expect(await state.toPngBase64(), isNull);

    await drawStroke(33, 0);
    final clear = find.byKey(const Key('clear-signature'));
    await _ensureVisible(tester, clear);
    await tester.tap(clear);
    await tester.pump();
    expect(state.hasSignature, isFalse);
    expect(await state.toPngBase64(), isNull);
    controller.dispose();
  });

  testWidgets('acknowledgement captures signatory data and locks the form', (
    tester,
  ) async {
    final repository = FakeAcknowledgementRepository();
    final controller = PreventiveMaintenanceController(
      repository: repository,
      user: testUser(),
    );
    controller.selectForm(repository.form);

    await tester.pumpWidget(
      MaterialApp(
        home: PreventiveMaintenanceAcknowledgementPage(
          controller: controller,
          form: repository.form,
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.enterText(
      find.byKey(const Key('ack-signatory-name')),
      'Synthetic Department Head',
    );
    await tester.enterText(
      find.byKey(const Key('ack-signatory-position')),
      'Department Head',
    );
    await _drawSignature(tester);
    await _ensureVisible(
      tester,
      find.byKey(const Key('acknowledge-form-button')),
    );
    await tester.tap(find.byKey(const Key('acknowledge-form-button')));
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('confirm-acknowledge-form')), findsOneWidget);
    expect(
      find.text(
        'Field-work completion and acknowledgement are separate. '
        'Acknowledgement records receipt/noting of the findings and the '
        'Department Head signatory details and signature, changes the form '
        'to Acknowledged, and locks it. Its inspection rows become eligible '
        'for official history. It does not approve corrective work, funding, '
        'or an RMRF.',
      ),
      findsOneWidget,
    );
    expect(find.textContaining('completes its linked schedules'), findsNothing);

    await tester.tap(find.byKey(const Key('confirm-acknowledge-form')));
    await tester.pumpAndSettle();

    expect(repository.acknowledgementCallCount, 1);
    expect(
      repository.acknowledgementInput?.signatoryName,
      'Synthetic Department Head',
    );
    expect(
      repository.acknowledgementInput?.signatoryPosition,
      'Department Head',
    );
    final signatureData = repository.acknowledgementInput?.signatureData;
    expect(signatureData, isNotNull);
    expect(base64Decode(signatureData!).take(8), [
      137,
      80,
      78,
      71,
      13,
      10,
      26,
      10,
    ]);
    expect(controller.selectedForm?.status, 'Acknowledged');
    expect(find.byKey(const Key('acknowledgement-receipt')), findsOneWidget);
    expect(find.text('Form acknowledged'), findsOneWidget);
    expect(find.text('PM form details'), findsOneWidget);
    final acknowledgedAt = DateTime.parse(
      '2026-02-10T09:00:00Z',
    ).toLocal().toIso8601String().replaceFirst('T', ' ').substring(0, 16);
    final receipt = find.byKey(const Key('acknowledgement-receipt'));
    expect(
      find.descendant(of: receipt, matching: find.text('PM-2026-0001')),
      findsOneWidget,
    );
    expect(
      find.descendant(
        of: receipt,
        matching: find.text('Department Head: Synthetic Department Head'),
      ),
      findsOneWidget,
    );
    expect(
      find.descendant(
        of: receipt,
        matching: find.text('Position: Department Head'),
      ),
      findsOneWidget,
    );
    expect(
      find.descendant(
        of: receipt,
        matching: find.text('Acknowledged: $acknowledgedAt'),
      ),
      findsOneWidget,
    );
    expect(
      find.descendant(of: receipt, matching: find.text('Read-only form')),
      findsOneWidget,
    );
    expect(find.textContaining('Signature type:'), findsNothing);
    expect(find.byKey(const Key('acknowledge-form-button')), findsNothing);
    controller.dispose();
  });

  testWidgets('confirmation cancellation and missing signature do not write', (
    tester,
  ) async {
    final repository = FakeAcknowledgementRepository();
    final controller = PreventiveMaintenanceController(
      repository: repository,
      user: testUser(),
    );
    controller.selectForm(repository.form);

    await tester.pumpWidget(
      MaterialApp(
        home: PreventiveMaintenanceAcknowledgementPage(
          controller: controller,
          form: repository.form,
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.enterText(
      find.byKey(const Key('ack-signatory-name')),
      'Synthetic Department Head',
    );
    await tester.enterText(
      find.byKey(const Key('ack-signatory-position')),
      'Department Head',
    );
    await _ensureVisible(
      tester,
      find.byKey(const Key('acknowledge-form-button')),
    );
    await tester.tap(find.byKey(const Key('acknowledge-form-button')));
    await tester.pumpAndSettle();
    expect(find.text('Capture the Department Head signature.'), findsOneWidget);
    expect(repository.acknowledgementCallCount, 0);

    await _drawSignature(tester);
    await _ensureVisible(
      tester,
      find.byKey(const Key('acknowledge-form-button')),
    );
    await tester.tap(find.byKey(const Key('acknowledge-form-button')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Cancel'));
    await tester.pumpAndSettle();
    expect(repository.acknowledgementCallCount, 0);
    expect(find.byKey(const Key('acknowledge-form-button')), findsOneWidget);
    controller.dispose();
  });

  testWidgets('acknowledgement errors expose session expiry and allow retry', (
    tester,
  ) async {
    final repository = FakeAcknowledgementRepository(
      nextFailure: const ApiException(
        statusCode: 401,
        message: 'internal auth detail',
      ),
    );
    final controller = PreventiveMaintenanceController(
      repository: repository,
      user: testUser(),
    );
    controller.selectForm(repository.form);

    await tester.pumpWidget(
      MaterialApp(
        home: PreventiveMaintenanceAcknowledgementPage(
          controller: controller,
          form: repository.form,
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.enterText(
      find.byKey(const Key('ack-signatory-name')),
      'Synthetic Department Head',
    );
    await tester.enterText(
      find.byKey(const Key('ack-signatory-position')),
      'Department Head',
    );
    await _drawSignature(tester);
    await _ensureVisible(
      tester,
      find.byKey(const Key('acknowledge-form-button')),
    );
    await tester.tap(find.byKey(const Key('acknowledge-form-button')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('confirm-acknowledge-form')));
    await tester.pumpAndSettle();

    expect(
      controller.errorMessage,
      'Your session expired. Please sign in again.',
    );
    expect(controller.selectedForm?.status, 'Submitted');
    expect(controller.acknowledgement, isNull);
    final errorText = tester.widget<Text>(
      find.byKey(const Key('acknowledgement-error'), skipOffstage: false),
    );
    expect(errorText.data, 'Your session expired. Please sign in again.');
    expect(find.text('internal auth detail'), findsNothing);

    await tester.tap(find.byKey(const Key('acknowledge-form-button')));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('confirm-acknowledge-form')));
    await tester.pumpAndSettle();
    expect(repository.acknowledgementCallCount, 2);
    expect(find.byKey(const Key('acknowledgement-receipt')), findsOneWidget);
    controller.dispose();
  });

  test(
    'controller prevents duplicate acknowledgement after lifecycle boundary',
    () async {
      final repository = FakeAcknowledgementRepository();
      final controller = PreventiveMaintenanceController(
        repository: repository,
        user: testUser(),
      );
      controller.selectForm(repository.form.copyWith(status: 'Acknowledged'));

      final result = await controller.acknowledgeForm(
        const AcknowledgePreventiveMaintenanceInput(
          signatoryName: 'Synthetic Department Head',
          signatoryPosition: 'Department Head',
          signatureData: 'iVBORw0KGgo=',
          signatureContentType: 'image/png',
        ),
      );

      expect(result, isNull);
      expect(repository.acknowledgementCallCount, 0);
      expect(
        controller.errorMessage,
        'Only forms awaiting acknowledgement can be acknowledged.',
      );
      controller.dispose();
    },
  );
}

Future<void> _drawSignature(WidgetTester tester) async {
  final canvas = find.byKey(const Key('signature-canvas'));
  await _ensureVisible(tester, canvas);
  final gesture = await tester.startGesture(tester.getCenter(canvas));
  await gesture.moveBy(const Offset(120, 30));
  await gesture.moveBy(const Offset(80, -20));
  await gesture.up();
  await tester.pump();
  final state = tester.state<PmSignaturePadState>(find.byType(PmSignaturePad));
  expect(state.hasSignature, isTrue);
  expect(await state.toPngBase64(), isNotNull);
}

Future<void> _ensureVisible(WidgetTester tester, Finder finder) async {
  if (finder.evaluate().isEmpty) {
    final scrollable = find.descendant(
      of: find.byKey(const Key('acknowledgement-page')),
      matching: find.byType(Scrollable),
    );
    await tester.scrollUntilVisible(finder, 200, scrollable: scrollable.first);
  }
  await tester.ensureVisible(finder);
  await tester.pumpAndSettle();
}

ScrollPosition _acknowledgementScrollPosition(WidgetTester tester) {
  final scrollable = find.descendant(
    of: find.byKey(const Key('acknowledgement-page')),
    matching: find.byType(Scrollable),
  );
  return tester.state<ScrollableState>(scrollable.first).position;
}

Future<void> _dragAcknowledgementList(WidgetTester tester) async {
  final before = _acknowledgementScrollPosition(tester).pixels;
  final delta = Offset(0, before > 0 ? 100 : -100);
  await tester.dragFrom(const Offset(5, 150), delta);
  await tester.pumpAndSettle();
  expect(_acknowledgementScrollPosition(tester).pixels, isNot(before));
}
