---
id: ADR-003
type: decision
title: Use optional OpenTelemetry, Prometheus, and Grafana for technical health metrics
status: reviewed
recordedAtUtc: 2026-07-12T05:27:26.3049014+00:00
testedCommit: c4fc0dfe1cf21e959ffaecae819373d4ad8db3fa
sourceBranch: feat/observability-metrics
evidenceLevel: source-inspected
---

# Prometheus And Grafana Observability

## Status

Accepted for the current local technical-health scope.

## Context

UniPM needs inspectable operational evidence before retrieval fusion, but the
backend must remain usable without a monitoring stack and deployable to IIS.
The project does not yet need tracing, centralized logs, alert delivery, or
maintenance-domain KPI dashboards.

## Decision

Use the .NET built-in metrics API and OpenTelemetry metrics in the API, expose
an opt-in `/metrics` scrape endpoint, and provide optional local Prometheus and
Grafana services under the Compose `observability` profile.

Prometheus uses the pinned `prom/prometheus:v3.5.0` image and scrapes the stable
`unipm-api:8080/metrics` target every 15 seconds. Grafana uses the pinned
`grafana/grafana:12.0.2` image, provisions datasource UID
`unipm-prometheus`, and loads dashboard UID `unipm-system-health`.

The API custom meter is `UniPM.Api`. Hosted retrieval instrumentation uses only
the low-cardinality `channel` and `outcome` dimensions. Query text, remarks,
recommendations, asset and inspection identifiers, user data, exception
details, provider settings, and vectors are excluded. Projection and embedding
rebuild commands remain observable through command output and evidence records;
they do not emit Prometheus metrics because those processes exit before a
long-running scrape endpoint can observe them.

## Alternatives

- Prometheus and Grafana were selected for a conventional local scrape and
  dashboard workflow that can be inspected from version-controlled files.
- OTLP and a collector were deferred because this branch needs only a local
  Prometheus-compatible scrape endpoint.
- Loki, Tempo, Alertmanager, vendor SDKs, and exporter infrastructure were
  deferred because centralized logs, tracing, and alert delivery are outside
  the current scope.
- Maintenance KPI dashboards remain a future React web concern rather than
  infrastructure dashboard content.

## Consequences

The API gains a technical metrics surface without making monitoring services
mandatory. The endpoint is disabled by default and normal Compose startup is
unchanged. Developers can opt into the profile and inspect the dashboard
without modifying application behavior.

The Prometheus ASP.NET Core exporter is pinned to prerelease
`1.16.0-beta.1` because that is the available exporter line compatible with
the pinned OpenTelemetry `1.16.0` packages. The exporter may change before a
stable release, so package upgrades require a focused compatibility review.

## Security And Privacy

The endpoint is not an authentication design. IIS deployments should enable it
only when network or reverse-proxy policy restricts access. The local Grafana
password in `.env.example` is a development placeholder and must be changed.
No credentials, connection strings, provider endpoints, prompts, or personal
data are emitted into metrics or committed provisioning files.

## Operational Impact

When Prometheus and Grafana are absent, the API, health checks, migrations,
seed/reset commands, projection rebuild, and embedding rebuild remain
available. Failed monitoring services do not block preventive-maintenance
operations. The local verification script uses bounded polling, including the
API readiness check, captures sanitized responses and hashes, stops the stack
in `finally`, and preserves volumes unless cleanup is explicitly requested.

## Implementation References

- `server/Program.cs`
- `server/Observability/ObservabilityOptions.cs`
- `server/Observability/UniPMMetrics.cs`
- `docker-compose.yml`
- `observability/`
- `scripts/evidence/Invoke-ObservabilityVerification.ps1`

## Evidence References

- [IMP-006](../implementation/IMP-006-observability-metrics.md)
- [TEST-002](../test-runs/TEST-002-observability-baseline.md)
