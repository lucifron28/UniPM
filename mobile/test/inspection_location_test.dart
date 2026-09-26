import 'dart:async';
import 'dart:convert';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

import 'package:mobile/api/api_client.dart';
import 'package:mobile/auth/auth_models.dart';
import 'package:mobile/features/assets/asset_models.dart';
import 'package:mobile/features/preventive_maintenance/inspection_location_capture.dart';
import 'package:mobile/features/preventive_maintenance/preventive_maintenance_models.dart';
import 'package:mobile/features/preventive_maintenance/preventive_maintenance_repository.dart';
import 'package:mobile/features/preventive_maintenance/scanned_asset_pm_entry.dart';

const _inspectorId = '11111111-1111-4111-8111-111111111111';
const _assetId = '22222222-2222-4222-8222-222222222222';
const _scheduleId = '33333333-3333-4333-8333-333333333333';
const _formId = '44444444-4444-4444-8444-444444444444';
const _inspectionId = '55555555-5555-4555-8555-555555555555';
const _attemptId = '66666666-6666-4666-8666-666666666666';

void main() {
  group('InspectionLocationCapture', () {
    test('requests denied permission, then captures a bounded fix', () async {
      final platform = _FakeDeviceLocationPlatform(
        permission: DeviceLocationPermission.denied,
        permissionRequests: [DeviceLocationPermission.granted],
      );

      final result = await InspectionLocationCapture(
        platform: platform,
      ).capture();

      expect(result.coordinates?.latitude, 14.6);
      expect(platform.permissionRequestCount, 1);
      expect(platform.positionCallCount, 1);
      expect(platform.receivedTimeout, const Duration(seconds: 10));
    });

    test('reports denied and permanently denied permission states', () async {
      final denied = _FakeDeviceLocationPlatform(
        permission: DeviceLocationPermission.denied,
        permissionRequests: [DeviceLocationPermission.denied],
      );
      final permanentlyDenied = _FakeDeviceLocationPlatform(
        permission: DeviceLocationPermission.permanentlyDenied,
      );

      expect(
        (await InspectionLocationCapture(platform: denied).capture()).failure,
        LocationCaptureFailure.permissionDenied,
      );
      expect(
        (await InspectionLocationCapture(
          platform: permanentlyDenied,
        ).capture()).failure,
        LocationCaptureFailure.permissionPermanentlyDenied,
      );
      expect(permanentlyDenied.permissionRequestCount, 0);
      expect(denied.positionCallCount, 0);
    });

    test('does not prompt when location services are disabled', () async {
      final platform = _FakeDeviceLocationPlatform(serviceEnabled: false);

      final result = await InspectionLocationCapture(
        platform: platform,
      ).capture();

      expect(result.failure, LocationCaptureFailure.servicesDisabled);
      expect(platform.permissionCheckCount, 0);
      expect(platform.permissionRequestCount, 0);
    });

    test('bounds a location request that does not return', () async {
      final platform = _FakeDeviceLocationPlatform()
        ..pendingPosition = Completer<DeviceLocationCoordinates>();

      final result = await InspectionLocationCapture(
        platform: platform,
        timeout: const Duration(milliseconds: 5),
      ).capture();

      expect(result.failure, LocationCaptureFailure.timedOut);
    });
  });

  test(
    'API contract sends coordinates and only adds attempt ID on row create',
    () async {
      final requests = <http.Request>[];
      final transport = MockClient((request) async {
        requests.add(request);
        if (request.url.path.endsWith('/location-verification-attempts')) {
          return http.Response(
            jsonEncode({
              'id': _attemptId,
              'outcome': 'NotConfigured',
              'accuracyMeters': 5.0,
              'distanceMeters': null,
            }),
            200,
          );
        }
        return http.Response(jsonEncode(_inspectionJson()), 200);
      });
      final client = ApiClient(
        baseUrl: Uri.parse('https://example.test/'),
        httpClient: transport,
      );
      final repository = ApiPreventiveMaintenanceRepository(client);

      final attempt = await repository.createLocationVerificationAttempt(
        _scheduleId,
        latitude: 14.6,
        longitude: 120.98,
        accuracyMeters: 5,
      );
      await repository.addInspection(
        _formId,
        AddInspectionInput(
          scheduleId: _scheduleId,
          locationAttemptId: attempt.id,
          inspectorUserId: _inspectorId,
          dateInspected: DateTime.utc(2026, 9, 26),
          isOperational: true,
          remarks: null,
          actionsRecommendations: null,
        ),
      );
      await repository.updateInspection(
        _formId,
        _inspectionId,
        UpdateInspectionInput(
          inspectorUserId: _inspectorId,
          dateInspected: DateTime.utc(2026, 9, 26),
          isOperational: true,
          remarks: null,
          actionsRecommendations: null,
        ),
      );

      expect(attempt.outcome, LocationVerificationOutcome.notConfigured);
      expect(requests, hasLength(3));
      expect(
        requests[0].url.path,
        '/api/v1/schedules/$_scheduleId/location-verification-attempts',
      );
      expect(jsonDecode(requests[0].body), {
        'latitude': 14.6,
        'longitude': 120.98,
        'accuracyMeters': 5,
      });
      expect(jsonDecode(requests[1].body)['locationAttemptId'], _attemptId);
      expect(
        jsonDecode(requests[2].body),
        isNot(contains('locationAttemptId')),
      );
      client.dispose();
      transport.close();
    },
  );

  test('maps every backend verification outcome', () {
    const expected = {
      'Inside': LocationVerificationOutcome.inside,
      'Outside': LocationVerificationOutcome.outside,
      'Uncertain': LocationVerificationOutcome.uncertain,
      'NotConfigured': LocationVerificationOutcome.notConfigured,
    };

    for (final entry in expected.entries) {
      final attempt = LocationVerificationAttempt.fromJson({
        'id': _attemptId,
        'outcome': entry.key,
      });
      expect(attempt.outcome, entry.value);
    }
  });

  const visibleOutcomes = <LocationVerificationOutcome, String>{
    LocationVerificationOutcome.inside: 'within the configured inspection area',
    LocationVerificationOutcome.outside:
        'outside the configured inspection area',
    LocationVerificationOutcome.uncertain:
        'could not be confirmed with enough accuracy',
  };
  for (final entry in visibleOutcomes.entries) {
    testWidgets('${entry.key.name} result displays and can continue', (
      tester,
    ) async {
      final platform = _FakeDeviceLocationPlatform();
      final repository = _FakePmRepository(locationOutcomes: [entry.key]);

      await _pumpEntry(tester, repository, platform);
      await tester.tap(find.byKey(const Key('start-pm')));
      await tester.pumpAndSettle();

      expect(find.textContaining(entry.value), findsOneWidget);
      expect(find.byKey(const Key('location-continue')), findsOneWidget);
      await tester.tap(find.byKey(const Key('location-continue')));
      await tester.pumpAndSettle();

      expect(find.byKey(const Key('add-inspection-button')), findsOneWidget);
      expect(repository.locationAttempts, hasLength(1));
    });
  }

  testWidgets(
    'retries denied permission, surfaces missing area, and carries attempt ID to add',
    (tester) async {
      final platform = _FakeDeviceLocationPlatform(
        permission: DeviceLocationPermission.denied,
        permissionRequests: [
          DeviceLocationPermission.denied,
          DeviceLocationPermission.granted,
        ],
      );
      final repository = _FakePmRepository(
        locationOutcomes: [LocationVerificationOutcome.notConfigured],
      );

      await _pumpEntry(tester, repository, platform);
      await tester.tap(find.byKey(const Key('start-pm')));
      await tester.pumpAndSettle();

      expect(
        find.textContaining('Location permission was denied'),
        findsOneWidget,
      );
      await tester.tap(find.byKey(const Key('location-retry')));
      await tester.pumpAndSettle();
      expect(
        find.textContaining('No verification area is configured'),
        findsOneWidget,
      );
      expect(repository.locationAttempts, hasLength(1));

      await tester.tap(find.byKey(const Key('location-continue')));
      await tester.pumpAndSettle();
      final addButton = find.byKey(const Key('add-inspection-button'));
      final listView = find.byType(ListView).last;
      final scrollable = find
          .descendant(of: listView, matching: find.byType(Scrollable))
          .first;
      await tester.scrollUntilVisible(addButton, 300, scrollable: scrollable);
      await tester.pumpAndSettle();
      await tester.tap(addButton);
      await tester.pumpAndSettle();

      expect(repository.addedInput?.locationAttemptId, _attemptId);
      expect(platform.permissionRequestCount, 2);
    },
  );

  testWidgets('disabled services offer continuation without a server attempt', (
    tester,
  ) async {
    final platform = _FakeDeviceLocationPlatform(serviceEnabled: false);
    final repository = _FakePmRepository();

    await _pumpEntry(tester, repository, platform);
    await tester.tap(find.byKey(const Key('start-pm')));
    await tester.pumpAndSettle();

    expect(
      find.textContaining('Location services are turned off'),
      findsOneWidget,
    );
    await tester.tap(find.byKey(const Key('location-continue')));
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('add-inspection-button')), findsOneWidget);
    expect(repository.locationAttempts, isEmpty);
  });

  testWidgets('system Back dismisses verification without starting the form', (
    tester,
  ) async {
    final platform = _FakeDeviceLocationPlatform(serviceEnabled: false);
    final repository = _FakePmRepository();

    await _pumpEntry(tester, repository, platform);
    await tester.tap(find.byKey(const Key('start-pm')));
    await tester.pumpAndSettle();
    expect(
      find.byKey(const Key('location-verification-dialog')),
      findsOneWidget,
    );

    await tester.binding.handlePopRoute();
    await tester.pumpAndSettle();

    expect(find.byKey(const Key('start-pm')), findsOneWidget);
    expect(repository._createdForm, isNull);
  });
}

