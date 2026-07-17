---
id: TEST-014
type: test-run
title: SQL Server retrieval benchmark warning assertion verification
status: executed
recordedAtUtc: 2026-07-17T11:01:18Z
testedCommit: 795bc3fbc787617695d9d810785607560a904bb5
sourceBranch: fix/retrieval-benchmark-warning-assertion
evidenceLevel: locally-executed
---

# SQL Server Retrieval Benchmark Warning Assertion Verification

## Objective

Verify the test-only correction for the semantic deterministic-provider warning
assertion. The benchmark runner already emitted `pipeline-validation`; the test
now also requires the same warning to state that it is not semantic-model
quality evidence.

## Execution Identity

- Tested commit: `795bc3fbc787617695d9d810785607560a904bb5`
- Branch: `fix/retrieval-benchmark-warning-assertion`
- Execution date: 2026-07-17 UTC
- SQL Server configuration: supplied only through the process environment from
  ignored local Docker configuration. No connection value or credentials are
  recorded.

## Commands

```powershell
dotnet restore .\UniPM.slnx
dotnet build .\UniPM.slnx -c Release --no-restore
dotnet test .\UniPM.slnx -c Release --no-build

dotnet test .\tests\UniPM.Api.Tests\UniPM.Api.Tests.csproj `
  -c Release `
  --no-build `
  --filter "FullyQualifiedName~RetrievalBenchmarkSqlServerTests.Semantic_benchmark_runs_through_sql_pipeline_with_deterministic_provider"

dotnet test .\tests\UniPM.Api.Tests\UniPM.Api.Tests.csproj `
  -c Release `
  --no-build `
  --filter "FullyQualifiedName~RetrievalBenchmarkSqlServerTests"

dotnet test .\tests\UniPM.Api.Tests\UniPM.Api.Tests.csproj `
  -c Release `
  --no-build `
  --filter "FullyQualifiedName~SqlServerInspectionSubmissionIntegrityTests"

dotnet test .\tests\UniPM.Api.Tests\UniPM.Api.Tests.csproj -c Release --no-build
dotnet test .\UniPM.slnx -c Release --no-build
```

## Results

- Restore and Release build succeeded with 0 warnings and 0 errors.
- Ordinary Release suite: 269 passed, 0 failed, 20 skipped because SQL Server
  and optional real-provider configuration were not supplied to that run.
- Direct semantic SQL benchmark regression: 1 passed, 0 failed, 0 skipped.
- `RetrievalBenchmarkSqlServerTests`: 3 passed, 0 failed, 0 skipped.
- `SqlServerInspectionSubmissionIntegrityTests`: 3 passed, 0 failed, 0 skipped.
- Complete SQL-enabled API project: 288 passed, 0 failed, 1 skipped.
- Complete SQL-enabled solution: 288 passed, 0 failed, 1 skipped.

The one SQL-enabled skipped test is the optional real embedding-provider smoke
test. This verification uses the benchmark's deterministic embedding provider
and does not claim real semantic-model quality, production retrieval quality,
or production deployment validation.

## Scope Confirmation

Only `RetrievalBenchmarkSqlServerTests.cs` changed production-adjacent source
coverage. The benchmark runner, warning text, retrieval behavior, benchmark
calculations, and report formats were unchanged.
