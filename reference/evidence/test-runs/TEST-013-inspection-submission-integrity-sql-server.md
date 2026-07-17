---
id: TEST-013
type: test-run
title: Inspection submission integrity SQL Server verification
status: executed
recordedAtUtc: 2026-07-17T05:19:17Z
testedCommit: 6e8d4b8
sourceBranch: fix/inspection-submission-integrity
evidenceLevel: locally-executed
---

# Inspection Submission Integrity SQL Server Verification

## Objective

Execute the relational migration, unique-index, and concurrent endpoint tests
against the local SQL Server 2025 Full-Text Search Compose service.

## Execution Identity

- Tested commit: `6e8d4b8`
- Branch: `fix/inspection-submission-integrity`
- Execution date: 2026-07-17 UTC
- Database: local temporary SQL Server databases created by the test suite

## Commands

```powershell
dotnet test .\tests\UniPM.Api.Tests\UniPM.Api.Tests.csproj `
  -c Release `
  --no-build `
  --filter "FullyQualifiedName~SqlServerInspectionSubmissionIntegrityTests"

dotnet test .\tests\UniPM.Api.Tests\UniPM.Api.Tests.csproj -c Release --no-build
dotnet test .\UniPM.slnx -c Release --no-build
```

`UNIPM_SQLSERVER_TEST_CONNECTION` was supplied only through the process
environment from the ignored local Docker configuration. It is not recorded
here.

## Results

The focused SQL Server integrity suite passed all three tests:

- migration preflight rejected duplicate existing `ScheduleId` values with the
  expected message;
- the physical unique index rejected a second inspection for one schedule;
- concurrent HTTP submissions produced one HTTP 201 and one HTTP 409, with one
  persisted inspection, one corresponding search document, and a completed
  schedule with `CompletedAt` populated.

The losing concurrent insert logged SQL Server error 2601 and was converted by
the endpoint's narrow unique-constraint handling into the expected conflict.

## Full-Suite Result

The SQL-enabled API-project and solution runs exposed an unrelated existing
failure in `RetrievalBenchmarkSqlServerTests.Semantic_benchmark_runs_through_sql_pipeline_with_deterministic_provider`.
The unchanged test expects `pipeline validation`, while the unchanged benchmark
runner emits `pipeline-validation`. This retrieval-benchmark assertion mismatch
is present on `main` and is outside the inspection-integrity branch scope.

## Limitations

The focused SQL Server integrity verification is successful. The full
SQL-enabled suite is not green until the separate pre-existing retrieval
benchmark assertion mismatch is corrected. This record does not claim CI,
production IIS deployment, or institutional workflow validation.
