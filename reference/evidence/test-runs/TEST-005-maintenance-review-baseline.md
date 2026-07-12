---
id: TEST-005
type: test-run
title: Maintenance review baseline
status: superseded
recordedAtUtc: 2026-07-12T13:24:30Z
testedCommit: 079240dce614de247b65a43e730a0bc2fda8eeac
sourceBranch: feat/retrieval-review
evidenceLevel: locally-executed
---

# Maintenance Review Baseline

## Commands And Results

```powershell
dotnet restore .\UniPM.slnx
dotnet build .\UniPM.slnx --configuration Release --no-restore
dotnet test .\UniPM.slnx --configuration Release --no-build
docker compose --profile observability config --quiet
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\evidence\Invoke-MaintenanceReviewVerification.ps1
```

- Restore: passed.
- Release build: passed with zero warnings and zero errors.
- Full Release test suite: passed.
- Docker Compose validation: passed with local Docker config permission
  warnings.
- Maintenance-review verification script: executed against a clean worktree,
  but Docker stack startup could not access the local Docker named pipe. The
  API endpoint was therefore not exercised by the script.
- In-memory WebApplicationFactory source-only endpoint tests: passed.

## Test Counts

| Total | Executed | Passed | Failed | Skipped |
|---:|---:|---:|---:|---:|
| 219 | 202 | 202 | 0 | 17 |

Skipped tests are the existing SQL Server integration tests, retrieval
benchmarks, and optional real embedding-provider smoke test. No test failure is
represented as a passed SQL or provider verification.

## Provider And Database Scope

- SQL Server integration tests: not executed; no configured SQL test connection.
- Local scripted endpoint verification: not completed because Docker named-pipe
  access was denied by the execution environment.
- Summary provider: not executed or configured.
- Real embedding provider: not executed or configured.
- Synthetic data: only in-memory test data was used by the review endpoint tests.
- No migration, review persistence, prompt persistence, summary persistence, or
  token-map persistence was added.

## Artifacts And Hashes

The attempted clean-worktree script capture is retained under the ignored path
`artifacts/evidence/20260712-132159Z-079240dce614-maintenance-review`. It
contains only the failure summary and bounded error text:

```text
03cc2d7a91cd8416afbbf03f3be3245ea7a0fb24ecc703542d09de0433b5dea8  error.txt
ddb15b22ed298fdfb47f9cf7bf87c6c56350c3b96c25ecc4e26f74a891bd99ae  verification-summary.json
```

The artifact records `worktreeClean=true`, `summaryProviderExecuted=false`,
and `realEmbeddingProviderExecuted=false`. It does not contain a provider
endpoint, API key, prompt, token map, response source text, or vector.

## Limitations

This is local implementation and orchestration evidence on fictional data. It
does not establish real summary quality, generated-summary faithfulness,
authenticated production access, SQL Server execution, IIS deployment, or
production GSD performance. TEST-004 remains unchanged as the prior fusion
record.
