---
id: TEST-156
type: test-run
title: Completion clocks and offline recovery verification
status: executed
recordedAtUtc: 2026-10-25
testedCommit: ca2b312a370b93340ebc0321e11dbd03bd4de805
additionalTestedCommits:
  - 100007f2bfb4eddf37298ac62a72ef334d659ed2
  - 0087179d1741ca28fa563f1b3773783ce4840980
sourceBranch: feature/capstone-remediation
evidenceLevel: locally-executed
---

# Completion clocks and offline recovery

## Policy and implementation

The existing compliance policy remains unchanged. The first server-accepted
save sets `InspectionRecord.CompletedAt`. An entered inspection date does not
backdate that value, and retries retain the first recorded value.

| Time | Meaning and source |
|---|---|
| Physical field-work completion | Not independently verified by these timestamps. |
| Entered inspection date | Supplied by the operator; retained as entered data. |
| Queued local completion representation | Device-recorded time for a row created offline; provisional until synchronization. |
| Persistent completion | Server-recorded first accepted save; the compliance clock. |
| Submission | Separate server-side whole-form transition. |
| Acknowledgement | Separate server-side receipt event and capture-operator record. |

Cycle deadlines remain the final instant of the PM month in Asia/Manila.
Offline work entered before the deadline but accepted afterwards can therefore
count as late. Another policy requires institutional review; no trusted
device-time field, schema change, inferred legacy timestamp or new compliance
formula was introduced.

Backend commits `7293a8a` and `ca2b312` add API/idempotency and fixed-time
analytics assertions and clarify catalog metadata. Client commit `6431418`
labels entered dates, device-saved pending rows and server-recorded completion
separately. Server-time displays use Manila time. Submission and
acknowledgement remain separate events. Workflow documentation commit
`2f2e4b0` removes the unsupported claim that the stored clock independently
establishes actual field-work time.

## Backend and native SQL execution

At `ca2b312a370b93340ebc0321e11dbd03bd4de805`:

```powershell
dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --filter "FullyQualifiedName~PreventiveMaintenanceFormDraftEndpointsTests|FullyQualifiedName~PmPeriodDashboardTests|FullyQualifiedName~PmAnalyticsV3EndpointTests" --logger "trx;LogFileName=phase3-backend.trx" --results-directory artifacts/remediation/backend
```

Exit 0: 63 passed, 0 failed, 0 skipped, 19 seconds. The fixed-time case
distinguishes an entered pre-deadline date with a post-deadline server save
from a completion recorded before the deadline.

```powershell
dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --filter "FullyQualifiedName~SqlServerPmAnalyticsTests.Gsd_analytics_uses_sql_server_2019_metrics_and_enforces_access" --logger "trx;LogFileName=phase3-native-sql.trx" --results-directory artifacts/remediation/backend
```

Exit 0: 1 passed, 0 failed, 0 skipped, 14 seconds. Both SQL test variables were
process-scoped to local Windows authentication and the master catalog. No
application connection setting was supplied. The reviewed fixture created
and used a new GUID-named synthetic database, asserted SQL Server major
version 15, Full-Text availability and compatibility level 150, then confirmed
that its database was dropped. No application database was selected.
This single legacy analytics check does not establish all V3 SQL paths.

## Mobile execution

Commands ran from `mobile`, separately, with concurrency limited to two:

| Command | Source | Result |
|---|---|---|
| `flutter test test/offline_inspection_repository_test.dart --concurrency=2` | `100007f2` | Exit 0; 20 passed |
| `flutter test test/batch_progress_calculation_test.dart --concurrency=2 --plain-name 'labels an inspection waiting for sync as device-saved'` | `100007f2` | Exit 0; 1 passed |
| `flutter test test/preventive_maintenance_form_acknowledgement_test.dart --concurrency=2` | `0087179` | Exit 0; 11 passed |
| `flutter test test/preventive_maintenance_form_draft_test.dart --concurrency=2 --plain-name 'resuming a draft renders every existing row'` | `0087179` | Exit 0; 1 passed |
| `flutter analyze` | `0087179` | Exit 0; no issues found |

The repository checks include restart simulation, retry/idempotency, conflict
review and account-isolated persistence. New 403 and 409 cases use one valid
form/schedule per case. Each keeps the rejected write queued, preserves the
local row, exposes conflict guidance and inserts no remote row. A separate
assertion checks replacement of provisional device time by the API's value
after synchronization; the write payload contains no authoritative completion
time supplied by the device.

## Web execution

At `0087179`, Node 22.23.4:

- `npm run test:run -- src/features/inspections/inspection-workflow.test.tsx --maxWorkers=2`: exit 0, 7 passed.
- `npm run test:run -- src/features/preventive-maintenance-forms/preventive-maintenance-form-review.test.tsx --maxWorkers=2`: exit 0, 21 passed.
- `npm run typecheck`: exit 0.
- ESLint and Prettier checks on the five changed inspection/form files: exit 0.

The exact lint/format argument lists are retained in the ignored supplementary
`artifacts/remediation/clients/lint-format-command-manifest.md` from the actual
executor commands. They name the inspection detail/test and form detail,
presentation and review-test files. No rerun was invented to recover metadata.

## Failed test-driver attempts and corrections

The initial new widget recovery test stalled under fake async timers. Replacing
an unadvanced zero-duration timer and waiting for animations did not produce
a reliable combined recovery/UI test. The recovery assertions were retained
in two plain async repository cases; presentation stayed in the separate
widget tests. Interrupted attempts have no passing test count.

The existing discard-confirmation test needed an explicit logical viewport
after the added copy changed layout. Its tap, confirmation and persisted-row
assertions remain. An acknowledgement expectation was updated to include the
explicit Manila zone. Test-driver commits are `d9233894`, `a6cf6e5`,
`3863851d`, `47d6224e`, `6fe7b02`, `100007f2` and `0087179d`.
These corrections do not establish physical-device acceptance.

## Provenance correction and limits

TEST-155 incorrectly said Phase 2's raw lint log retained its exact file
arguments. That log retains only `EXIT=0`; its command, ordered arguments,
runtime and SHA were not retained there. Treat that lint result as the
executor-reported result, not a complete retained command identity. The
supplement above records the gap rather than reconstructing an execution.
The backend TRX and web test/typecheck results in TEST-155 are unaffected.

Raw Phase 3 logs/TRX remain ignored under `artifacts/remediation/backend/`
and `artifacts/remediation/clients/phase3*`. The main chat reviewed the actual
source and test diffs. No real-provider request, institutional database use,
OS-level device restart, signed distribution, deployment or GSD acceptance
was performed by this record.
