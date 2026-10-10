---
id: TEST-069
type: test-run
title: Schedule recovery across application restart verification
status: executed
recordedAtUtc: 2026-10-10T06:52:06Z
testedCommit: 09d1cfa10377df788783089235bad7d52b14a049
sourceBranch: feat/pre-evaluation-ux-motion
evidenceLevel: locally-executed
---

# Schedule recovery across application restart verification

## Objective

Close the application-restart recovery gap in PR #88 and rerun the full
backend Release suite after making an existing SQL concurrency test setup
deterministic. The two commits covered by the final run change test code only.

## Execution identity

The exact source and test commit was
09d1cfa10377df788783089235bad7d52b14a049 on
`feat/pre-evaluation-ux-motion`. The final Release build and complete backend
suite ran on this commit.

Environment: Windows, .NET SDK 10.0.300, native SQL Server 2019
15.0.2000.5 with Full-Text Search installed. SQL Server integration tests used
disposable databases created by the test fixture. The restart test explicitly
sets its disposable database to compatibility level 150. Connections used
process-scoped environment variables and integrated authentication; no
connection strings or credentials are recorded. No AI provider was called.

## Commands and results

| Command | Result |
| --- | --- |
| `dotnet build UniPM.slnx --configuration Release --no-restore -m:1 -p:BuildInParallel=false` | Passed, 0 errors. One existing CS8602 warning remains at `server/Features/PreventiveMaintenanceForms/PreventiveMaintenanceFormEndpoints.cs:540`. |
| `dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --configuration Release --no-build --no-restore -m:1 -p:BuildInParallel=false --logger "console;verbosity=minimal"` | 386 passed, 0 failed, 1 optional external-provider smoke test skipped, 387 total. |
| SQL Server-tagged tests in the full-suite run | 39 test methods carry `[SqlServerFact]` or `[SqlServer2019Fact]`; all passed as part of the full suite. |
| Focused SQL Server lock and restart tests | 2 passed, 0 failed, 0 skipped against native SQL Server 2019. |
| `dotnet ef migrations has-pending-model-changes --project server/server.csproj --startup-project server/server.csproj --configuration Release --no-build` | Passed; no pending model changes. A process-only design-time connection setting allowed model construction. EF did not connect to or modify a database. |
| `dotnet format tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj whitespace --verify-no-changes --no-restore --include Inspections/SqlServerInspectionSubmissionIntegrityTests.cs --verbosity quiet` | Passed. |
| `git diff --check` | Passed for the source and evidence changes before commit. |

Raw logs are retained under ignored `artifacts/test-results/` files. The final
Release build log is `pre-eval-release-build-final.log`; the final complete
suite log is `pre-eval-backend-release-final.log`.

## Application restart recovery

`Application_restart_rechecks_schedule_recovery_without_duplicate_schedules`
uses a disposable SQL Server 2019 database and starts the API through a
`WebApplicationFactory` host with the schedule recovery worker enabled. The
first host returned HTTP 200 for liveness and readiness and generated an
eligible schedule for a synthetic active fire-alarm asset. The test disposed
that host, removed one generated schedule, then started a second API host
against the same database. The second host also returned HTTP 200 for
liveness and readiness. Recovery recreated the removed cycle; the test verified
there was exactly one schedule for it, the total schedule count was unchanged,
and IDs for the other cycles were preserved.

This proves recovery across two API host lifecycles in-process. It does not
exercise a separately launched Kestrel process or IIS service restart. An
ad-hoc Windows PowerShell process smoke attempt could not construct
`Microsoft.Data.SqlClient` because that provider reported it was unsupported
on the PowerShell platform; the attempt failed before creating a database.
The SQL-backed `WebApplicationFactory` test is the executed restart evidence.

## Concurrency test synchronization correction

The first full-suite run at `4d801ee3bdb471b22be439ef9970cebf5b084f3a`
reported one failure in the pre-existing
`Registration_and_recovery_wait_for_assignment_then_defer_locked_cycle`
test: the observer expected two requests waiting on an application lock, but
registration had already returned HTTP 409 after its lock timeout. The test
started registration and full-year schedule recovery concurrently while an
assignment held the batch lock. The same test passed when run alone, consistent
with launch order and suite load making the two-waiter observation nondeterministic.

The setup now waits until registration is observed waiting on the held batch
lock before it launches schedule recovery, then checks for both waiters. The
focused lock/restart tests passed, and the complete suite passed on
`09d1cfa10377df788783089235bad7d52b14a049`. No production lock behavior or
timeout changed.

## Prior verification carried forward

No database model, migration, API, web, or mobile source changed in this
follow-up. TEST-068 records migration Up/Down/reapply coverage, operational
record preservation checks, SQL Server compatibility 150, and the earlier full
backend run. TEST-066 records local web checks and 46 passing Playwright tests.
These remain relevant to their unchanged source; exact-head GitHub CI for the
new branch head is tracked separately on PR #88 after push.

## Limitations

This record verifies API host restart recovery with a real SQL Server database,
not process-level Kestrel or IIS restart. GSD acceptance, temporary deployment,
production configuration, institutional effective-date approval, Water
Drinking Station frequency confirmation, physical-device acceptance, iOS,
complete WCAG, and screen-reader review remain unverified. The optional
external-provider smoke test was skipped and does not affect deterministic PM
workflow verification.
