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

To run against the deployed Azure backend, from the repository root:

```powershell
.\scripts\start-mobile-deployed.ps1
# Optional, when selecting a specific connected Android device:
.\scripts\start-mobile-deployed.ps1 -DeviceId emulator-5554
```

This uses the existing Android launcher with the deployed HTTPS URL. It does
not start a local server or set up ADB port forwarding. Flutter and ADB must be
on PATH, with an Android phone or emulator connected.

To configure a local backend URL directly:

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

For a release build, provide an approved HTTPS API URL:

```powershell
flutter build apk --release --dart-define=UNIPM_API_BASE_URL=https://<approved-api-host>/
```

Production signing is supplied outside the repository through the standard
untracked `android/key.properties` file and its referenced keystore. Release
configuration never falls back to the debug keystore. A release artifact built
without project-owned signing material is not a distributable signed package.

The Android package ID is `io.github.lucifron28.unipm`. It installs separately
from the previous `com.example.mobile` app, and encrypted offline work is not
migrated automatically. Before moving to the new app, sign in to the existing
app and sync or recover its pending work while the old app and array-form API
response are still supported. Keep the old app installed until that work is
confirmed; do not uninstall it as part of the upgrade.

## Authentication Boundary

The client uses the existing `/api/v1/auth/login`, `/api/v1/auth/me`, and
`/api/v1/auth/logout` contracts. Access tokens live only in memory. The mobile
client does not persist, capture, or manually send cookies, and does not
implement refresh-token rotation or startup session restoration. The app starts
signed out after restart and requires a fresh login. A protected 401 clears the
memory-only session and returns the user to sign-in without refresh or replay.

Only `Inspector` and `GSD` users enter the current mobile shell. This is a
navigation boundary; backend authorization remains authoritative.

The integrated mobile home screen places pending acknowledgements near the top,
keeps asset scanning prominent, and prioritizes urgent PM tasks. Its
acknowledgement screen uses a compact summary with expandable inspection details
while keeping non-operational findings and signatory/signature controls
accessible. Existing review, confirmation, authorization, and acknowledgement
behavior remains intact. GSD and physical-device acceptance remain pending.

## Preventive-maintenance Draft Workflow

Inspector and GSD users can open **Preventive-maintenance drafts** from the
authenticated shell. This phase supports creating a one-page form header,
adding multiple inspection rows, resuming a saved Draft, and editing or
deleting Draft rows. A worker can review the rows and submit the whole form;
submission assigns the provisional file number returned by the API and makes
the form read-only. Online edits are sent to the ASP.NET Core API immediately.
For offline work, the mobile client uses cached assigned schedules, assets, and
reference data, and stores Draft form and inspection-row changes in encrypted
per-user secure storage. Changes sync automatically after connectivity returns
and can be retried from the home screen. Stable client IDs make create retries
idempotent; stale row updates remain queued as conflicts for review rather than
overwriting newer server data.

The mobile authentication session remains memory-only, so the same account
must sign in again after an app restart before queued work can sync. Location
verification, photo evidence, form submission, and acknowledgement remain
online-only. A queued or conflicted form cannot be submitted. Sign-out locks the
in-memory cache and preserves encrypted queued work for that account.

The **Scan asset QR** entry sends the complete scanned UniPM QR value to the
authenticated backend asset lookup. Backend asset data remains authoritative;
the mobile app does not derive an asset identity or category from the QR text.
The lookup identifies an asset record; it does not prove physical presence.
The app does not capture device coordinates, and any location data supplied by
a client is advisory only.
Eligible schedules are requested for the returned asset ID. The worker chooses
when more than one applicable schedule exists, then starts a derived Draft,
reuses a compatible Draft, chooses between multiple compatible Drafts, or
resumes an existing inspection row. An inspection row is created only when the
worker saves it in the existing editor.

`PmCycle` identifies the institutional scheduled month and year as `YYYY-MM`.
Mobile schedule displays should show that cycle separately and derive its due
date from the cycle's civil month-end in fixed Asia/Manila `+08:00` time, not
from `ScheduleDate`. New schedule records store that month-end deadline in
`ScheduleDate`; historical values remain unchanged and may not match it. The
due date is not a planned visit date and must not shift with the device's time
zone. The actual inspection date and `InspectionRecord.CompletedAt` come from
field work.
Mobile batch cards show **Progress**, inspected schedules divided by
scheduled assets. **Compliance rate** remains a GSD reporting measure,
completed inspections on or before the deadline divided by eligible
non-cancelled scheduled assets; it does not belong in the Inspector mobile UI.

From the scanned asset details, the worker can open read-only **Maintenance
history**. The client requests `/api/v1/inspections/history/{assetId}` using
the exact backend asset ID and displays only the records returned by that
acknowledged-history contract, including inspection date, condition, the
inspection-row reference, remarks, recommendations, and the confirmed Water
Drinking Station work items when present. Draft and Submitted rows are
excluded by the backend and are not reconstructed or classified by the mobile
client.

The current PM editor follows the four confirmed visible GSD forms: Fire
Extinguisher Monitoring (Rev. 2, November 2023), Fire Alarm Preventive
Maintenance (Rev. 1, May 2022), Emergency Lights Preventive Maintenance (Rev.
1, May 2022), and Water Drinking Station Preventive Maintenance (Rev. 1,
November 2023). Asset/device number, location, building, department, and
category are taken from backend asset/schedule data. The worker records
operational status, inspection date, remarks, recommendations, the optional
Water Station accomplishment date, and the Water Station filter/UV work items.
Type, capacity, installation date, expiration
date, and other metadata are shown only when supplied by the asset contract;
the mobile form does not ask the worker to re-enter unavailable asset data.
The Water Station RMRF number is outside this PM inspection boundary.

For the confirmed GSD workflow, a submitted form can be opened for
acknowledgement in the authenticated skilled-worker mobile session. The worker
reviews the whole form, records the concerned Department Head's name and
position, and captures the signature; the Department Head does not need a
UniPM account. The client calls
`POST /api/v1/preventive-maintenance-forms/{id}/acknowledge` with the signatory
data and a PNG signature. The backend remains authoritative: it changes the
form to **Acknowledged** and records receipt/noting. It does not alter
execution or compliance timestamps or complete linked schedules. Field-work
completion, recorded in `InspectionRecord.CompletedAt`, completes schedules
before submission. The mobile client never marks schedules complete directly.
After success, the form is read-only and its completed rows are eligible for
acknowledged-only official history. Acknowledgement is not corrective-action,
budget, RMRF, or WMS approval.

The four supplied GSD forms are implemented as the authoritative visible
category-form structure for the current mobile PM pass. Historical form-version
preservation, corrective handoff, and RMRF processing remain outside this mobile
implementation. Offline persistence, queued synchronization, explicit conflict
recovery, and the acknowledgement/home UX are integrated into `main` and
`deploy/azure-staging`; GSD and physical-device acceptance remain pending.

## Integration Verification

The mobile integration passed Flutter CI on the checked `main` head `5a92f9b`
([run](https://github.com/lucifron28/UniPM/actions/runs/39408165986)). The
branch consolidation report also records successful exact-head checks for the
maintained branches. These are automated engineering checks, not field
acceptance.

Physical-device behavior and GSD field acceptance remain unverified.

## Dependencies

- `http`: JSON HTTP requests to the existing ASP.NET Core API.
- `mobile_scanner`: Android camera preview and QR decoding. Android camera
  permission is declared in the application manifest.

No API credentials, URLs, tokens, cookies, or environment-specific settings are
committed or persisted by the mobile client.
