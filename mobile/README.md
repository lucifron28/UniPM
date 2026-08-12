# UniPM Mobile Foundation

This Flutter application is the Android-first foundation for the skilled-worker
field workflow. It contains authentication, a small authenticated home shell,
and the first Draft preventive-maintenance form workflow. QR scanning and later
field workflow actions remain outside this phase. Offline synchronization is
deferred; its persistence and synchronization architecture remain undecided
until a separate approved decision.

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

HTTP is permitted only in debug builds for local development. The debug
Android manifest enables cleartext traffic for this purpose; release builds
must use HTTPS and do not enable cleartext traffic.

For an Android emulator, `10.0.2.2` is the route to the development machine.
For a physical device, use a reachable LAN address, for example
`http://192.168.1.20:5000/`, or use an approved HTTPS development setup. The
device and development machine must be able to reach the API.

## Authentication Boundary

The client uses the existing `/api/v1/auth/login`, `/api/v1/auth/me`, and
`/api/v1/auth/logout` contracts. Access tokens live only in memory. The mobile
client does not persist, capture, or manually send cookies, and does not
implement refresh-token rotation or startup session restoration. The app starts
signed out after restart and requires a fresh login. A protected 401 clears the
memory-only session and returns the user to sign-in without refresh or replay.

Only `Inspector` and `GSD` users enter the current mobile shell. This is a
navigation boundary; backend authorization remains authoritative.

## Preventive-maintenance Draft Workflow

Inspector and GSD users can open **Preventive-maintenance drafts** from the
authenticated shell. This phase supports creating a one-page form header,
adding multiple inspection rows, resuming a saved Draft, and editing or
deleting Draft rows. Every action is sent to the ASP.NET Core API immediately;
the mobile app does not keep offline drafts or synchronize a local database.

The mobile client only presents Draft forms. Submission, acknowledgement,
signatures, schedule completion, corrective handoff, RMRF, QR scanning, and
offline synchronization remain outside this phase. Offline synchronization is
deferred rather than rejected; its persistence and synchronization architecture
remain undecided pending a separate approved decision.

## Dependencies

- `http`: JSON HTTP requests to the existing ASP.NET Core API.

No API credentials, URLs, tokens, cookies, or environment-specific settings are
committed or persisted by the mobile client.
