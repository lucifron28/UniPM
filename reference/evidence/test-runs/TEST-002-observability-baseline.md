---
id: TEST-002
type: test-run
title: Observability metrics and local monitoring baseline
status: executed
recordedAtUtc: 2026-07-12T05:38:32.6252368+00:00
testedCommit: 2766f1bcc18d7aa617ebdae12a9a381af6f1107b
sourceBranch: feat/observability-metrics
evidenceLevel: locally-executed
---

# Observability Baseline

## Objective

Verify the backend and the optional local Prometheus/Grafana observability
profile at one exact clean-worktree commit without claiming production
monitoring, deployment, alerting, or retrieval-quality improvement.

## Execution Identity

- Tested commit: `2766f1bcc18d7aa617ebdae12a9a381af6f1107b`
- Branch: `feat/observability-metrics`
- Worktree: clean before both verification runs.
- Backend artifact directory: `artifacts/evidence/20260712-053747Z-2766f1bcc18d`
- Observability artifact directory: `artifacts/evidence/20260712-053832Z-2766f1bcc18d-observability`
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
4338343974d99d5c238c80c4a0b7430018a572588b3d489e8488389e06772663  build.log
46c90340c2ec612846839a783fdbf3855731cec2d53cdd5f3e694023f613bc80  environment.json
7c1f32b54fe97ecd7b05fd3315b4896d1471ad042232cb122a1b5867e22e1363  restore.log
6abe52499c2228b1c43794454eb050d2997b6296c68aa96efaff5763602e5446  test-results\backend-tests.trx
a0944e0bbb947b27c21f02024f24460c412e36157ca71522bef30dd624d5c7ca  tests-console.log
c42baf55e89f39daa49332c8ae27fec72eec441bd593d7539c7eefb6cd301e13  verification-summary.json
```

Observability artifact hashes:

```text
0efaa888a5a508b0a4a9bc5cac8737915c35287602de7166d20a6fd685b97bed  api-liveness.json
21d961a3bc9f82f7c95e92e2a2707e4a0f5df4dc4b293965411710342df213e1  api-metrics.json
0b828fd3997aad6c23688970c62619fa6df25c43a186baba9f186865fdc663d6  api-metrics-sample.txt
a53dc01bc0366f2159a1f7f7137d4f270f4df7c7445e032a6b814c0aff2b5bd5  api-root.json
40f1b0e5beac41733d66c75e6cf8c008d90a9bd854831dd8482c88685eb6d208  compose-config.log
1d8ae60d2af93af50c520b5559675d50f73f86fdb4129e5ef6474a4fe6633649  compose-down.log
e3c89a43213655e6fae67d2478b9421a16e2c5f8f8c3dcfb77d1b656bf543fec  compose-up.log
a01f6d9875172ad9c1ae931484e73d99b2015b52ef7d8b6eeaffcb19a39165c7  environment.json
a1666f17dd605a5767ccd822b13b3b230fd68bed30b488342085ae387a002ff2  grafana-dashboard.json
855dad2a279172b93ae0c508772ebeeccba215ec0b0679cd096b185e1cc03dea  grafana-datasource.json
177781537caedca5bfb6b88fcfdc4e8b10c1b5a05d5c3136e8ca11bfc12216ae  grafana-health.json
48941fcac516f5ca05a299602a02823b022e027c6deb6e4df5292b206b84e757  prometheus-ready.txt
120734c4bc08156609179ec83e6e4ab8731e69907eccf1551c94d1915d83e6a6  prometheus-targets.json
6d5001c329ad4f0dc705c538c67997a1f89d8f31cadedd9ad26489798ef32d23  verification-summary.json
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
