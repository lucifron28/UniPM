---
id: TEST-002
type: test-run
title: Observability metrics and local monitoring baseline
status: executed
recordedAtUtc: 2026-07-12T07:11:14.1855329+00:00
testedCommit: 6691f048c9d034b97f5044212d1bc47f16523d50
sourceBranch: feat/observability-metrics
evidenceLevel: locally-executed
---

# Observability Baseline

## Objective

Verify the backend and the optional local Prometheus/Grafana observability
profile at one exact clean-worktree commit without claiming production
monitoring, deployment, alerting, or retrieval-quality improvement.

## Execution Identity

- Tested commit: `6691f048c9d034b97f5044212d1bc47f16523d50`
- Branch: `feat/observability-metrics`
- Worktree: clean before both verification runs.
- Backend artifact directory: `artifacts/evidence/20260712-054633Z-3b2e742c8cf7`
- Observability artifact directory: `artifacts/evidence/20260712-071114Z-6691f048c9d0-observability`
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
- `/health/ready`: HTTP `200`.
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
a7bf1b2cfec7513adc9119402f4b2128f08879066587e778b845164d382eea85  api-metrics-sample.txt
929f57ed7d3a308c50873fe872961076d25c6279d364ef01d42bf759630ea705  api-readiness.json
dfc57e459833706e41a7a2d64208c72858e0f7c3e318f867ff4db9f0a37258cb  api-root.json
450ca3bb36dbbddf3fa8854111b8ea30d71310221de15c4e5b65917931bf1c93  compose-config.log
057253d6dbff7a92067492e306341b2d2e9a923d123de60efec2965f92e7ecd5  compose-down.log
5152ef1341b67631c98828879533c913ccdd7ee1ea629294ccf0849d5b7192a9  compose-up.log
6bf41ffc3072aca25b285b1433fa35a9674b69428d6a3c54ba330734cb7e07bf  environment.json
c82615a7601401e98b076e73fb3223b88d4bdddf1181b0ef1096e5418aaee80a  grafana-dashboard.json
855dad2a279172b93ae0c508772ebeeccba215ec0b0679cd096b185e1cc03dea  grafana-datasource.json
177781537caedca5bfb6b88fcfdc4e8b10c1b5a05d5c3136e8ca11bfc12216ae  grafana-health.json
48941fcac516f5ca05a299602a02823b022e027c6deb6e4df5292b206b84e757  prometheus-ready.txt
120734c4bc08156609179ec83e6e4ab8731e69907eccf1551c94d1915d83e6a6  prometheus-targets.json
be4f1f1382475a85d5e1ea8b21da178c99d40ae407bed0b0089cf0293d433a98  verification-summary.json
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
