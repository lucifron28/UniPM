---
id: TEST-001
type: test-run
title: Current backend verification baseline
status: executed
recordedAtUtc: 2026-07-12T01:02:21.5763922+00:00
testedCommit: 798278fc6d5cb0fd5ad7d19d92e694b8bee338a3
sourceBranch: chore/engineering-evidence
evidenceLevel: locally-executed
---

# Current Backend Verification Baseline

## Objective

Capture current backend restore, build, test, SQL Server integration, and
lexical benchmark evidence using the committed Windows-first script.

## Execution Identity

- Tested commit: `798278fc6d5cb0fd5ad7d19d92e694b8bee338a3`
- Branch: `chore/engineering-evidence`
- UTC execution start: `2026-07-12T01:02:21.5763922+00:00`
- Capture script: `scripts/evidence/Invoke-BackendVerification.ps1`
- Configuration: `Release`
- Script exit code: `0`

## Environment

- Windows 10 build description reported by PowerShell.
- PowerShell: `5.1.26100.8655`
- .NET SDK: `10.0.300`
- Git: `2.54.0.windows.1`
- Docker server: `29.4.1`
- SQL Server configuration was present for this run; the connection value is
  not retained.
- Embedding configuration was not present.

## Commands

The script executed these stages, each with exit code `0`:

```powershell
dotnet restore .\UniPM.slnx
dotnet build .\UniPM.slnx --configuration Release --no-restore
dotnet test .\UniPM.slnx --configuration Release --no-build --logger trx;LogFileName=backend-tests.trx --results-directory <artifact-root>\test-results
dotnet test .\UniPM.slnx --configuration Release --no-build --filter FullyQualifiedName~SqlServer --logger trx;LogFileName=sqlserver-tests.trx --results-directory <artifact-root>\test-results
dotnet run --project .\tools\UniPM.RetrievalBenchmark --configuration Release --no-build -- --channels lexical --output <artifact-root>\benchmark
```

The `<artifact-root>` placeholder represents the local ignored directory below
and is not a secret or a committed absolute path.

## Results

- Restore: passed, exit code `0`.
- Build: passed, exit code `0`.
- Full backend test run: passed, exit code `0`.
- SQL Server test scope: passed, exit code `0`.
- Lexical benchmark: passed, exit code `0`.
- The benchmark completed all 24 manifest queries, including Q024.

## Test Counts

- Full test run: total `151`, passed `150`, failed `0`, skipped `1`.
- SQL Server scope: total `16`, passed `16`, failed `0`, skipped `0`.
- The one full-suite skip is the optional real-provider embedding smoke test;
  no real embedding provider was configured.

## SQL Server Verification

SQL Server integration tests actually executed against the configured local SQL
Server. Sixteen SQL-scoped tests passed. This is local environment evidence,
not CI or deployment evidence.

## AI-Provider Verification

No real embedding provider was configured or executed. Deterministic embedding
tests present in the source tree validate orchestration only and are not
semantic model-quality evidence. No semantic benchmark was run.

## Generated Artifacts

Artifact directory:
`artifacts/evidence/20260712-010221Z-798278fc6d5c/`

The directory is ignored by Git and contains environment metadata, restore,
build, test, SQL test, and benchmark logs, TRX files, reports,
`verification-summary.json`, and `SHA256SUMS.txt`.

Selected SHA-256 values from `SHA256SUMS.txt`:

| Artifact | SHA-256 |
|---|---|
| `benchmark/retrieval-benchmark.json` | `7a31f48c667f8b326124c955419fae89b3441323f9475136812c48e599bd7e0d` |
| `benchmark/retrieval-benchmark.md` | `6d27e9ecb3039c66d591412c25641846ec0fa1f800689be691bd1ed30644646` |
| `test-results/backend-tests.trx` | `2759273424dcd9d82969dd591b9afd895272a8a9001ed559df76d8161e71a3e1` |
| `test-results/sqlserver-tests.trx` | `4594d61b1276ed7f95d99d57ec088472063bc08934110efd4c4cf3f74da729d2` |
| `verification-summary.json` | `4f88f9a5b89a0131d0507877e4e568e45a48eab57fe34b6695d6cf698bde6a49` |

## Failures And Corrections

An earlier script attempt at commit `7975867` failed before artifact creation
because repository detection called the wrong Git form. Commit `8608a49` fixed
that. The next run exposed that the TRX counter reported zero `notExecuted`
tests despite 16 `NotExecuted` result nodes. Commit `798278f` corrected the
parser to count result outcomes directly. This record describes only the final
successful run at `798278f`.

## Skipped Verification

- Real-provider embedding smoke test: skipped because no endpoint, model, API
  key, and test dimensions were configured.
- Semantic benchmark: not executed because real semantic benchmark
  configuration was unavailable.
- CI execution, deployment, observability, and production data verification:
  not executed.

## Limitations

The lexical report is a synthetic benchmark result and does not prove
production GSD performance. The local SQL Server result does not establish CI,
deployment, multilingual production quality, lexicon precision/recall/F1, or
semantic model quality. No raw connection string, API key, prompt, provider
payload, vector, or production record is retained in committed evidence.
