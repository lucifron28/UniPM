---
id: TEST-161
type: test-run
title: Authorized form pagination and bounded report scale
status: executed
recordedAtUtc: 2026-10-25
testedCommit: 03ba54bd711f176130252dd5e729957b5d49cce1
additionalTestedCommits:
  - 1dd8a75c0ec5027e9815b8f5d52e8bd0f0e036a2
  - 30beffbc152e5d15cf51274b534b54682c6b62c4
  - 2d3925540a8146df42a5241d88e3f06c6a8fcda2
sourceBranch: feature/capstone-remediation
evidenceLevel: locally-executed
---

# Authorized form pagination and bounded reporting

The [page contract](../../planning/capstone-form-pagination-contract.md)
applies authorization before filters, total count and deterministic paging.
Only the selected page loads related graphs. The scalar options endpoint
replaces the web's unpaged second form download. Mobile follows every page
at size 100 and rejects inconsistent counts/pages or duplicate IDs.

Backend passing runs below used `03ba54bd711f176130252dd5e729957b5d49cce1`,
.NET SDK 10.0.300/runtime 10.0.8 and the local native SQL Server 2019 Developer
instance. SQL connections were process-only Windows authentication to master;
fixtures created fresh GUID databases, used compatibility 150 and dropped
them on disposal. No application or institutional database was used. No
post-test database inventory was captured for these Phase 7 runs.

| UTC interval | Command filter | Result |
|---|---|---|
| 08:31:58.032–08:32:52.910 | `FullyQualifiedName~PreventiveMaintenanceFormRegistryQueryEndpointsTests\|FullyQualifiedName~OperationalInspectionReadScopeEndpointsTests\|FullyQualifiedName~AuthorizationPolicyTests\|FullyQualifiedName~RegistryReadEndpointsTests.Sql_server_2019_pm_form_pages_scope_before_supervisor_filter_and_options` | 22 passed, zero failed/skipped |
| 08:34:21.357–08:34:54.987 | `FullyQualifiedName~PmAnalyticsV3BoundedScaleTests.Synthetic_report_scales_are_measured_and_50000_row_limit_is_enforced` | One passed, zero failed/skipped |
| 08:35:02.957–08:36:27.602 | `FullyQualifiedName~PmAnalyticsV3SqlServerTests.Sql_server_2019_synthetic_pm_reports_measure_100_1000_and_10000_rows` | One passed, zero failed/skipped |

Date is October 25, 2026. Every command used this shape, substituting the
literal filter and log filename above:

```powershell
dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --filter "<filter>" --logger "console;verbosity=detailed" --logger "trx;LogFileName=<name>.trx" --results-directory artifacts/remediation/backend
```

TRX names are `phase7-pagination-auth-final.trx`, `phase7-report-scale.trx`
and `phase7-sql-scale.trx`. Exact argv, output and UTC intervals remain in
ignored `artifacts/remediation/backend/phase7.md` and its command logs.
The SQL cases asserted product major version 15 and compatibility 150.

## Measured synthetic execution

| Eligible rows | InMemory Inventory ms | InMemory PM ms | Native SQL PM ms |
|---:|---:|---:|---:|
| 100 | 1113.44 | 1129.44 | 2381.68 |
| 1,000 | 68.69 | 1100.58 | 2475.71 |
| 10,000 | 73.60 | 1082.32 | 1613.45 |
| 50,000 | 169.82 | 1175.40 | Not run |

Both subjects rejected 50,001 in InMemory. These are single local diagnostic
measurements, with cold-start effects. They do not establish monotonic scaling,
an SLA, concurrency performance or production-host capacity. SQL fixtures
contained 11,100 rows across the three measured categories. No query plan or
logical-read capture was performed.

Existing indexes cover form category/status and department/category/cycle,
schedule asset/cycle and status/date, and inspection schedule uniqueness.
There is no dedicated covering index for the new created-time/supervisor
query. This run did not justify speculative indexes, schema changes or a
new aggregation implementation. The 50,000-row cap and calculations remain.

## Clients and retained failed attempts

At `1dd8a75`, Flutter 3.44.5 ran
`flutter test --concurrency=2 test/preventive_maintenance_form_draft_test.dart`
in `mobile/`. All 48 passed, exit zero. Mobile source was committed unchanged
from `e500dcb3f141f633839d67c1da1f1ea45af21e81`.

At `30beffb`, Node 22.23.4 ran
`node ./node_modules/vitest/vitest.mjs run --maxWorkers=2 src/features/preventive-maintenance-forms/preventive-maintenance-form-review.test.tsx`
in `web/`. All 24 passed, exit zero, 08:44:38.548–08:45:09.542 UTC. Logs are under ignored
`artifacts/remediation/clients/phase7-*`. Earlier 23-pass/one-fail attempts
are retained. The first used an unsuitable text query and omitted a mock;
the corrected fixture then exposed a real bug in the pagination controls.
`FormPagination.total` still received the current page's row count. Commit
`30beffb` changes it to the server's total count. Assertions remain intact.

The initial backend compile failed at `ee4e409` because a new test called a
nonexistent helper. At `62b87c1`, 21 passed and one failed because the new
InMemory fixture had an orphan schedule FK. `03ba54b` uses a valid unassigned
schedule and passes on both relational and InMemory paths. These fixture
failures are not reported as production paging defects.

The generated snapshot came from an isolated loopback API with no application
connection, schedule generation or AI. That process was stopped after pull.
Generated types incorporate the current API's pre-existing schedule and enum
contracts as well as the new page/options models. At `30beffb`,
`node ./node_modules/typescript/bin/tsc -b`,
`node ./scripts/check-openapi-contract.mjs` and
`node ./scripts/test-negative-openapi-contract.mjs` passed. The last command
passed all eleven negative checks. At `2d39255`, full
`node ./node_modules/prettier/bin/prettier.cjs --check .` passed, including
the changed snapshot. Scoped ESLint passed for both contract scripts and the
four form contract/query/registry/test files. Its exact argv is retained in
`phase7-web-eslint.log`. `node ./scripts/check-generated.mjs` passed after
the successful Orval generation. All exits were zero. The final formatting
commits change no API meaning or form runtime behavior.

Offset pagination does not provide a concurrent snapshot. A changing data set
may require retry. Old array-list clients require a coordinated upgrade.
These tests do not establish current-build physical-device acceptance.
