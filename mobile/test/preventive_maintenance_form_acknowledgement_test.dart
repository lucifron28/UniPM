import 'dart:convert';
import 'dart:io';
import 'dart:typed_data';

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
    expect(find.text('Department Head acknowledgement'), findsOneWidget);
    expect(find.text('Acknowledge PM form'), findsOneWidget);
    expect(find.text('Awaiting acknowledgement'), findsOneWidget);
    expect(find.text('Fire Extinguisher'), findsOneWidget);
    expect(find.text('GSD · Main Building'), findsOneWidget);
    expect(find.text('FE-2026-0001'), findsOneWidget);
    expect(find.text('Room 101'), findsOneWidget);
    expect(find.text('Non-operational'), findsOneWidget);
    expect(find.text('Low pressure'), findsOneWidget);
    expect(find.text('Inspect gauge'), findsOneWidget);
    expect(find.text('Inspection totals'), findsOneWidget);
    final localInspectionDate = DateTime.parse(
      '2026-02-09T16:30:00Z',
    ).toLocal().toIso8601String().substring(0, 10);
    expect(find.text('Inspection date'), findsNothing);
    await tester.tap(find.byKey(Key('inspection-row-$inspectionId')));
    await tester.pumpAndSettle();
    expect(find.text(localInspectionDate), findsOneWidget);
    expect(find.text('Remarks'), findsOneWidget);
    expect(find.text('Recommendations'), findsOneWidget);
    expect(find.textContaining(inspectionId), findsNothing);
    expect(find.textContaining(scheduleId), findsNothing);
    expect(
      find.textContaining('66666666-6666-4666-8666-666666666666'),
      findsNothing,
    );
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

    expect(find.text('Asset details unavailable'), findsOneWidget);
    expect(find.text('Not recorded'), findsOneWidget);
    expect(find.textContaining(inspectionId), findsNothing);
    expect(find.textContaining(scheduleId), findsNothing);
    controller.dispose();
  });

  testWidgets('signature drawing locks and restores acknowledgement scrolling', (
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
      tester.state<PmSignaturePadState>(find.byType(PmSignaturePad))
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
      tester.state<PmSignaturePadState>(find.byType(PmSignaturePad))
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
    expect(
      tester.state<PmSignaturePadState>(find.byType(PmSignaturePad))
          .hasSignature,
      isFalse,
    );
    await _dragAcknowledgementList(tester);

    controller.dispose();
  });

  testWidgets(
    'signature input stops at its bounds and ignores secondary pointers',
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

      final canvas = find.byKey(const Key('signature-canvas'));
      await _ensureVisible(tester, canvas);
      final bounds = tester.getRect(canvas);
      final firstPointer = await tester.startGesture(
        bounds.center,
        pointer: 21,
      );
      await tester.pump();
      final state = tester.state<PmSignaturePadState>(
        find.byType(PmSignaturePad),
      );
      final secondPointer = await tester.startGesture(
        bounds.center + const Offset(12, 0),
        pointer: 22,
      );
      await tester.pump();

      expect(state.strokes, hasLength(1));
      expect(state.strokes.single, hasLength(1));
      expect(tester.getRect(find.byKey(state.surfaceKey)), bounds);
      expect(tester.getRect(find.byKey(state.repaintKey)), bounds);

      await firstPointer.moveTo(Offset(bounds.center.dx, bounds.bottom + 12));
      await tester.pump();
      expect(state.activePointer, isNull);
      expect(
        tester.widget<ListView>(find.byKey(const Key('acknowledgement-page')))
            .physics,
        isNot(isA<NeverScrollableScrollPhysics>()),
      );

      await secondPointer.moveTo(bounds.center + const Offset(48, 20));
      await firstPointer.moveTo(
        tester.getCenter(find.byKey(const Key('ack-signatory-name'))),
      );
      await tester.pump();

      expect(state.strokes, hasLength(1));
      expect(state.strokes.single, hasLength(1));
      final surfaceSize = tester.getSize(find.byKey(state.surfaceKey));
      for (final point in state.strokes.single) {
        expect(point.dx, inInclusiveRange(0, surfaceSize.width));
        expect(point.dy, inInclusiveRange(0, surfaceSize.height));
      }

      await firstPointer.up();
      await secondPointer.up();
      await tester.pump();
      controller.dispose();
    },
  );

  testWidgets(
    'signature undo and clear update validity and export only signature pixels',
    (tester) async {
      var hasSignature = false;
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: Center(
              child: SizedBox(
                width: 320,
                child: PmSignaturePad(
                  onChanged: (value) => hasSignature = value,
                  onGestureChanged: (_) {},
                ),
              ),
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();

      final canvas = find.byKey(const Key('signature-canvas'));
      final bounds = tester.getRect(canvas);
      expect(find.byKey(const Key('signature-placeholder')), findsOneWidget);

      Future<void> drawStroke(Offset start, Offset end) async {
        final gesture = await tester.startGesture(start);
        await gesture.moveTo(end);
        await gesture.up();
        await tester.pump();
      }

      await drawStroke(
        Offset(bounds.left + 24, bounds.top + 40),
        Offset(bounds.left + 56, bounds.top + 40),
      );
      await drawStroke(
        Offset(bounds.left + 64, bounds.top + 72),
        Offset(bounds.left + 92, bounds.top + 72),
      );

      final state = tester.state<PmSignaturePadState>(
        find.byType(PmSignaturePad),
      );
      expect(hasSignature, isTrue);
      expect(find.byKey(const Key('signature-placeholder')), findsNothing);
      expect(state.strokes, hasLength(2));

      await tester.tap(find.byKey(const Key('undo-signature')));
      await tester.pumpAndSettle();
      expect(state.strokes, hasLength(1));
      expect(hasSignature, isTrue);

      final png = base64Decode((await state.toPngBase64())!);
      final pngData = ByteData.sublistView(png);
      final width = pngData.getUint32(16, Endian.big);
      final height = pngData.getUint32(20, Endian.big);
      expect(width, bounds.width.ceil());
      expect(height, bounds.height.ceil());
      final scanlines = _decodePngScanlines(png);
      expect(scanlines[40 * (width + 1) + 1 + 40], 0);
      expect(scanlines[(height ~/ 2) * (width + 1) + 1 + (width ~/ 2)], 255);

      await tester.tap(find.byKey(const Key('clear-signature')));
      await tester.pumpAndSettle();
      expect(state.strokes, isEmpty);
      expect(state.hasSignature, isFalse);
      expect(hasSignature, isFalse);
      expect(await state.toPngBase64(), isNull);
      expect(find.byKey(const Key('signature-placeholder')), findsOneWidget);
    },
  );

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
        'This records receipt of the findings, locks the form, and makes its '
        'inspection rows eligible for official history. It does not '
        'authorize repairs, funding, or an RMRF.',
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
    expect(find.text('PM-2026-0001'), findsOneWidget);
    expect(find.text('Synthetic Department Head'), findsOneWidget);
    expect(find.text('Department Head'), findsWidgets);
    expect(find.text('This form is now read-only.'), findsOneWidget);
    expect(find.textContaining('Signature type'), findsNothing);
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

Uint8List _decodePngScanlines(Uint8List png) {
  final data = ByteData.sublistView(png);
  final compressed = BytesBuilder();
  var offset = 8;
  while (offset < png.length) {
    final length = data.getUint32(offset, Endian.big);
    final type = String.fromCharCodes(png.sublist(offset + 4, offset + 8));
    if (type == 'IDAT') {
      compressed.add(png.sublist(offset + 8, offset + 8 + length));
    }
    offset += length + 12;
    if (type == 'IEND') break;
  }
  return Uint8List.fromList(ZLibDecoder().convert(compressed.takeBytes()));
}

Future<void> _ensureVisible(WidgetTester tester, Finder finder) async {
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
