---
id: TEST-022
type: test-run
title: Native SQL Server 2019 compatibility verification
status: executed
recordedAtUtc: 2026-07-24T15:52:23Z
testedCommit: fe06015670ad9a0bcb26b6ee337cfee40c11f42b
sourceBranch: spike/sqlserver-2019-compatibility
evidenceLevel: locally-executed
---

# Native SQL Server 2019 Compatibility Verification

## Objective

Execute the SQL Server 2019 compatibility path on a native Windows Developer
instance with Full-Text and Semantic Extractions for Search installed. This
supersedes the environment gap recorded in TEST-021 without rewriting that
historical blocked Docker attempt.

## Execution Identity

- Tested commit: `fe06015670ad9a0bcb26b6ee337cfee40c11f42b`
- Branch: `spike/sqlserver-2019-compatibility`
- Execution date: 2026-07-24 UTC
- SQL Server configuration: supplied only through the process environment.
  Connection strings, credentials, and the temporary Development seed password
  were not recorded.

## Commands

```powershell
$env:ConnectionStrings__DefaultConnection = "<process-only native SQL Server 2019 application database>"
$env:UNIPM_SQLSERVER2019_TEST_CONNECTION = "<process-only native SQL Server 2019 test connection>"
$env:UNIPM_DEV_USER_PASSWORD = "<process-only generated password>"

powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\evidence\Invoke-SqlServer2019CompatibilityVerification.ps1
```

The runner executed `dotnet restore`, the Release build, database migration,
synthetic seeding, Development user seeding, maintenance-search-document
rebuild, and `dotnet test .\UniPM.slnx -c Release --no-build`.

The SQL Server 2019 readiness test uses only
`UNIPM_SQLSERVER2019_TEST_CONNECTION`. The runner maps that dedicated
connection to `UNIPM_SQLSERVER_TEST_CONNECTION` only for the existing general
SQL integration suite. This keeps ordinary SQL Server 2025 test runs from
executing the major-version-15 assertion.

## Results

- Native SQL Server major version: 15.
- Database compatibility level: 150.
- Full-Text Search installed: yes.
- `UniPMMaintenanceRetrieval` Full-Text catalog: one.
- Enabled `MaintenanceSearchDocuments` Full-Text index: one.
- Migration, seed, Development-user seed, and projection rebuild commands: all
  completed successfully.
- The SQL Server 2019-specific migration and `CONTAINSTABLE` test passed as
  part of the full SQL-enabled suite.

## Test Counts

| Scope | Passed | Failed | Skipped |
|---|---:|---:|---:|
| Complete SQL-enabled solution suite | 310 | 0 | 1 |

The single skipped test was the optional real embedding-provider smoke test.
SQL migration, lexical retrieval, deterministic semantic retrieval, fusion,
benchmark, and refresh-session integration tests ran using the configured
native SQL Server test connection.

## SQL Server Verification

The native instance accepted the current migrations at compatibility level 150.
The domain-contract migration canonicalized legacy values, and the Full-Text
catalog/index and a `CONTAINSTABLE` query over `MaintenanceSearchDocuments`
executed successfully. This run used a native Windows SQL Server 2019 Developer
instance; the default SQL Server 2025 Compose stack was not modified.

## AI-Provider Verification

No real embedding or summary provider was configured or called. Deterministic
embedding tests validate SQL persistence and retrieval orchestration only; they
do not establish semantic-model, fused-retrieval, or production quality.

## Generated Artifacts

Reviewed local artifacts remain ignored under
`artifacts/evidence/sqlserver2019-20260724-235159/`. The reviewed
`sql-server-probes.json` contains only version, compatibility, Full-Text, and
object-count facts; it contains no connection information or secrets.

## Failures And Corrections

Initial native execution exposed a migration splitter issue: SQL Server could
evaluate a tally `SUBSTRING` expression before its filter, producing an invalid
length. The final tested commit bounds the splitter with `DATALENGTH` and a
sentinel delimiter run. The focused migration-preflight SQL test and final
complete suite passed after that correction.

## Skipped Verification

- Real embedding-provider smoke testing, because no real provider configuration
  was supplied.
- IIS deployment and production workload validation.

## Limitations

SQL Server Developer Edition is for development and testing only. This result
does not establish production deployment readiness, institutional workload
fitness, real semantic-model quality, or final GSD workflow approval.
