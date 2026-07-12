---
id: ADR-001
type: decision
title: Use SQL Server Full-Text Search for lexical retrieval
status: reviewed
recordedAtUtc: 2026-07-12T01:10:00Z
sourceBranch: main
evidenceLevel: source-inspected
---

# Use SQL Server Full-Text Search For Lexical Retrieval

## Status

Reviewed architecture decision recorded from the implemented source and project
constraints. This record does not claim an empirical comparison of search
platforms.

## Context

UniPM needs lexical maintenance-history retrieval with structured asset,
location, operational-status, and date filters. The target stack already uses
SQL Server for relational records and future retrieval capabilities.

## Decision

Use SQL Server Full-Text Search over the persisted
`MaintenanceSearchDocument.SearchText` projection. Keep structured filters in
SQL queries and return source-traceable inspection metadata through an internal
retriever.

## Alternatives

Separate search platforms and a separate lexical index were considered in the
architecture discussion but are not introduced. This is a constrained stack
decision, not a claim that SQL Server is superior for every workload.

## Consequences

The application keeps one relational source of truth and one rebuildable search
projection. Full-Text catalog/index migrations and asynchronous population must
be operationally checked. Search behavior follows SQL Server tokenization and
the available vocabulary.

## Security And Privacy

Search is bounded to approved operational projection fields. Raw prompts,
provider payloads, secrets, and evaluation labels do not enter the production
search path.

## Operational Impact

SQL Server Full-Text Search must be installed and populated. The benchmark and
integration tests use bounded readiness/content probes. Production monitoring
is deferred to the observability branch.

## Implementation References

- `server/Features/Retrieval/SqlServerLexicalMaintenanceRetriever.cs`
- `server/Features/Retrieval/LexicalMaintenanceQueryBuilder.cs`
- `server/Migrations/20260711120000_AddMaintenanceFullTextSearch.cs`
- `tools/UniPM.RetrievalBenchmark/SqlServerBenchmarkRunner.cs`

## Evidence References

- [IMP-003](../implementation/IMP-003-lexical-full-text-retrieval.md)
- [EXP-001](../experiments/EXP-001-lexical-fts-baseline.md)
