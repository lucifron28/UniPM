---
id: TEST-060
type: test-run
title: Schedule authorization and mobile PM loading field-test preparation
status: executed
recordedAtUtc: 2026-10-06T13:10:02Z
testedCommit: 36650ed39d014a9fa06be36b6de58f4e38a67dc4
sourceBranch: fix/field-testing-unblock
evidenceLevel: locally-executed
---

# Field-testing preparation

## Reproduced failure

The real local web Schedules page showed both the generic registry Retry and
Retry summary while signed in as the fictional `admin@unipm.local` account.
The query inspector identified `GET /api/v1/schedules` with HTTP 403,
`problem: null`, and an empty response body. A separate real JWT request
confirmed that status and body. Anonymous access returned HTTP 401.

The account had only Admin. Backend schedule authorization intentionally admits
GSD, Inspector, and Supervisor, and excludes technical Admin. The web showed
that access denial as a generic retryable loading failure. No database/query
exception caused this reproduced 403. Backend permissions remain unchanged.
The account used during the user's earlier failure has not been confirmed;
this record establishes the reproduced local failure, not every prior failure.

After signing in as GSD, the same endpoint returned HTTP 200 with 43 schedules,
and the browser parsed and displayed them successfully. The SQL database had
the canonical PM cycle, schedule oversight, and inspection-location migrations
applied. No migration, stale-data, client-generation, or response-parsing defect
was demonstrated in this local run.

## Request path inspected

`ScheduleRegistry` calls `useSchedules`, which calls generated `listSchedules`,
then the authenticated HTTP client and `GET /api/v1/schedules`. The query parses
the response with `parseSchedules`. The summary uses the unfiltered query;
asset, status, and quarter filters have separate queries. Backend schedules
apply role authorization and Inspector assignment filtering before querying
SQL Server and projecting schedule responses.

Real GSD requests returned HTTP 200 for:

- `/api/v1/schedules`, including `?status=Due&quarter=Q4&year=2026`.
- `/api/v1/assets`.
- `/api/v1/reference-data/asset-categories`, which supplies `scheduledMonths`.
- `/api/v1/reference-data/schedule-statuses`.
- `/api/v1/reference-data/schedule-quarters`.
- `/api/v1/reference-data/schedule-period-types`.
- `/api/v1/schedules/assignment-options`.
- The newly created schedule detail.

A diagnostic request to `/api/v1/reference-data/schedule-frequencies` returned
404 because that route does not exist. The web uses `asset-categories`; the
diagnostic 404 is not the Schedules failure.

## Changes and source identity

- `5a77d223b29bf34a912d5527aa9dce200e42e531` explains schedule HTTP 403 in the
  registry and summary, preserving Retry and all parsing/validation.
- `bac68e0acfe5d66569aae988f7f31d1683fe171c` catches mobile form and schedule
  loading failures independently and displays each error while keeping the
  successful data available. Terminal HTTP 401 remains handled by the existing
  API/session invalidation path.
- `36650ed39d014a9fa06be36b6de58f4e38a67dc4` makes the new widget tests wait
  for scrolling to settle before tapping workflow actions.

No backend, migration, generated client, RAG, analytics, manuscript, or
unrelated UI source was changed. The NLA V3 experiment branch was neither
modified nor merged. This branch began at current `origin/main`,
`444cd7fcca8a48468638c6d46c817e82583107a1`. Local main was already checked out
in another worktree and was not moved.

## Executed checks

| Check | Source commit | Result |
| --- | --- | --- |
| Focused web schedule workflow Vitest file | `5a77d22` | 14 passed |
| Web `npm run typecheck` | `bac68e0` | Passed |
| Web `npx vite build` | `bac68e0` | Passed |
| Flutter Home, field-worker workflow, foundation tests | `bac68e0` | Initial executed run: 20 passed, 2 failed because test taps missed off-screen buttons |
| Same focused Flutter command after one test correction | `36650ed` | 22 passed |
| `flutter analyze` | `36650ed` | Passed, no issues |
| `flutter build apk --debug` | `36650ed` | Passed; generated `mobile/build/app/outputs/flutter-apk/app-debug.apk` |
| Focused backend schedule/auth/form/location test command | `bac68e0` | Build failed before tests, executable lock; zero tests executed |

The web test run began at `5a77d22` and overlapped the mobile-only commit
`bac68e0`; its web sources did not change. Backend sources stayed identical to
`444cd7f` throughout. Browser/API smoke ran on the backend built from that base.
The Flutter rerun used the corrected committed tests, not an uncommitted patch.

Commands were run sequentially within each component. The Flutter command was:

```powershell
flutter test test/home_page_return_ux_test.dart test/field_worker_workflow_test.dart test/mobile_foundation_test.dart
flutter analyze
flutter build apk --debug
```

The backend command targeted schedule validation and read authorization,
canonical batch assignment, form-row assignment, and location-attempt access
and draft resume through a FullyQualifiedName filter. Its exact filter is
retained in the ignored metadata artifact. The running API smoke process
locked `UniPM.Api.exe`, producing MSB3027/MSB3021 during build. The API process
was stopped. No backend test rerun was attempted after the environment failure.
Earlier sandbox launches that produced no test process/output were NOT EXECUTED.

