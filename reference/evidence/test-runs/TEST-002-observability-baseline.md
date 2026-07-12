---
id: TEST-002
type: test-run
title: Observability metrics and local monitoring baseline
status: executed
recordedAtUtc: 2026-07-12T05:47:19.6156783+00:00
testedCommit: 3b2e742c8cf780c3dc8c68e4373fe750fee39a27
sourceBranch: feat/observability-metrics
evidenceLevel: locally-executed
---

# Observability Baseline

## Objective

Verify the backend and the optional local Prometheus/Grafana observability
profile at one exact clean-worktree commit without claiming production
monitoring, deployment, alerting, or retrieval-quality improvement.

## Execution Identity

- Tested commit: `3b2e742c8cf780c3dc8c68e4373fe750fee39a27`
- Branch: `feat/observability-metrics`
- Worktree: clean before both verification runs.
- Backend artifact directory: `artifacts/evidence/20260712-054633Z-3b2e742c8cf7`
- Observability artifact directory: `artifacts/evidence/20260712-054719Z-3b2e742c8cf7-observability`
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
27d85860f0f885314fe835d2f5cb4247c10aa643666aa35d4495b1668958f9a4  build.log
beb7004c9ee523d2314500bf4af953c9f90ee88cf4b65f3734b8267ff46ce422  environment.json
adc9b358b0e21b3acdc06b4664f232efe75aebe3d5fe44c8b5748c486bca6929  restore.log
3c988f044429ea1a0fc00341fe42c27e0c01241d6cf61a5587d89c87c9f31215  test-results\backend-tests.trx
aab4018f1525c8a7df3c0765f361dc2d06e6782cc1724f9fcc416cf7ba44846f  tests-console.log
48bd972aa45ec29fd3bafa0c0885adeebb779354e96262249bf0e8b480c43906  verification-summary.json
```

Observability artifact hashes:

```text
0efaa888a5a508b0a4a9bc5cac8737915c35287602de7166d20a6fd685b97bed  api-liveness.json
21d961a3bc9f82f7c95e92e2a2707e4a0f5df4dc4b293965411710342df213e1  api-metrics.json
ca2fd9636ae5f3914231505e326c1dab01d0c2043b34a3b816ab9126b750aa3d  api-metrics-sample.txt
a53dc01bc0366f2159a1f7f7137d4f270f4df7c7445e032a6b814c0aff2b5bd5  api-root.json
e0e7c2d4f39d1cd18336fe1733eaa35d0776033873c2ec277e9dde26e56ff67e  compose-config.log
3dddc225ce422198b47b5c92109dc8bac9e82e13aac1366e2403447a568979ed  compose-down.log
fbe06f333cc2924ac6e17a1c079f03079c30d34b71a8c0275cafaf7e7ff0c791  compose-up.log
aa3d29e6cb402b13c7a20f42f8bdac0a3feb1a401472a72684695756fd081f38  environment.json
a1666f17dd605a5767ccd822b13b3b230fd68bed30b488342085ae387a002ff2  grafana-dashboard.json
855dad2a279172b93ae0c508772ebeeccba215ec0b0679cd096b185e1cc03dea  grafana-datasource.json
177781537caedca5bfb6b88fcfdc4e8b10c1b5a05d5c3136e8ca11bfc12216ae  grafana-health.json
48941fcac516f5ca05a299602a02823b022e027c6deb6e4df5292b206b84e757  prometheus-ready.txt
120734c4bc08156609179ec83e6e4ab8731e69907eccf1551c94d1915d83e6a6  prometheus-targets.json
18f09e5a241767adbfbb4c6339ac99762002af73ad45417f8812b1369d3b7952  verification-summary.json
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
