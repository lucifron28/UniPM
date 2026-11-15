---
id: TEST-203
type: test-run
title: Android inspection synchronization-conflict evidence
status: executed
recordedAtUtc: 2026-11-15T17:58:19Z
testedCommit: 53afe5708ca640b6fe7618589cbf3be1a1de6ae3
sourceBranch: main
evidenceLevel: locally-executed
---

# Android inspection synchronization-conflict evidence

This record documents an offline inspection update conflict displayed by the
UniPM Android app and the app's discard-local-change recovery. The app, API,
and conflict behavior were not modified to produce the evidence.

## Result

The app queued a local inspection edit while offline. While it remained queued,
an independent authenticated update changed the same server row. After network
reconnection, the app showed **“Update inspection needs review”**, displayed
both the saved local remark and the current server remark, and offered **“Retry
my saved change”** and **“Discard local change.”** This is the expected
conflict-review path for a stale update. The app's own HTTP exchange was not
captured at packet level; the UI state is direct emulator evidence. A separate
control replay of the same stale local payload returned HTTP 409.

Discarding the queued local edit removed the conflict card. The Draft form then
displayed the server-side remark. A SQL check confirmed that the server remark
and update timestamp remained unchanged.

## Source behavior

The source at the tested commit queues offline edits with the server row's
`expectedUpdatedAt`. Synchronization calls the conditional update operation;
HTTP 409 or 412 marks the queued write as a conflict and retains the local
draft. The conflict-review tile exposes retry and discard actions. The API
returns 409 when the server row changed and the requested values differ from
the current row. If the stale request's values already match the row, the API
accepts it idempotently with 200.

The local UI evidence and API control replay are separate observations. No
claim is made that the app's own response body or status code was captured.

The mobile behavior was inspected in
`mobile/lib/features/preventive_maintenance/offline_inspection_repository.dart`
and `mobile/lib/features/preventive_maintenance/preventive_maintenance_page.dart`.

## Environment

- Tested application source commit: `53afe5708ca640b6fe7618589cbf3be1a1de6ae3`
- Tested source branch: `main`; evidence was recorded on
  `feature/manual-sync-conflict-evidence` without application-source changes.
- Backend: local ASP.NET Core Development API on port 5254, connected only to
  disposable local SQL Server databases.
- Database engine: local SQL Server 2019 Developer, version `15.0.2000.5`.
- Used database: `UniPM_ConflictEvidence_20261116_02`, migrated and populated
  with Development users and synthetic demo records using
  `--migrate-database`, `--seed-development-users`, and `--seed-demo`. A
  separate seeded database named `UniPM_ConflictEvidence_20261116_01` was not
  connected to the emulator. `UNIPM_DEV_USER_PASSWORD` was supplied from a
  temporary local file; that file was removed after the run.
- Test account: seeded Development Inspector `inspector@unipm.local`; its
  randomly generated password is not recorded.
- Emulator: Android 17, API 37, x86_64 Google APIs Play Store image; AVD
  `UniPM_ConflictEvidence` using the `pixel_9_pro_xl` device profile. Android
  reports model `sdk_gphone16k_x86_64`.
- APK: debug build configured for the emulator's host API mapping at
  `http://10.0.2.2:5254/`; SHA-256
  `2FC5081BA9138B2D1E8845A54ACF920AE3669FAEFF21218831D0D113A29B1B6B`
  (179,707,321 bytes).
- No production service or institutional record was accessed.

## Synthetic record and reproduction

The app opened Draft form
`1440ADBB-035B-4473-B693-D061233B5A1C`, inspection
`397894E5-0DCC-4AF1-B493-D2DDD48F7497`, for synthetic asset
`CPMP-DEMO-DEMO-CCMS-EL-01` (Emergency Light, CCMS Building, Demo CCMS,
June 2026 PM cycle). Before the edits, the row was operational and had remark
`Fictional routine check: operational.` Its `updatedAt` was
`2026-06-20 10:00:00 +08:00`.

1. Start the local Development API against the migrated and seeded disposable
   database. Build/install the debug APK on the clean test AVD. The build
   command was:
   `flutter build apk --debug --dart-define=UNIPM_API_BASE_URL=http://10.0.2.2:5254/`.
2. Sign in as the seeded Development Inspector and open the existing Draft
   through **Continue PM batch**.
3. Enable airplane mode and disable Wi-Fi. The emulator reported no active
   default network.
4. Change the inspection remark to `Local offline evidence edits were saved.`
   and press **Save row**. The app showed **Offline mode** and reported one
   saved change awaiting synchronization.
