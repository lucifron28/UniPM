---
id: TEST-068
type: test-run
title: Final pre-evaluation backend and SQL Server verification
status: executed
recordedAtUtc: 2026-10-10T06:12:47Z
testedCommit: 473dcbcecc8570e692a1f7a86c743b6d5b57fa8f
sourceBranch: feat/pre-evaluation-ux-motion
evidenceLevel: locally-executed
---

# Final pre-evaluation backend and SQL Server verification

## Objective

Close the remaining native SQL Server and full-backend verification gaps for
PR #88. The three source commits covered here change tests only.

## Execution identity

The exact source and test commit was
473dcbcecc8570e692a1f7a86c743b6d5b57fa8f on
feat/pre-evaluation-ux-motion. The full Release test run began at
2026-10-10T06:04:07Z and finished at 2026-10-10T06:05:37Z.

Environment: Windows, .NET SDK 10.0.300, native SQL Server 2019
15.0.2000.5, Full-Text Search installed, and compatibility level 150. SQL
integration tests created disposable databases through the test fixture. A
read-only post-run check found three test databases created in July 2026 by
earlier runs. They were not part of this run and were left untouched.

The SQL connection came from process-scoped environment variables and used
integrated authentication. No credentials or connection values were recorded.
No AI provider was called.

## Commands and results

| Command | Result |
| --- | --- |
| dotnet build UniPM.slnx --configuration Release --no-restore -m:1 -p:BuildInParallel=false --verbosity minimal -bl:artifacts/test-results/pr88-release-build-473dcbc.binlog | Passed. One existing CS8602 warning remains in PreventiveMaintenanceFormEndpoints.cs:540. |
| dotnet test UniPM.slnx --configuration Release --no-build --no-restore -m:1 -p:BuildInParallel=false --results-directory artifacts/test-results --logger "console;verbosity=minimal" --logger "trx;LogFileName=pr88-backend-release-473dcbc.trx" | 385 passed, 0 failed, 1 optional external-provider smoke test skipped, 386 total. |
| Release TRX classification for SQL Server, SQL Server 2019, and Full-Text tests | 38 passed, 0 failed, 0 skipped. These are tests within the full suite, not a separate SQL-only invocation. |
| Focused test Registration_and_recovery_wait_for_assignment_then_defer_locked_cycle | Passed against native SQL Server 2019. |
| dotnet format whitespace tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --verify-no-changes --no-restore --include Infrastructure/SqlServerDomainContractTests.cs Inspections/SqlServerInspectionSubmissionIntegrityTests.cs Retrieval/ReferenceDocumentSqlServerTests.cs --verbosity quiet | Passed. |
| dotnet ef migrations has-pending-model-changes --project server/server.csproj --startup-project server/server.csproj --configuration Release --no-build | Passed. No pending model changes. A process-only design-time connection setting allowed model construction; EF did not connect to or modify a database. |
| git diff --check | Passed for the staged evidence and test changes. |

Raw build and test output is retained under ignored
artifacts/test-results/pr88-release-build-473dcbc.binlog and
artifacts/test-results/pr88-backend-release-473dcbc.trx.

## SQL Server results

The full-suite TRX contains 38 SQL Server and Full-Text tests. All passed.
The tests exercised the deferral migration round-trip, GSD review concurrency
and paging, schedule-batch application locks, registration/assignment races,
manual schedule creation, inspection/reassignment races, generation
uniqueness, WMS referral concurrency, SQL constraints, and reference
Full-Text/embedding storage.

The deferral migration test created synthetic Asset, schedule, inspection,
acknowledged PM form, acknowledgement, and enrollment-deferral records. It
applied both deferral migrations, rolled back the review migration, and
verified stable IDs for the operational records and that the base deferral row remained while review
metadata columns disappeared. It then rolled back the original deferral-table
migration, verified that table was removed, reapplied it, and verified the
table returned empty. The stable IDs for the Asset, schedule, inspection, PM form, and acknowledgement
remained the same through these transitions. The test does not compare full-row hashes. It also verifies
duplicate deferral prevention and idempotent recovery. It does not claim
deferral rows survive dropping the migration that owns their table.

The GSD review test issued two concurrent reviews for one deferral. One returned
204 and the other 409. The stored reviewer, timestamp, and note matched the
winning request. Pending/reviewed counts, category and department filters, and
two-page results matched the seeded synthetic records.

## Confirmed test-harness defects and fixes

The Full-Text test helper previously waited for a generic matching phrase.
Other documents in the fixture already contained that phrase, so the helper
could proceed before the target ReferenceDocument section was indexed. It now
waits for the fixture-specific SourceKey.

The application-lock observer previously opened a new SQL connection on each
poll and observed for five seconds, the same duration as the application-lock
timeout. Under suite load it could miss the waiter state and report a false
failure. It now reuses one connection and command, observes transaction-owned
lock waiters, and finishes its observation window before the production lock
timeout. The focused race test and full SQL-tagged suite passed after the
change.

The migration coverage did not exercise both deferral migrations through
rollback/reapply or concurrent GSD review. Focused SQL tests now cover those
cases. No production code changed in these three commits.

## Scheduling and deferred-review audit

Source inspection and tests confirm that the optional
ScheduleGeneration:EffectiveDate remains unset by default. Without an approved
effective date, recovery creates only eligible cycles from the current
institutional month onward and reports earlier missing cycles to GSD for
coverage review without creating persistent obligations. Registration retains
its date eligibility, and generation skips deferrals. Institutional cycle
boundaries use Asia/Manila (+08:00).

The deployment environment was not inspected, so this does not prove that a
temporary deployment has no effective-date override. GSD approval of any
effective date remains pending. GSD confirmation of Water Drinking Station
frequency also remains pending because the operative CPMP table and revision
history conflict. The implementation continues to use the operative table
recorded in TEST-060.

The review workflow remains separate from completed or compliant PM counts.
This work did not change scheduling policy, PM workflow semantics, or
production behavior.

## Web and end-to-end evidence

The three commits in this record changed tests only. There is no server or web
source difference from web commit
ecadf495fcfa482a6012f4fa378237596ed5fa35. TEST-066 records the web format,
lint, typecheck, 211 unit tests, API contract and generated-client checks,
production build, and 46 passing Playwright tests. Its live browser workflow
covered GSD, Supervisor, and Inspector through WMS referral using a disposable
SQL database. The browser evidence remains applicable because web and server
source did not change.

No mobile source changed. Flutter, physical-device, iOS, complete WCAG, and
screen-reader verification were not run in this batch.

## Limitations

This record covers local backend, native SQL Server, formatter, and EF model
verification at 473dcbcecc8570e692a1f7a86c743b6d5b57fa8f. Exact-head GitHub CI
for the later evidence-only commit is tracked on PR #88 and reported in its
updated description after push.

GSD acceptance, temporary deployment, production configuration, institutional
effective-date approval, Water Drinking Station frequency confirmation,
physical-device acceptance, iOS, complete WCAG, and screen-reader review
remain unverified.
