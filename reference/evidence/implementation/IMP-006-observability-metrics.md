---
id: IMP-006
type: implementation
title: Optional OpenTelemetry metrics and local system-health monitoring
status: reviewed
recordedAtUtc: 2026-07-12T05:27:26.3049014+00:00
testedCommit: c4fc0dfe1cf21e959ffaecae819373d4ad8db3fa
sourceBranch: feat/observability-metrics
evidenceLevel: source-inspected
---

# Optional Observability Metrics

## Objective

Add a narrow technical observability foundation before retrieval fusion without
changing retrieval ranking, adding a public retrieval endpoint, or making
monitoring infrastructure a runtime dependency.

## Source Identity

The implementation was developed on `feat/observability-metrics` from current
`main`. Relevant commits are:

- `56e634b` - expose OpenTelemetry Prometheus metrics and bounded retrieval instrumentation;
- `b2ec945` - provision the optional Prometheus and Grafana Compose profile;
- `c5376e3` - add metrics, endpoint, privacy, and provisioning tests;
- `9fc7d9d` - add CI validation and the observability evidence script;
- `da6df8f`, `61ce84e`, `b4411f9`, `d948e1e`, and `6e5de8f` - correct and harden evidence-script execution and provisioning verification;
- `a32e378` - declare the Prometheus `/metrics` scrape path explicitly;
- `3b2e742` - enable metrics in the local environment example while retaining the disabled appsettings default;
- `c4fc0df` - add direct custom-meter bounded-tag coverage;
- corrective changes - remove non-scrapeable CLI rebuild instruments, add hosted
  Prometheus family coverage, and harden readiness and port verification.

## Implementation Summary

- `Observability:MetricsEnabled` is false in committed appsettings and is
  explicitly enabled only for the local verification profile.
- `/metrics` is mapped only when metrics are enabled and is not added to
  OpenAPI.
- OpenTelemetry collects the UniPM meter, ASP.NET Core hosting and Kestrel
  meters, and `System.Runtime`.
- The custom meter is `UniPM.Api` with hosted retrieval instruments only.
- Retrieval decorators record channel, bounded outcome, duration, and result
  count while preserving the inner result and exception behavior.
- Projection and embedding rebuild commands retain explicit command output and
  evidence records; durable job telemetry is deferred because those processes
  exit before a Prometheus scrape can observe them.
- `/health/live`, `/health/ready`, and `/metrics` are excluded from HTTP
  request metrics.

## Architecture And Contracts

The Prometheus ASP.NET Core exporter is pinned to prerelease
`1.16.0-beta.1`; OpenTelemetry core and hosting packages are pinned to
`1.16.0`. The prerelease limitation is documented in ADR-003.

Allowed custom metric dimensions are limited to `channel` and `outcome`.
Retrieval channels are `lexical` and `semantic`; retrieval outcomes are
`success`, `empty`, `validation_error`, `unavailable`, `failure`, and
`cancelled`.

## Important Files

- `server/Observability/ObservabilityOptions.cs`
- `server/Observability/UniPMMetrics.cs`
- `server/Features/Retrieval/RetrievalMetricsDecorators.cs`
- `server/Program.cs`
- `docker-compose.yml`
- `observability/prometheus/prometheus.yml`
- `observability/grafana/provisioning/`
- `observability/grafana/dashboards/unipm-system-health.json`
- `scripts/evidence/Invoke-ObservabilityVerification.ps1`
- `tests/UniPM.Api.Tests/Observability/`

## Database Changes

None. This branch does not add migrations, tables, indexes, exporters, or
database metrics.

## Tests Present

The repository contains tests for retrieval outcome mapping, exception and
ordering preservation, duration/result recording, bounded tags, hosted
Prometheus family names, endpoint enablement, health behavior, privacy-safe
output, Compose configuration, and dashboard/datasource provisioning.

## Verification Status

Source inspection is recorded here. Actual local execution is recorded in
TEST-002 at commit `6691f048c9d034b97f5044212d1bc47f16523d50`, which contains
the source-inspected implementation listed here.

## Known Limitations

The local Docker run is not production monitoring evidence. IIS deployment,
long-term retention, alert delivery, real user traffic, and production network
restriction were not verified. Retrieval custom instruments have no data until
the hosted maintenance-review workflow invokes the decorated retrievers.
Projection and embedding rebuild outcomes remain command/evidence data until
durable job telemetry is designed.

## Related Evidence

- [ADR-003](../decisions/ADR-003-prometheus-grafana-observability.md)
- [TEST-002](../test-runs/TEST-002-observability-baseline.md)
