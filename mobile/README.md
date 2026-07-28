# UniPM Mobile Foundation

This Flutter application is the Android-first foundation for the skilled-worker
field workflow. This phase contains authentication and a small authenticated
home shell only. Preventive-maintenance forms, inspection entry, QR scanning,
offline sync, and field workflow actions are intentionally deferred.

## Local Setup

From this directory:

```powershell
flutter pub get
flutter analyze
flutter test test/mobile_foundation_test.dart
```

Configure the backend URL at runtime; it is not committed to the repository:

```powershell
flutter run --dart-define=UNIPM_API_BASE_URL=http://10.0.2.2:5000/
```

For an Android emulator, `10.0.2.2` points to the development machine. For a
physical device, use the development machine's reachable LAN address, for
example `http://192.168.1.20:5000/`, and ensure the device can reach the API.
Use HTTPS and an approved development certificate configuration when required
by the device.

## Authentication Boundary

The client uses the existing `/api/v1/auth/login`, `/api/v1/auth/me`,
`/api/v1/auth/refresh`, and `/api/v1/auth/logout` contracts. Access tokens live
only in memory. The refresh-cookie value is stored through platform secure
storage so the client can attempt one session restoration after restart. The
HTTP client allows one refresh and one replay for an ordinary 401, then clears
local session material on terminal failure.

Only `Inspector` and `GSD` users enter the current mobile shell. This is a
navigation boundary; backend authorization remains authoritative.

## Dependencies

- `http`: JSON HTTP requests to the existing ASP.NET Core API.
- `flutter_secure_storage`: platform-secure storage for refresh-cookie session
  material only. Access tokens are never persisted.

No API credentials, URLs, tokens, cookies, or environment-specific settings are
committed.