Future<void> _pumpEntry(
  WidgetTester tester,
  _FakePmRepository repository,
  _FakeDeviceLocationPlatform platform,
) async {
  await tester.pumpWidget(
    MaterialApp(
      home: Scaffold(
        body: SingleChildScrollView(
          child: ScannedAssetPmEntry(
            asset: const Asset(
              id: _assetId,
              assetCode: 'FE-001',
              assetCategory: 'fire-extinguisher',
              building: 'Main Building',
              department: 'GSD',
              location: 'Room 101',
              qrCodeValue: 'UNIPM:ASSET:FE-001',
              status: 'Active',
            ),
            repository: repository,
            user: const AuthUser(
              id: _inspectorId,
              email: 'inspector@example.test',
              displayName: 'Test Inspector',
              roles: ['Inspector'],
            ),
            locationCapture: InspectionLocationCapture(platform: platform),
          ),
        ),
      ),
    ),
  );
  await tester.pumpAndSettle();
}

class _FakeDeviceLocationPlatform implements DeviceLocationPlatform {
  _FakeDeviceLocationPlatform({
    this.serviceEnabled = true,
    this.permission = DeviceLocationPermission.granted,
    List<DeviceLocationPermission> permissionRequests = const [],
  }) : permissionRequests = [...permissionRequests];