Raw local artifacts are ignored under `artifacts/field-testing-unblock/`,
`artifacts/field-testing-unblock/backend-focused/`, and
`artifacts/mobile-field-testing-20261006/`. Credentials, tokens, connection
strings, GPS coordinates, and database contents are not copied into this record.

## Real workflow smoke

The GSD browser created schedules for existing fictional assets
`DEMO-LIB-FE-001` and `DEMO-LIB-FE-002`, both Active and in Library, using
CPMP cycle `2026-11`. It opened each schedule and assigned the entire
`LIBRARY + fire-extinguisher + 2026-11` batch to the active fictional Maintenance
Inspector and Maintenance Supervisor. The browser reported two schedules
assigned. A real Inspector GET returned 11 schedules, all assigned to that
Inspector, including these two; the canonical batch had exactly one distinct
Inspector ID and one distinct Supervisor ID.

After creation, the browser summary showed 45 schedules, 8 Due and 37 Completed.
Filtering Due, Q4, 2026 showed exactly the two newly created schedules.
`POST /api/v1/schedules` with October for a fire extinguisher returned HTTP 400:
`Request validation failed`, with `errors.PmCycle` stating that the month must
be one of `02, 05, 08, 11`. Validation was not weakened.

The two new schedules and their assignment remain in the local development
database for the next field test. Existing rows and asset location settings
were not changed. The API smoke process was stopped to release its executable
lock and restarted after checks finished. Start the API again tomorrow if the
current process has ended.

## Not verified

- Backend focused tests did not execute. Inactive-asset, missing-department,
  and invalid-cycle test cases were inspected, but that is not test execution.
- No physical Android GPS/camera/permission or real QR scan was executed.
- The local database had zero assets with all three geolocation fields set.
  Fresh configured GPS verification could not be exercised on that data.
- Mobile configured fresh verification and Draft resume without another capture
  were source-inspected, not proven by the executed Home/foundation tests.
  The backend permits recording attempts during Draft resume; the no-new-attempt
  rule is implemented by the mobile entry flow, not by forbidding that API call.
- No existing backend test rejecting inactive batch assignees was identified.
  The endpoint checks active status and role for each assignee; this assertion
  remains source-inspected in this run.
- Full suites, CI, deployment, institutional acceptance, and the user's prior
  account/environment are not claimed as verified.

## Steps for October 7, 2026

1. Use `fix/field-testing-unblock`. Start the existing native SQL Server, then
   run `./scripts/start-server.ps1` and `./scripts/start-web.ps1` in separate
   PowerShell terminals from the repository root. They use API port 5254 and
   web port 5173. Keep credentials in the ignored local configuration.
2. Sign into web as `gsd@unipm.local`, not the technical Admin account. Open
   `/app/schedules`. Check summary, status, asset, quarter, and year filters.
3. For the prepared batch, use Due, Q4, 2026 and open either
   `DEMO-LIB-FE-001` or `DEMO-LIB-FE-002`. Confirm Library, fire-extinguisher,
   cycle 2026-11, Maintenance Inspector, and Maintenance Supervisor. Both
   schedules are already assigned. Avoid creating duplicates of this batch.
4. For another schedule, choose an Active asset with Department and an allowed
   month. Fire extinguishers and water stations use February, May, August,
   November; fire alarms and emergency lights use June and December. Create
   all needed rows in the same department/category/cycle, then assign the entire
   batch to one active Inspector and one active Supervisor before inspection starts.
5. In web Assets, open the asset that will be inspected. Under Inspection
   location verification, enter the actual field site's Latitude, Longitude,
   and positive Radius in meters, then Save location. All three are required
   together. Do not invent coordinates for a fictional demo location; use an
   asset/reference location appropriate to the real test site. Unconfigured
   verification continues without GPS evidence and will not test this flow.
6. Connect the Android phone by USB with debugging enabled and confirm it is
   listed as `device` by `adb devices`. From the repository root run
   `./scripts/start-mobile.ps1 -DeviceId '<device-id>'`. The script sets up ADB
   reverse for the physical phone and API port 5254. An emulator uses 10.0.2.2.
7. Sign into Android as `inspector@unipm.local`. Under My PM Tasks, open the
   assigned Library/fire-extinguisher/November batch and tap Start inspection.
   Scan that batch asset's QR from its web detail page, or use its manual asset
   code. Confirm the returned asset belongs to the chosen batch.
8. Start a fresh inspection for the configured asset, with phone location
   services on. Grant the location permission when prompted and inspect the
   existing verification result. The location feature is assistive and
   nonblocking, including unavailable or outside-radius results. It does not
   auto-approve the inspection.
9. Save the inspection as a Draft. Return home and continue that Draft, then
   reopen the same existing row. Confirm it resumes the editor without another
   location capture/attempt. For another fresh-attempt check, use the other
   untouched schedule rather than the saved row.
10. For resilience, interrupt only the forms request in a controlled test and
    confirm schedule tasks remain visible with a forms-specific error. Repeat
    with only the schedules request failing and confirm existing forms remain
    resumable. A protected 401 must return to sign-in, not remain a Retry state.

The branch is unmerged. No remote push or PR was requested or performed.
