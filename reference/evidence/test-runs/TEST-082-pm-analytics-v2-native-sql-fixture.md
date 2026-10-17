---
id: TEST-082
type: test-run
title: PM analytics V2 native SQL verification
status: executed
recordedAtUtc: 2026-10-15T01:08:32Z
testedCommit: d4186c414467f4e2aeb0c7930351fd56615fdf17
sourceBranch: feature/nla-schema-expansion
evidenceLevel: locally-executed
---

# PM analytics V2 native SQL verification

## Execution identity

This record preserves three focused attempts on the same feature branch:

- Attempt 1 used `e12e5ffaab987e20dff8be4881674ae5756f3e5e`.
- Attempt 2 used `3744ac18050f2657c93ad18eb877bc209a3ac5db`, which changed only
  the V2 test fixtures.
- Attempt 3 used `d4186c414467f4e2aeb0c7930351fd56615fdf17`, which added the
  SQL date-projection correction in `PmAnalyticsV2QueryService.cs`.

All three runs used the process-only SQL Server opt-in with Windows integrated
security to local SQL Server 2019. The connection string was supplied only to
the test process and is not included here. No `.env` files were read, no
provider credentials were read or exposed, and no external provider was called.
The native fixture owns a disposable database and drops it in `finally`.

## Attempt 1: invalid SQL fixture value

The focused command was:

```powershell
dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~PmAnalyticsV2' --logger trx --results-directory artifacts/web-qa-20261015/nla-v2/backend-focused-approved -m:1 -nodeReuse:false
```

The focused run exited 1 with 22 passed, 1 failed, 0 skipped, 23 total. The
failure occurred in `SeedAsync` at `PmAnalyticsV2SqlServerTests.cs:553`: SQL
Server rejected a blank `PmCycle` under `CK_Schedules_PmCycle_Format` before
analytics queries ran. This attempt provides no query evidence. A separate
reviewed read-only metadata check later confirmed the test's named database
was absent. Initial sandboxed `sqlcmd` checks could not connect and are not
cleanup evidence.

## Attempt 2: fixture setup passed, first native query failed

The test-only correction used canonical `PmCycle = "2023-02"` in the SQL
fixture and scoped its fallback-count assertion to the canonical-cycle fixture
asset. A new InMemory endpoint test retained blank-cycle fallback coverage.
It queried with Calculation source scope and asserted the resolved cycle,
fallback flag, and Asia/Manila deadline. The test comment clarifies that SQL
requires canonical cycle values.

The focused command was:

```powershell
dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~PmAnalyticsV2' --logger 'trx;LogFileName=PmAnalyticsV2-fixture-corrected.trx' --results-directory artifacts/web-qa-20261015/nla-v2/backend-focused-fixture-corrected -m:1 -nodeReuse:false
```

On commit `3744ac18050f2657c93ad18eb877bc209a3ac5db`, the focused run exited 1
with 23 passed, 1 failed, 0 skipped, 24 total. SQL Server version, Full-Text
Search, compatibility-level 150, and fixture seeding gates passed. The native
test's first V2 query returned HTTP 500 at `PmAnalyticsV2SqlServerTests.cs:65`;
later native assertions were not reached. Retained diagnostics identify an
`InvalidCastException` converting a SQL string result to `DateTimeOffset`
during source-page projection in `PmAnalyticsV2QueryService.ExecuteAsync`
(line 157): SQL Server received a date-map `CASE` whose date branches were
emitted as string literals, so the result could not be materialized as
`DateTimeOffset`. A read-only `sys.databases` query afterward returned no
`UniPMPmAnalyticsV2_*` database names.

## Attempt 3: native SQL verification passed

The correction in commit
`d4186c414467f4e2aeb0c7930351fd56615fdf17` uses the `ScheduleDate` column
expression as the fallback for both cycle-start and deadline maps. Selected
rows have already been filtered to known cycles. Anchoring the SQL `CASE`
result to the datetimeoffset column fixes the materialization type without
changing cycle selection or deadline values.

The exact focused command was:

```powershell
dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~PmAnalyticsV2' --logger 'trx;LogFileName=PmAnalyticsV2-date-projection.trx' --results-directory artifacts/web-qa-20261015/nla-v2/backend-focused-date-projection -m:1 -nodeReuse:false
```

The run exited 0 with 24 passed, 0 failed, 0 skipped, 24 total. The native
`PmAnalyticsV2SqlServerTests.V2_queries_translate_on_sql_server_and_keep_population_and_access_contracts`
fact executed and passed. A post-run read-only `sqlcmd` query against
`sys.databases` for `UniPMPmAnalyticsV2_*` names exited 0 with no matches; the
owned test database was absent. The Release test build reported the existing
nullable warning at `PreventiveMaintenanceFormEndpoints.cs:559`.

The second attempt allowed after the projection correction was unused.

## Retained artifacts and limits

- Attempt 1: `artifacts/web-qa-20261015/nla-v2/backend-focused-approved/` and
  adjacent `backend-focused-approved.log` / `.exit`.
- Attempt 2: `artifacts/web-qa-20261015/nla-v2/backend-focused-fixture-corrected/`
  (`stdout.log`, `stderr.log`, `exit.txt`, and TRX).
- Attempt 3: `artifacts/web-qa-20261015/nla-v2/backend-focused-date-projection/`
  (`stdout.log`, `stderr.log`, `exit.txt`, and `PmAnalyticsV2-date-projection.trx`).
- The ignored patch proposal remains at
  `artifacts/web-qa-20261015/nla-v2/pm-analytics-v2-date-projection-fix.proposed.patch`;
  its change was applied only in commit `d4186c4`.

No additional test, retry, provider call, held-out data access, or user-database
operation was performed. This focused integration evidence does not measure
interpretation quality or establish GSD acceptance.
