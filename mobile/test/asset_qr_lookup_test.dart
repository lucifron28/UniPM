import 'dart:convert';

import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

import 'package:mobile/api/api_client.dart';
import 'package:mobile/api/api_exception.dart';
import 'package:mobile/auth/auth_models.dart';
import 'package:mobile/auth/auth_repository.dart';
import 'package:mobile/auth/session_controller.dart';
import 'package:mobile/features/assets/asset_models.dart';
import 'package:mobile/features/assets/asset_qr_lookup_controller.dart';
import 'package:mobile/features/assets/asset_repository.dart';

const assetId = '11111111-1111-4111-8111-111111111111';
const qrCodeValue = 'UNIPM-FIREALARM-11111111';

Map<String, dynamic> assetJson({Object? status = 'Active'}) =>
    <String, dynamic>{
      'id': assetId,
      'assetCode': 'FA-001',
      'assetCategory': 'fire-alarm',
      'building': 'Main Building',
      'department': 'GSD',
      'location': 'First floor',
      'qrCodeValue': qrCodeValue,
      'status': status,
      'createdAt': '2026-08-01T00:00:00Z',
      'updatedAt': '2026-08-01T00:00:00Z',
    };

ApiAssetRepository repositoryWith(
  Future<http.Response> Function(http.Request request) handler,
) {
  final client = ApiClient(
    baseUrl: Uri.parse('http://localhost:5000/'),
    httpClient: MockClient(handler),
  );
  client.configureSession(
    accessTokenProvider: () => 'inspector-access-token',
    terminalAuthFailureHandler: () async {},
  );
  return ApiAssetRepository(client);
}

class FakeAuthGateway implements AuthGateway {
  @override
  Future<LoginResult> login(String email, String password) async {
    return const LoginResult(
      accessToken: 'inspector-access-token',
      user: AuthUser(
        id: assetId,
        email: 'inspector@example.test',
        displayName: 'Synthetic Inspector',
        roles: ['Inspector'],
      ),
    );
  }

  @override
  Future<AuthUser> currentUser() async {
    return const AuthUser(
      id: assetId,
      email: 'inspector@example.test',
      displayName: 'Synthetic Inspector',
      roles: ['Inspector'],
    );
  }

  @override
  Future<void> logout() async {}
}

void main() {
  test('valid-looking QR sends the complete value to the authenticated API', (
    ) async {
    http.Request? capturedRequest;
    final repository = repositoryWith((request) async {
      capturedRequest = request;
      return http.Response(jsonEncode(assetJson()), 200);
    });

    await repository.getByQr('  unipm-firealarm-11111111  ');

    expect(
      capturedRequest?.url.path,
      '/api/v1/assets/by-qr/unipm-firealarm-11111111',
    );
    expect(
      capturedRequest?.headers['authorization'],
      'Bearer inspector-access-token',
    );
  });

  test('asset response parses all lookup fields', () {
    final asset = Asset.fromJson(assetJson());

    expect(asset.id, assetId);
    expect(asset.assetCode, 'FA-001');
    expect(asset.assetCategory, 'fire-alarm');
    expect(asset.building, 'Main Building');
    expect(asset.department, 'GSD');
    expect(asset.location, 'First floor');
    expect(asset.qrCodeValue, qrCodeValue);
    expect(asset.status, 'Active');
  });

  test('empty and obvious non-UniPM values are rejected before a request', (
    ) async {
    var requestCount = 0;
    final repository = repositoryWith((request) async {
      requestCount++;
      return http.Response(jsonEncode(assetJson()), 200);
    });

    await expectLater(
      repository.getByQr('   '),
      throwsA(isA<InvalidUniPmQrException>()),
    );
    await expectLater(
      repository.getByQr('https://example.test/asset/1'),
      throwsA(isA<InvalidUniPmQrException>()),
    );

    expect(requestCount, 0);
  });

  test('backend 404 is distinguished as an unknown asset', () async {
    final repository = repositoryWith(
      (request) async => http.Response('', 404),
    );
    final controller = AssetQrLookupController(repository);

    await controller.lookup(qrCodeValue);

    expect(controller.status, AssetQrLookupStatus.notFound);
    expect(controller.asset, isNull);
    expect(controller.errorMessage, 'No asset matches this UniPM QR code.');
    controller.dispose();
  });

  test('malformed asset response is reported separately', () async {
    final malformed = assetJson(status: null);
    final repository = repositoryWith(
      (request) async => http.Response(jsonEncode(malformed), 200),
    );
    final controller = AssetQrLookupController(repository);

    await controller.lookup(qrCodeValue);

    expect(controller.status, AssetQrLookupStatus.failure);
    expect(
      controller.errorMessage,
      'The server returned an invalid asset response.',
    );
    controller.dispose();
  });

  test('API failure remains distinct from invalid and unknown QR states', (
    ) async {
    final repository = repositoryWith(
      (request) async => http.Response('', 500),
    );
    final controller = AssetQrLookupController(repository);

    await controller.lookup(qrCodeValue);

    expect(controller.status, AssetQrLookupStatus.failure);
    expect(
      controller.errorMessage,
      'The mobile service is unavailable. Please try again.',
    );
    controller.dispose();
  });

  test('asset lookup 401 uses the existing terminal session boundary', (
    ) async {
    http.Request? capturedRequest;
    final apiClient = ApiClient(
      baseUrl: Uri.parse('http://localhost:5000/'),
      httpClient: MockClient((request) async {
        capturedRequest = request;
        return http.Response('', 401);
      }),
    );
    final sessionController = SessionController(FakeAuthGateway());
    apiClient.configureSession(
      accessTokenProvider: () => sessionController.accessToken,
      terminalAuthFailureHandler:
          sessionController.handleTerminalAuthenticationFailure,
    );
    await sessionController.login(
      'inspector@example.test',
      'fictional-password',
    );
    final repository = ApiAssetRepository(apiClient);

    await expectLater(
      repository.getByQr(qrCodeValue),
      throwsA(
        isA<ApiException>().having(
          (error) => error.statusCode,
          'status code',
          401,
        ),
      ),
    );

    expect(capturedRequest?.headers['authorization'], 'Bearer inspector-access-token');
    expect(sessionController.status, SessionStatus.signedOut);
    expect(sessionController.accessToken, isNull);
    expect(
      sessionController.errorMessage,
      'Your session expired. Please sign in again.',
    );
    sessionController.dispose();
  });
}
