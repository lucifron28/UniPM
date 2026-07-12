---
id: TEST-004
type: test-run
title: Retrieval fusion correction verification
status: executed
recordedAtUtc: 2026-07-12T11:28:24Z
testedCommit: 4dffb641a89f140c3ae22f4c9bfd4e774eff54b0
sourceBranch: feat/retrieval-fusion
evidenceLevel: locally-executed
---

# Retrieval Fusion Correction Verification

## Objective

Verify the focused fusion correction after review. The correction preserves
degradation for known semantic availability, execution, and data failures;
surfaces unexpected semantic failures as fused execution failures; records the
embedding descriptor in fused benchmark metadata; and reports deterministic
provider warnings with channel-aware quality limitations.

This record does not claim real semantic or fused model-quality performance.

## Execution Identity

- Tested commit: `4dffb641a89f140c3ae22f4c9bfd4e774eff54b0`
- Branch: `feat/retrieval-fusion`
- Worktree: clean before verification.
- Backend artifact directory: `artifacts/evidence/20260712-112814Z-4dffb641a89f`
- Environment: Windows 10.0.26200, PowerShell 5.1.26100.8655, .NET SDK
  10.0.300, Git 2.54.0, Docker 29.4.1.

## Commands And Results

```powershell
dotnet restore .\UniPM.slnx
dotnet build .\UniPM.slnx --configuration Release --no-restore
dotnet test .\UniPM.slnx --configuration Release --no-build
docker compose --profile observability config --quiet
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\evidence\Invoke-BackendVerification.ps1 -Configuration Release
```

- Restore: passed.
- Release build: passed with zero warnings and zero errors.
- Full Release test suite: passed.
- Observability Compose configuration: passed.
- Backend evidence capture: passed, exit code `0`.
- TEST-003 is superseded for the current fusion branch by this corrected run.

## Test Counts

| Total | Executed | Passed | Failed | Skipped |
|---:|---:|---:|---:|---:|
| 207 | 190 | 190 | 0 | 17 |

The skipped tests are the existing SQL Server integration tests, the fused
SQL benchmark test, and the optional real-provider smoke test. They were not
represented as successful SQL or provider verification.

## Retrieval And Provider Scope

- Committed tests cover known semantic degradation, unexpected semantic failure,
  bounded fused failure metrics, and the fused benchmark descriptor/warning
  assertions. The SQL benchmark test containing the descriptor and warning
  assertions was not executed in this capture.
- SQL Server tests: not executed; `UNIPM_SQLSERVER_TEST_CONNECTION` was absent.
- Real embedding provider: not configured or executed.
- Fused benchmark: not executed; the evidence capture selected
  `BenchmarkChannels=none`.
- Deterministic fusion orchestration: covered by unit tests and the deterministic
  benchmark test path, but not model-quality evidence.
- `EXP-002-fused-rrf-baseline`: not created because no real provider run exists.

## Artifacts And Hashes

Raw artifacts remain ignored under `artifacts/`. The capture recorded no
connection string, API key, provider endpoint, query vector, source text, or
provider payload.

```text
ec2ba45b8532286c1dbaf23c4c1724e2f54b37f62df9f7cfce048f97b3523fb3  build.log
387cb5b6edc45f0f152c426908c5cd636cace1b50539ca4e564b95f2eee14943  environment.json
ab7d22ee65f86b04662632deb3fb9faefd4f16be984b901b9a9feaba6d89d618  restore.log
3261e75eb58dd1c0fbc92511e53db2a3048b9598950b9e0c1ed9d0cb0b0aacb9  test-results\backend-tests.trx
ee0ed945d89536a7f2e6892d63f7954fe5c59e763cadc61a8e40d4e272cdb90e  tests-console.log
9d803a559ddbabb12f3b730bd85e9d3fcaaa837d7468587778687e1cdd5590e6  verification-summary.json
```

## Limitations

This is implementation and orchestration evidence on fictional data. It does
not establish semantic model quality, fused retrieval quality, production GSD
performance, SQL Server execution, IIS deployment, Prometheus retention,
alerting, source selection, sanitization, summary quality, or real-user
behavior. The next backend branch is `feat/retrieval-review`.