  bool serviceEnabled;
  DeviceLocationPermission permission;
  final List<DeviceLocationPermission> permissionRequests;
  Completer<DeviceLocationCoordinates>? pendingPosition;
  int permissionCheckCount = 0;
  int permissionRequestCount = 0;
  int positionCallCount = 0;
  Duration? receivedTimeout;

  @override
  Future<bool> isLocationServiceEnabled() async => serviceEnabled;

  @override
  Future<DeviceLocationPermission> checkPermission() async {
    permissionCheckCount++;
    return permission;
  }

  @override
  Future<DeviceLocationPermission> requestPermission() async {
    permissionRequestCount++;
    if (permissionRequests.isNotEmpty) {
      permission = permissionRequests.removeAt(0);
    }
    return permission;
  }

  @override
  Future<DeviceLocationCoordinates> getCurrentPosition({
    required Duration timeout,
  }) {
    positionCallCount++;
    receivedTimeout = timeout;
    return pendingPosition?.future ??
        Future.value(
          const DeviceLocationCoordinates(
            latitude: 14.6,
            longitude: 120.98,
            accuracyMeters: 5,
          ),
        );
  }
}

class _FakePmRepository
    implements PreventiveMaintenanceRepository, LocationVerificationRepository {
  _FakePmRepository({this.locationOutcomes = const []})
    : schedules = [
        ScheduleOption(
          id: _scheduleId,
          assetId: _assetId,
          scheduleDate: DateTime.utc(2026, 9, 26),
          pmCycle: '2026-09',
          periodType: 'Quarter',
          status: 'Due',
          quarter: 'Q3',
          semester: null,
          year: 2026,
          academicYear: '2026-2027',
          assignedToUserId: _inspectorId,
          asset: const ScheduleAssetOption(
            id: _assetId,
            assetCode: 'FE-001',
            assetCategory: 'fire-extinguisher',
            building: 'Main Building',
            department: 'GSD',
            location: 'Room 101',
          ),
        ),
      ];

  final List<ScheduleOption> schedules;
  final List<LocationVerificationOutcome> locationOutcomes;
  final locationAttempts =
      <
        ({
          String scheduleId,
          double latitude,
          double longitude,
          double accuracyMeters,
        })
      >[];
  AddInspectionInput? addedInput;
  int _nextLocationOutcome = 0;
  PreventiveMaintenanceForm? _createdForm;

  @override
  Future<List<PreventiveMaintenanceForm>> listForms() async =>
      _createdForm == null ? [] : [_createdForm!];

  @override
  Future<PreventiveMaintenanceForm> getForm(String id) async => _createdForm!;

  @override
  Future<PreventiveMaintenanceForm> createForm(
    CreatePreventiveMaintenanceFormInput input,
  ) async {
    _createdForm = PreventiveMaintenanceForm(
      id: _formId,
      fileNumber: null,
      assetCategory: input.assetCategory,
      building: input.building,
      department: input.department,
      periodType: input.periodType,
      quarter: input.quarter,
      semester: input.semester,
      year: input.year,
      academicYear: input.academicYear,
      status: 'Draft',
      createdByUserId: _inspectorId,
      submittedByUserId: null,
      submittedAt: null,
      createdAt: DateTime.utc(2026, 9, 26),
      updatedAt: DateTime.utc(2026, 9, 26),
      inspections: const [],
    );
    return _createdForm!;
  }

  @override
  Future<List<ScheduleOption>> listSchedules({String? assetId}) async =>
      assetId == null
      ? schedules
      : schedules.where((schedule) => schedule.assetId == assetId).toList();

  @override
  Future<LocationVerificationAttempt> createLocationVerificationAttempt(
    String scheduleId, {
    required double latitude,
    required double longitude,
    required double accuracyMeters,
  }) async {
    locationAttempts.add((
      scheduleId: scheduleId,
      latitude: latitude,
      longitude: longitude,
      accuracyMeters: accuracyMeters,
    ));
    final outcome = locationOutcomes[_nextLocationOutcome++];
    return LocationVerificationAttempt(
      id: _attemptId,
      outcome: outcome,
      accuracyMeters: accuracyMeters,
    );
  }

  @override
  Future<PreventiveMaintenanceInspection> addInspection(
    String formId,
    AddInspectionInput input,
  ) async {
    addedInput = input;
    return PreventiveMaintenanceInspection(
      id: _inspectionId,
      scheduleId: input.scheduleId,
      assetId: _assetId,
      inspectorUserId: input.inspectorUserId,
      dateInspected: input.dateInspected,
      isOperational: input.isOperational,
      remarks: input.remarks,
      actionsRecommendations: input.actionsRecommendations,
      createdAt: DateTime.utc(2026, 9, 26),
      updatedAt: DateTime.utc(2026, 9, 26),
    );
  }

  @override
  Future<PreventiveMaintenanceAcknowledgement> acknowledgeForm(
    String formId,
    AcknowledgePreventiveMaintenanceInput input,
  ) => throw UnimplementedError();

  @override
  Future<List<ReferenceOption>> listAssetCategories() async => const [];

  @override
  Future<List<ReferenceOption>> listPeriodTypes() async => const [];

  @override
  Future<List<ReferenceOption>> listQuarters() async => const [];

  @override
  Future<PreventiveMaintenanceForm> submitForm(String formId) =>
      throw UnimplementedError();

  @override
  Future<PreventiveMaintenanceInspection> updateInspection(
    String formId,
    String inspectionId,
    UpdateInspectionInput input,
  ) => throw UnimplementedError();

  @override
  Future<void> deleteInspection(String formId, String inspectionId) async {}
}

Map<String, dynamic> _inspectionJson() => {
  'id': _inspectionId,
  'scheduleId': _scheduleId,
  'assetId': _assetId,
  'inspectorUserId': _inspectorId,
  'dateInspected': '2026-09-26T00:00:00Z',
  'isOperational': true,
  'remarks': null,
  'actionsRecommendations': null,
  'createdAt': '2026-09-26T00:00:00Z',
  'updatedAt': '2026-09-26T00:00:00Z',
};
