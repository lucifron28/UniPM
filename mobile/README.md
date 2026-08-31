# UniPM Mobile

This Flutter application provides the Android-first skilled-worker field
workflow. Authenticated Inspector and GSD users can identify an existing asset
from its UniPM QR code, review backend-authoritative asset details, resolve an
applicable asset schedule, and start or resume the existing Draft
preventive-maintenance workflow.

## Local Setup

From this directory:

```powershell
flutter pub get
flutter analyze --no-pub
flutter test --no-pub
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

The **Scan asset QR** entry sends the complete scanned UniPM QR value to the
authenticated backend asset lookup. Backend asset data remains authoritative;
the mobile app does not derive an asset identity or category from the QR text.
Eligible schedules are requested for the returned asset ID. The worker chooses
when more than one applicable schedule exists, then starts a derived Draft,
reuses a compatible Draft, chooses between multiple compatible Drafts, or
resumes an existing inspection row. An inspection row is created only when the
worker saves it in the existing editor.

Submission, acknowledgement, signatures, final category-specific forms,
schedule completion, corrective handoff, RMRF processing, and offline workflow
remain outside this implementation. Offline persistence and synchronization
architecture remain undecided pending a separate approved decision.

## Dependencies

- `http`: JSON HTTP requests to the existing ASP.NET Core API.
- `mobile_scanner`: Android camera preview and QR decoding. Android camera
  permission is declared in the application manifest.

No API credentials, URLs, tokens, cookies, or environment-specific settings are
committed or persisted by the mobile client.
