---
id: ADR-013
type: decision
title: Adopt SQL Server 2019 as the minimum supported database platform
status: reviewed
recordedAtUtc: 2026-07-25T00:00:00Z
sourceBranch: refactor/database-sqlserver-2019-baseline
evidenceLevel: source-inspected
---

# Adopt SQL Server 2019 As The Minimum Supported Database Platform

## Status

Accepted. This decision retargets configuration and documentation after the
native Windows SQL Server 2019 compatibility execution in TEST-022. It does not
claim IIS production deployment readiness.

## Context

UniPM originally retained a SQL Server 2025 Docker development stack while its
database and retrieval implementation used SQL Server Full-Text Search,
serialized embeddings, bounded SQL filtering, application-side cosine
similarity, and Reciprocal Rank Fusion. TEST-022 demonstrated that the current
migrations, FTS retrieval, deterministic semantic path, and fusion path execute
on native Windows SQL Server 2019 at compatibility level `150` with Full-Text
Search installed.

## Decision

SQL Server 2019 is the minimum supported UniPM database platform. UniPM uses
SQL Server Full-Text Search for lexical retrieval and stores versioned serialized
embedding vectors alongside relational source metadata. The backend retrieves a
bounded candidate set and calculates semantic similarity in application memory.
Native SQL Server vector features and a separate vector database are not
required.

The primary local-development path uses a native Windows SQL Server 2019
instance with Full-Text Search and process-scoped configuration. The target
deployment remains ASP.NET Core hosted through IIS with native Windows SQL
Server. Docker remains optional development tooling; the former SQL Server 2025
Compose stack is retained only as an explicitly named legacy development
experiment.

## Alternatives

- Continue treating SQL Server 2025 Docker Compose as the default path: rejected
  because TEST-022 established the lower native Windows baseline and Docker is
  not the target deployment architecture.
- Require native SQL Server vector features: rejected because the implemented
  bounded application-side cosine path works at compatibility level `150`.
- Add a separate vector database: rejected by the single relational source-of-
  truth and operational-simplicity constraints.

## Consequences

Documentation, examples, and scripts must use the native SQL Server 2019 path
by default. Compatibility level `150` remains centralized in
`SqlServerCompatibility`; no duplicate provider configuration is introduced.
Full-Text Search remains a required database feature for lexical and hybrid
retrieval. The optional SQL Server 2025 Compose configuration retains its
existing volume and is never a migration or backup path to SQL Server 2019.

## Security And Privacy

Connection strings, passwords, API keys, prompts, token maps, and vectors remain
outside committed configuration and evidence. Process-scoped configuration is
the documented local-development practice.

## Operational Impact

Native Windows SQL Server 2019 with Database Engine Services and Full-Text
Search is required for the supported local path. This decision does not select a
production SQL Server edition, verify IIS deployment, establish capacity or
backup policy, or prove real embedding-model quality.

## Implementation References

- Merged PR #35, SQL Server 2019 compatibility spike
- `server/Data/SqlServerCompatibility.cs`
- `scripts/evidence/Invoke-SqlServer2019CompatibilityVerification.ps1`
- `reference/planning/manuscript-platform-baseline.md`

## Evidence References

- [TEST-021](../test-runs/TEST-021-sql-server-2019-compatibility-spike.md)
  preserves the blocked Docker experiment.
- [TEST-022](../test-runs/TEST-022-native-sql-server-2019-compatibility.md)
  verifies native Windows SQL Server 2019 compatibility.
