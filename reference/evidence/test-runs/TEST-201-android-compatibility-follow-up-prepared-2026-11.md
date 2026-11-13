---
id: TEST-201
type: test-run
title: Android compatibility follow-up prepared, execution pending
status: pending
recordedAtUtc: 2026-11-13T15:40:42Z
reviewedAtUtc: 2026-11-13T16:28:31Z
sourceBaseCommit: 22ec25cb37c19014779ff2968f4d2b4d7bb87773
sourceBranch: codex/nonfunctional-testing-2026-11
evidenceLevel: source-inspected
---

# TEST-201: Android compatibility follow-up prepared, execution pending

## Status

This is a source-review record, not an executed test run. The TEST-201 changes
are local and uncommitted on the Android testing branch. No local device test,
build, commit, push, or GitHub Actions run has been started for them, and no
result is counted as a pass. The repository execution limit was reported as
reached; its expiration has not been verified, so execution remains blocked.

Latest executed evidence is [TEST-200](TEST-200-nonfunctional-android-smoke-rerun-2026-11.md),
run [43444880972](https://github.com/lucifron28/UniPM/actions/runs/43444880972),
tested commit `523cce1898956434c70d50ebb6b1142be459c7a8`.

## TEST-201 execution outcomes

No TEST-201 commit SHA or workflow run ID/URL exists. Every row below is
NOT EXECUTED for this follow-up; the TEST-200 results above remain the latest
verified outcomes.

| Check | API 29 | API 34 |
|---|---|---|
| Build, APK install, startup, sign-in UI, configuration tests | NOT EXECUTED | NOT EXECUTED |
| Synthetic login, authenticated navigation, dashboard, search, asset details | NOT EXECUTED | NOT EXECUTED |
| Seeded maintenance history and visible history record | NOT EXECUTED | NOT EXECUTED |
| PM schedule display and online Start/save/reopen/edit/backend persistence | NOT EXECUTED | NOT EXECUTED |
| Backend disconnection and offline Draft/inspection local save | NOT EXECUTED | NOT EXECUTED |
| Force-stop/cold-start and offline queue persistence across restart | NOT EXECUTED | NOT EXECUTED |
| Reconnection, synchronization, expected server state, and form reopen | NOT EXECUTED | NOT EXECUTED |
| Controlled synchronization conflict | NOT EXECUTED | NOT EXECUTED |
| Commit, push, and GitHub Actions execution | BLOCKED — execution-limit expiration unverified | BLOCKED — execution-limit expiration unverified |

## TEST-200 outcomes carried forward

| Check | API 29 | API 34 |
|---|---|---|
| Install, visible login, configuration checks, authenticated navigation, dashboard, asset search/details | PASSED | PASSED |
| History | FAILED at test-side `ensureVisible(view-asset-history)` because the lazy details list had not built the History action. No tap or history API request occurred. | PASSED; the history page displayed a seeded record and the API returned 200. |
| PM schedule | NOT EXECUTED after the History selector failure. | PASSED. |
| Start/Resume and form save/reopen | NOT EXECUTED. | BLOCKED by using DEMO-EL-001, whose schedule is Completed and has no Draft row; no Start/Resume action was expected from this fixture. |
| Offline draft persistence and reconnection sync | NOT EXECUTED. | NOT EXECUTED. |

The earlier asset-details timeout from run 43396819069 did not recur in TEST-200,
but TEST-200 does not identify the cause of that earlier timeout. No production
defect was confirmed, so production code is unchanged.

## Prepared changes

- The History step scrolls the active vertical details list until its lazily
  built action is visible. It checks the history endpoint's HTTP 200 response,
  the exact seeded inspection ID and remarks, and then verifies that the same
  record appears on the navigated history screen. Its back-navigation checks
  return through asset details, search, and the dashboard explicitly.
- Start/Resume checks use the separate seeded synthetic asset
  `DEMO-EL-003` (`30000000-0000-4000-8000-000000000003`) and schedule
  `31000000-0000-4000-8000-000000000003`. The test checks the schedule is Due,
  eligible, assigned to the synthetic inspector, and has no existing Draft in
  its PM group before opening the form. The online workflow creates and saves
  an inspection, reopens and edits it, verifies the updated backend form, and
  reopens the edited row.
- Offline work uses the separate synthetic asset `DEMO-FE-003`
  (`10000000-0000-4000-8000-000000000003`) and its eligible November schedule
  (`11000000-0000-4000-8000-000000000003`). The runner disables emulator
  connectivity only after the UI has loaded the fixture. A direct emulator
  request to `/health/ready` must fail before the app creates a local Draft and
  inspection row. The test checks the visible local remarks and two pending
  writes. The runner then force-stops the app, confirms its process is absent,
  and starts a second integration-test phase while the emulator remains
  offline. That phase confirms the cold-start login screen and backend
  unreachability, then requests reconnection. After HTTP 200 and same-account
  sign-in, the test verifies the exact Draft, inspection remarks, asset,
  schedule ID, inspector, Completed server schedule, cleared sync banner, and
  reopened form value. Authentication is memory-only, so the offline queue is
  checked in the app before process restart and its persistence is demonstrated
  by the verified post-restart server state after reconnect/sign-in; there is
  no authenticated local-draft screen check between cold start and reconnect.
- The API 29 and API 34 matrix rows run in separate GitHub-hosted jobs, each
  with a fresh ephemeral database and independently seeded synthetic fixtures.
- Any uncaught integration check failure emits its current stage and dumps the
  Flutter widget and render trees before rethrowing. The runner listens for the
  marker and captures an in-app screenshot plus Android window hierarchy while
  the failure screen is open. It retains integration/API request logs,
  logcat, connectivity snapshots, and network-toggle command outcomes.
No earlier test-run record or artifact was modified, replaced, or deleted.
TEST-201 is an additional pending record; TEST-198, TEST-199, TEST-200 and
their associated run artifacts remain preserved.

## Pending execution and reporting

When execution is permitted, first commit and push the reviewed changes to the
testing branch, then run the existing matrix on API 29 and API 34. For each
profile, record install/login/dashboard/search/details, History navigation and
API response, PM schedule selection, Start/Resume, online save/reopen/edit, the
offline backend probe, local queue state, force-stop/cold-start evidence, and
reconnection/server state separately. Report failure screenshots, widget
diagnostics, API statuses, and emulator logs with the exact tested SHA.

Compatibility remains incomplete until all required workflows pass on both
profiles. The controlled live conflict scenario was not prepared or executed;
report it separately as NOT EXECUTED unless separately authorized and tested.