5. While the device remained offline, make an independent authenticated API
   update to the same row, using its current expected update timestamp. The
   API returned 200 and stored
   `Server-side concurrent edit for synthetic conflict evidence.` The server
   row's `updatedAt` became `2026-11-16 01:47:09 +08:00`.
6. Re-enable Wi-Fi and reconnect. The app showed **Sync needs review** and
   reported that the server record had changed or was no longer eligible.
7. Open **Review saved drafts** and expand the affected Draft. The conflict
   tile displayed both remarks and the retry/discard actions.
8. Capture the screen directly from the emulator with `adb exec-out screencap
   -p`; do not crop, annotate, recompress, or otherwise alter the PNG.
9. Choose **Discard local change** and confirm. The conflict tile disappeared;
   the Draft displayed the server-side remark. A SQL query confirmed that this
   remark and its update timestamp remained unchanged.

The conflict screenshot was captured at `2026-11-16T01:48:22+08:00`
(`2026-11-15T17:48:22Z`). It includes the warning, local and server remarks,
both recovery actions, and Draft form context. The debug-build ribbon is
visible and has been preserved. The optional recovery screenshot is a separate
direct emulator capture after discarding the local change.

## Expected and observed behavior

| Check | Expected behavior | Observed result |
| --- | --- | --- |
| Offline edit | Save locally and keep the edit pending | The app displayed Offline mode and one saved change |
| Concurrent server edit | Update the same row independently while the device is offline | API returned 200 and stored the synthetic server remark |
| Reconnect with stale local version | Preserve local edit and expose a conflict-review action | The app showed the review warning, both versions, retry, and discard |
| Stale control with values already on server | Treat as idempotently applied | HTTP 200; SQL values stayed unchanged |
| Stale control with the conflicting local remark | Reject without overwriting the server row | HTTP 409; SQL values stayed unchanged |
| Discard local change | Keep the server version and remove the queued local edit | Conflict tile disappeared; Draft and SQL showed the server remark |

The app's displayed conflict message was **“The server record changed or was
no longer eligible. Your saved draft remains on this device.”** The conflict
tile was labeled **“This draft needs sync review”** and **“Update inspection
needs review.”**

The sanitized control log recorded:

```text
2026-11-15T17:52:42.9856783+00:00 stale-version control PUT status=200 expectedUpdatedAt=2026-06-20T10:00:00+08:00
2026-11-15T17:53:51.7285114+00:00 stale local-payload control PUT status=409 expectedUpdatedAt=2026-06-20T10:00:00+08:00
```

These are separate controls after the captured conflict and recovery, not a
client network trace.

## Evidence files and integrity

Hashes were computed with SHA-256 after capture. The PNGs are original,
unmodified `adb exec-out screencap -p` outputs.

| Evidence | File | SHA-256 |
| --- | --- | --- |
| Conflict review | [conflict-android-2026-11-16.png](conflict-android-2026-11-16.png) | `DB1F2CFA2A59AB12B20E26BA4F38A3F410FF846E2650064A8562CCA1635FBDEA` |
| Recovery after discard | [recovery-android-2026-11-16.png](recovery-android-2026-11-16.png) | `96A9B0DB099374389030F0A0927F7790FC4BD220565B4B8A409CDC9C07524216` |

## Limitations and deviations

- The app's own synchronization response was inferred from the visible app
  state and corroborated by a separate stale-payload API control. A client
  network trace was not retained.
- A pre-existing `Pixel_10_Pro` AVD had about 368 MB free and could not install
  the 179,707,321-byte APK. It was stopped without wiping it. The separate
  clean AVD used for the screenshots had sufficient space and installed the
  APK successfully.
- The first disposable database was seeded but never connected to the app; the
  second disposable database was used for the captured run. API startup also
  added six missing current-year synthetic schedules; the selected inspection
  was not changed by that operation.
- TEST-201 and TEST-202 were not present in this checkout's evidence tree or
  reachable Git history, so their historical results were not read or changed.
- This is one synthetic local emulator reproduction. It does not establish
  physical-device, production, institutional-data, or broad concurrency
  acceptance. No application tests were run for this evidence-only change.

## Appendix J text

**Suggested figure caption:** *Figure J.1. UniPM Android inspection
synchronization-conflict review after an offline Draft edit met a concurrent
server update. The screen preserves the saved local remark, shows the current
server remark, and offers retry or discard actions.*

**Suggested accompanying paragraph:** The Inspector saved an inspection
remark while the Android device was offline. Before reconnection, an independent
update changed the same synthetic server record. UniPM retained the local edit
and displayed a review warning with both versions and the available recovery
actions. The separate recovery capture shows the Draft after the Inspector
discarded the local edit; the server-side remark remained in place. This
demonstrates the existing conflict-review workflow in a disposable development
environment, not production or field validation.
