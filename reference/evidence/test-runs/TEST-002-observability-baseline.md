---
id: TEST-002
type: test-run
title: Observability metrics and local monitoring baseline
status: executed
recordedAtUtc: 2026-07-12T05:27:26.3049014+00:00
testedCommit: c4fc0dfe1cf21e959ffaecae819373d4ad8db3fa
sourceBranch: feat/observability-metrics
evidenceLevel: locally-executed
---

# Observability Baseline

## Objective

Verify the backend and the optional local Prometheus/Grafana observability
profile at one exact clean-worktree commit without claiming production
monitoring, deployment, alerting, or retrieval-quality improvement.

## Execution Identity

- Tested commit: `c4fc0dfe1cf21e959ffaecae819373d4ad8db3fa`
- Branch: `feat/observability-metrics`
- Worktree: clean before both verification runs.
- Backend artifact directory: `artifacts/evidence/20260712-052641Z-c4fc0dfe1cf2`
- Observability artifact directory: `artifacts/evidence/20260712-052725Z-c4fc0dfe1cf2-observability`
- Execution environment: Windows, PowerShell 5.1, .NET SDK 10.0.300, Git 2.54.0, Docker 29.4.1.

## Environment

Metrics were enabled only inside the verification process. Committed
`appsettings.json` remains disabled by default. The SQL Server test connection
and real embedding provider were not configured for this record.

## Commands

Backend capture:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\evidence\Invoke-BackendVerification.ps1 -Configuration Release
```

The capture script executed:

```text
dotnet restore .\UniPM.slnx
dotnet build .\UniPM.slnx --configuration Release --no-restore
dotnet test .\UniPM.slnx --configuration Release --no-build --logger "trx;LogFileName=backend-tests.trx" --results-directory artifacts/evidence/<run>/test-results
```

Observability capture:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\evidence\Invoke-ObservabilityVerification.ps1
```

The script validated Compose, started
`docker compose --profile observability up --build -d`, polled the API,
Prometheus, and target health, checked Grafana health and provisioned objects,
then ran `docker compose --profile observability down`.

## Results

- Backend capture: passed, exit code `0`.
- Restore: passed.
- Release build: passed.
- Full backend tests: passed.
- Compose configuration: passed.
- Compose observability profile startup: passed.
- API root: HTTP `200`.
- `/health/live`: HTTP `200`.
- `/metrics`: HTTP `200` with the Prometheus-compatible text response asserted
  by endpoint tests.
- Prometheus readiness: HTTP `200`.
- Prometheus target `unipm-api:8080`: `up`.
- Grafana health: HTTP `200`.
- Grafana datasource: UID `unipm-prometheus` present.
- Grafana dashboard: UID `unipm-system-health`, title `UniPM API System Health`.
- Stack teardown: passed.

## Test Counts

The backend TRX summary recorded:

| Total | Executed | Passed | Failed | Skipped |
|---:|---:|---:|---:|---:|
| 169 | 153 | 153 | 0 | 16 |

The 16 skipped tests are existing SQL Server and optional real-provider cases;
they were not represented as successful SQL or provider verification.

## SQL Server Verification

SQL Server was present inside the local Compose stack for API readiness, but
the SQL Server integration-test scope was not requested by the backend capture
and no SQL Server test connection was configured for this record. SQL tests are
therefore recorded as not executed.

## AI-Provider Verification

No real embedding provider was configured or called. Deterministic provider
tests remain orchestration evidence only. No semantic model-quality claim is
made.

## Generated Artifacts

Raw artifacts remain ignored under `artifacts/`. The final observability run
contains sanitized endpoint samples, Prometheus target metadata, Grafana health,
datasource identity, dashboard identity, command logs, summaries, and SHA-256
hashes. No raw credentials, connection strings, provider payloads, or vectors
were copied into this record.

Backend artifact hashes:

```text
d03dd82d507fb7c408d9928908db2d47a427c809136ea0e0b1453fef9778d2d1  build.log
49090e81d19136e399f68afaa66785fb42278078286869726605e52893960456  environment.json
e456cda916d42eb921257be70199f4604d0f53cf74e0f301e1923b790a2338a9  restore.log
809aa3f7fb9e25acef98359b548b67298ed77d7636e29c9f06f191f7ea2b2b70  test-results\backend-tests.trx
9dc0277bf522c3c7c004ef1ebd9b232d4fd9f8aa429df2d00a0fd5a4de7d0b37  tests-console.log
bebd7c92f5326dcb208105c83da9075baa866a73237a487f7246530455aaf5cd  verification-summary.json
```

Observability artifact hashes:

```text
0efaa888a5a508b0a4a9bc5cac8737915c35287602de7166d20a6fd685b97bed  api-liveness.json
21d961a3bc9f82f7c95e92e2a2707e4a0f5df4dc4b293965411710342df213e1  api-metrics.json
f9ccb272dc40285505a938cc8ac4eaaf39c4194e33b07b5437dae5d62133dedd  api-metrics-sample.txt
a53dc01bc0366f2159a1f7f7137d4f270f4df7c7445e032a6b814c0aff2b5bd5  api-root.json
f24fd1cf5b6477f3b6edd6e06dc77df2978fd9f21e016afe22d73614c996a659  compose-config.log
ecaa371730d48cb6a48c47cee76e50141453192cd3428d7038821a9654893a61  compose-down.log
29567fd4bf4cf1318bff6c828a2d53f6118edde7711572ed735d205dd0ff815a  compose-up.log
a973509ec8cd0e6087694af2d3cfef769143b36ca4e8b38b89ad054cec6a8bd5  environment.json
a1666f17dd605a5767ccd822b13b3b230fd68bed30b488342085ae387a002ff2  grafana-dashboard.json
855dad2a279172b93ae0c508772ebeeccba215ec0b0679cd096b185e1cc03dea  grafana-datasource.json
177781537caedca5bfb6b88fcfdc4e8b10c1b5a05d5c3136e8ca11bfc12216ae  grafana-health.json
48941fcac516f5ca05a299602a02823b022e027c6deb6e4df5292b206b84e757  prometheus-ready.txt
120734c4bc08156609179ec83e6e4ab8731e69907eccf1551c94d1915d83e6a6  prometheus-targets.json
41ad4727d75b4e4aa5e642fe1e877199a70a8798c0bf54fbdcb9443eb18455a989  verification-summary.json
```

## Failures And Corrections

Earlier local attempts are retained as ignored artifacts and are not used as
the final baseline. They exposed sandbox NuGet access, PowerShell native-stderr
handling, early target polling, and the need for a process-scoped metrics
override. The final script corrections were committed before the final paired
run, and the final run passed at the tested commit above.

## Skipped Verification

- SQL Server integration-test scope.
- Real embedding-provider execution and semantic model quality.
- IIS deployment and production endpoint restriction.
- Long-term Prometheus retention.
- Alert delivery/effectiveness, centralized logs, tracing, and real user traffic.

## Limitations

This is a clean local Docker proof of optional infrastructure and endpoint
behavior. It does not establish production uptime, institutional deployment,
alert effectiveness, retrieval quality, or business KPI correctness. No
retrieval experiment record is created because retrieval ranking was unchanged.
