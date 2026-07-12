---
id: TEST-003
type: test-run
title: Retrieval fusion implementation baseline
status: superseded
supersededBy: TEST-004
recordedAtUtc: 2026-07-12T10:34:23Z
testedCommit: 58668f963b5cc3aa2b835e8d1f2f985bdf4a3581
sourceBranch: feat/retrieval-fusion
evidenceLevel: locally-executed
---

# Retrieval Fusion Baseline

## Objective

Verify the internal RRF implementation, fused benchmark contracts, CI/evidence
configuration, and complete Release backend suite at one clean branch commit.
This record does not claim real semantic or fused model-quality performance.

## Execution Identity

- Base main commit: `9b975f1949237c615a5575758c0770e634de1d92`
- Tested commit: `58668f963b5cc3aa2b835e8d1f2f985bdf4a3581`
- Branch: `feat/retrieval-fusion`
- Worktree: clean before verification.
- Backend artifact directory: `artifacts/evidence/20260712-103416Z-58668f963b5c`
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
- Release build: passed.
- Full Release test suite: passed.
- Observability Compose configuration: passed.
- Backend evidence capture: passed, exit code `0`.

## Test Counts

| Total | Executed | Passed | Failed | Skipped |
|---:|---:|---:|---:|---:|
| 202 | 185 | 185 | 0 | 17 |

The skipped tests are the existing SQL Server integration tests, the fused
SQL benchmark test, and the optional real-provider smoke test. They were not
represented as successful SQL or provider verification.

## Retrieval And Provider Scope

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
6a8ba86fdc8d61aae371d7f128f8c693ec5d75b137378c3296f8b94bf74c8bf1  build.log
a2a4c365b6b374cad2fb7c6052a81ee94efefa8a10b3312bd872c2410c219632  environment.json
f9cdf588ee17db04553498c9fc62778ea97054063c66b5ff2edcff28246adcb1  restore.log
7243f7c480cf3c5e9a876763a6faf322bd03bdd5a30fa4f0b80e0ba10fe92c5d  test-results\backend-tests.trx
c1d43457da988385a06e4bcfb779576d4a4050d95a70caefc8519a8b89dcb989  tests-console.log
4b62af1cec7445e359d4cff56f9af876bad547f4558a1b4a9b940c90a6f8cc7e  verification-summary.json
```

## Limitations

This is implementation and orchestration evidence on fictional data. It does
not establish semantic model quality, fused retrieval quality, production GSD
performance, SQL Server execution, IIS deployment, Prometheus retention,
alerting, source selection, sanitization, summary quality, or real-user
behavior. The next backend branch is `feat/retrieval-review`.
