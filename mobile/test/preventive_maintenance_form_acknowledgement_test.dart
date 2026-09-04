import 'dart:convert';

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

PreventiveMaintenanceForm submittedForm() {
  final now = DateTime.utc(2026, 2, 10);
  return PreventiveMaintenanceForm(
    id: formId,
    fileNumber: 'PM-2026-0001',
    assetCategory: 'fire-extinguisher',
    building: 'Main Building',
    department: 'GSD',
    periodType: 'Quarter',
    quarter: 'Q1',
    semester: null,
    year: 2026,
    academicYear: '2026-2027',
    status: 'Submitted',
    createdByUserId: inspectorId,
    submittedByUserId: inspectorId,
    submittedAt: now,
    createdAt: now,
    updatedAt: now,
    inspections: [
      PreventiveMaintenanceInspection(
        id: inspectionId,
        scheduleId: scheduleId,
        assetId: '66666666-6666-4666-8666-666666666666',
        inspectorUserId: inspectorId,
        dateInspected: now,
        isOperational: false,
        remarks: 'Low pressure',
        actionsRecommendations: 'Inspect gauge',
        createdAt: now,
        updatedAt: now,
      ),
    ],
  );
}

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

    expect(find.byKey(Key('review-form-$formId')), findsOneWidget);
    await tester.tap(find.byKey(Key('review-form-$formId')));
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('open-acknowledgement')), findsOneWidget);
    await tester.tap(find.byKey(const Key('open-acknowledgement')));
    await tester.pumpAndSettle();
    expect(find.text('Department Head acknowledgement'), findsOneWidget);
    expect(find.text('Asset category: Fire Extinguisher'), findsOneWidget);
    expect(find.text('Remarks: Low pressure'), findsOneWidget);
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
        'Only Submitted forms can be acknowledged.',
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
  await tester.ensureVisible(finder);
  await tester.pumpAndSettle();
}
