# UniPM Mobile

This Flutter application provides the Android-first skilled-worker field
workflow. Authenticated Inspector and GSD users can identify an existing asset
from its UniPM QR code, review backend-authoritative asset details, resolve an
applicable asset schedule, and start, review, or submit the existing
preventive-maintenance Draft workflow.

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
deleting Draft rows. A worker can review the rows and submit the whole form;
submission assigns the provisional file number returned by the API and makes
the form read-only. Every action is sent to the ASP.NET Core API immediately;
the mobile app does not keep offline drafts or synchronize a local database.

The **Scan asset QR** entry sends the complete scanned UniPM QR value to the
authenticated backend asset lookup. Backend asset data remains authoritative;
the mobile app does not derive an asset identity or category from the QR text.
Eligible schedules are requested for the returned asset ID. The worker chooses
when more than one applicable schedule exists, then starts a derived Draft,
reuses a compatible Draft, chooses between multiple compatible Drafts, or
resumes an existing inspection row. An inspection row is created only when the
worker saves it in the existing editor.

From the scanned asset details, the worker can open read-only **Maintenance
history**. The client requests `/api/v1/inspections/history/{assetId}` using
the exact backend asset ID and displays only the records returned by that
acknowledged-history contract, including inspection date, condition, the
inspection-row reference, remarks, and recommendations. Draft and Submitted
rows are excluded by the backend and are not reconstructed or classified by
the mobile client. The current history contract does not expose final
category-specific Page 2 fields or a form file number; those remain pending
GSD validation.

For the confirmed GSD workflow, a submitted form can be opened for
acknowledgement in the authenticated skilled-worker mobile session. The worker
reviews the whole form, records the concerned Department Head's name and
position, and captures the signature; the Department Head does not need a
UniPM account. The client calls
`POST /api/v1/preventive-maintenance-forms/{id}/acknowledge` with the signatory
data and a PNG signature. The backend remains authoritative: it changes the
form to **Acknowledged** and completes linked schedules. The mobile client
never marks schedules complete directly. After success, the form is
read-only. Acknowledgement is not corrective-action, budget, RMRF, or WMS
approval.

Final category-specific forms, corrective handoff, RMRF processing, and
offline workflow remain outside this mobile implementation. Offline
persistence and synchronization architecture remain undecided pending a
separate approved decision.

## Dependencies

- `http`: JSON HTTP requests to the existing ASP.NET Core API.
- `mobile_scanner`: Android camera preview and QR decoding. Android camera
  permission is declared in the application manifest.

No API credentials, URLs, tokens, cookies, or environment-specific settings are
committed or persisted by the mobile client.
