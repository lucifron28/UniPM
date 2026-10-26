---
id: TEST-165
type: test-run
title: Saved-report integration, SQL concurrency and browser persistence
status: executed
recordedAtUtc: 2026-10-26
testedCommit: d6a7a510cab4e175c0c01d41bf2bd775f7d3ed9d
sourceBranch: feature/saved-reports-stability
evidenceLevel: locally-executed
---

# Saved-report stability verification

Saved reports were selectively ported onto main `3749f2d`, preserving its
interpreter, NLA review screen, authorization and pagination. The old branch
was not merged. The final executable source is `d6a7a51`; later documentation
does not alter it. All dates below are October 26, 2026, in UTC.

## Corrections

- SQL creation locks the existing owner row with `UPDLOCK, HOLDLOCK` within
  the transaction that checks the quota and inserts the definition. Concurrent
  requests from the same owner cannot both take the final available slot.
- DELETE requires the displayed UUID revision. Missing or malformed values
  return 400; stale revisions return 409 without deleting the newer row.
- The runtime initializer and staging preflight require the combined exact
  24-migration schema, including `SavedAnalyticsReports`. The deployment
  script was parsed only, not executed.
- The client sends the required revision, retains a report after a conflict,
  and can reload before trying again. A load failure no longer shows a false
  empty-list message. Generated contracts and negative checks cover DELETE.

Saved definitions contain typed plans and metadata, not result rows or raw
questions. Reopening validates a fresh preview and uses the existing explicit
confirmation/execution path. It does not repeat interpretation.

## Backend and SQL checks

At `c955ac21153de6b343f73091c779a6a9d2f8f77d`, .NET SDK 10.0.300:

```powershell
dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --filter "FullyQualifiedName~PmAnalyticsV3EndpointTests.Saved_reports_are_private_revalidated_and_execute_current_data_without_AI|FullyQualifiedName~SqlServerPmAnalyticsTests.Saved_report_schema_upgrade_quota_lock_and_current_data_reexecution_work_on_sql_server_2019|FullyQualifiedName~StagingDemoInitializationTests" --logger "trx;LogFileName=saved-reports-targeted.trx" --results-directory artifacts/saved-report-fix/backend --nologo --verbosity minimal
```

The actual results-directory argument was the absolute workspace path to the
same ignored directory. Execution ran 08:50:59–08:51:56, exit 0, 11 passed,
zero failed/skipped. The SQL Server 2019 fixture upgraded an owned GUID database
from migration 23 to 24, checked compatibility 150, table/index/foreign key,
and started two creates at 99 reports. One returned 201 and one 409, leaving
exactly 100. Re-executing the saved plan after a synthetic asset insert changed
the result from one to two. Other checks covered ownership, stale deletion,
updates, relative periods and staging initialization.

SQL used loopback Windows-authenticated master only to create/drop isolated
fixtures. No application database or provider was used. Fixtures clean up in
finally blocks; the saved-report fixture asserts its database was dropped.

The run emitted one nullable test warning. Test-only commit `e9ba394` adds an
explicit not-null assertion. Its compile-only command passed 08:53:37–08:53:50
with zero warnings/errors:

```powershell
dotnet build tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --no-restore --nologo --verbosity minimal
```

Tests were not repeated after this assertion-only change. Server implementation
is unchanged through the final source. PowerShell `Parser.ParseFile` reported
zero errors for the staging script; no deployment operation ran.

## Web checks

Node 22.23.4 was used. At `acd1118`, production build/type checking passed and
the focused invocation passed 27/27, zero failures/skips:

```text
npm run build
npm run test:run -- src/features/reports/pm-analytics-v3.test.tsx src/features/reports/saved-reports.test.tsx --maxWorkers=2
```

At `1928d2d`, the following passed three mocked browser cases, zero failures
or skips, with CI mode, initially free port 5197, no server reuse and an unused
API base on port 5199:

```text
npm run e2e -- --workers=2 e2e/pm-analytics-v3.spec.ts
```

Later changes only format test files. At `d6a7a51`, Prettier on all 14 changed
web files, full `npm run lint`, positive contract check, 13 negative contract
fixtures and the generated-client drift check passed. API export comparison
showed three new saved-report paths and two schemas, with no existing path or
schema changes after normalizing property/required-list order.

Earlier build failures came from unsupported Testing Library selector options;
those were removed without weakening the assertions. Ported browser assertions
initially expected summary text hidden by the current redesign. They now check
the visible period rows and resolved plan years. Earlier outputs are retained.

`npm run api:check` failed during Prettier writes on Windows with `UNKNOWN:
unknown error, open` for seven generated models. The API validation and Orval
generation steps succeeded. One later process-only `UV_THREADPOOL_SIZE=1`
diagnostic at `d6a7a51` failed the same way. Only those formatter side effects
were restored to the committed versions; the separate drift check passed.
The wrapper is not reported as a local pass. CI must verify the normal wrapper
on its Linux runner. No dependency or generation script was changed to hide it.

## Real browser, API and SQL Server

At `d6a7a510cab4e175c0c01d41bf2bd775f7d3ed9d`, a local fixture runner
executed the following live spec using the actual API and a new GUID SQL Server
2019 database. The database was migrated and populated only with synthetic
assets and Development users. Credentials existed only in child process
environment and browser memory; tracing and screenshots were disabled.

```text
node node_modules/@playwright/test/cli.js test e2e/saved-reports.live.spec.ts --workers=1 --retries=0 --trace=off --output=../artifacts/saved-report-fix/live/browser-results
```

The overall fixture ran 09:18:24.499–09:18:46.686, exit 0. The browser reported
one passed, zero skipped, in 8.4 seconds. It authenticated a synthetic GSD
account, interpreted an inventory question with the deterministic parser,
saved the reviewed plan, reloaded the page, reopened and confirmed it, added
one synthetic asset through the real API, then reopened and confirmed again.
The result increased by one; interpretation remained at exactly one call.

The API and browser used isolated ports 5200 and 5201. AI and embeddings were
disabled. The owned database was dropped in finally. An independent metadata
query returned zero remaining databases with that exact fixture name, and
neither fixture port had a remaining listener. The harness made no deployment
or institutional-data changes.

## Artifacts and limits

Raw command logs, exact timestamps, TRX, API contract export, fixture lifecycle
and prior failures remain ignored under `artifacts/saved-report-fix/`. Backend
details are in `backend/verification.md`, client details in `client/manifest.md`,
and the real SQL/browser lifecycle in `live/manifest.json`. The fixture script
is local under `artifacts/saved-report-review/run-live-sql.ps1`.

No new full-suite run, provider evaluation, physical-device acceptance,
institutional migration or deployment is claimed. The feature does not change
the manuscript. Remote CI, when available, is separate evidence.
