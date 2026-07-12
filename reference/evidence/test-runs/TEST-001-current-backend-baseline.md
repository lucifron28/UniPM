---
id: TEST-001
type: test-run
title: Current backend verification baseline
status: executed
recordedAtUtc: 2026-07-12T01:54:36.0528954+00:00
testedCommit: 899ea5e06c19c6604aacd5a05e64f855702eb913
sourceBranch: chore/engineering-evidence
evidenceLevel: locally-executed
---

# Current Backend Verification Baseline

## Objective

Capture current backend restore, build, test, SQL Server integration, and
lexical benchmark evidence using the committed Windows-first script.

## Execution Identity

- Tested commit: `899ea5e06c19c6604aacd5a05e64f855702eb913`
- Branch: `chore/engineering-evidence`
- UTC execution start: `2026-07-12T01:02:21.5763922+00:00`
- Capture script: `scripts/evidence/Invoke-BackendVerification.ps1`
- Configuration: `Release`
- Script exit code: `0`
- Worktree identity: clean before restore, build, and test execution.

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
`artifacts/evidence/20260712-015436Z-899ea5e06c19/`

The directory is ignored by Git and contains environment metadata, restore,
build, test, SQL test, and benchmark logs, TRX files, reports,
`verification-summary.json`, and `SHA256SUMS.txt`.

Selected SHA-256 values from `SHA256SUMS.txt`:

| Artifact | SHA-256 |
|---|---|
| `benchmark/retrieval-benchmark.json` | `7daf33f28a4eacbcdb4abf50f43d1227f05af64e348ece4291a363965558748a` |
| `benchmark/retrieval-benchmark.md` | `38a84c7bc4eff8897c6e4e5295e292661daba4bf4af59fadbe2cdba629355fed` |
| `test-results/backend-tests.trx` | `4231b9a4623a743f7f37e89fb71f68d5ad9a0c43629f31972b1ae1a9d7df9c05` |
| `test-results/sqlserver-tests.trx` | `aaa1caa5d4c0dbd4f7a66aa439b5ca625e30a9d3d13878e46fd0d59fa7837997` |
| `verification-summary.json` | `60a35269d36aea2c1aa328a4c93cfcc65c5402424a3834d4269eaf11e8745df4` |

## Failures And Corrections

An earlier script attempt at commit `7975867` failed before artifact creation
because repository detection called the wrong Git form. Commit `8608a49` fixed
that. The next run exposed that the TRX counter reported zero `notExecuted`
tests despite 16 `NotExecuted` result nodes. Commit `798278f` corrected the
parser to count result outcomes directly. Commit `63f9300` then added clean
worktree identity and dependent-stage gating. Commit `899ea5e` completed the
array-valued combined-channel compatibility. This record describes only the
final successful run at `899ea5e`.

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
