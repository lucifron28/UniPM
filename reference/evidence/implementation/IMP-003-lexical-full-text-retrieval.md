---
id: IMP-003
type: implementation
title: SQL Server lexical Full-Text retrieval
status: reviewed
recordedAtUtc: 2026-07-12T01:10:00Z
sourceBranch: main
evidenceLevel: source-inspected
---

# Lexical Full-Text Retrieval

## Objective

Provide an internal SQL Server Full-Text Search channel over the persisted
`MaintenanceSearchDocument` projection with structured filters and traceable
inspection results.

## Source Identity

- Source branch: `main` at merge commit `00e5401`.
- Relevant commits: `a26298b`, `5ab301b`, `62daea7`, `fbf3c28`, `3e1a31e`,
  `087144c`, `3ea72df`, and `3d378a1`.
- Implementation date from Git history: 2026-07-11.
- Source paths: `server/Features/Retrieval/`, SQL migrations under
  `server/Migrations/`, and SQL Server retrieval tests.

## Implementation Summary

The repository contains a persisted one-document-per-inspection projection,
deterministic SearchText construction, a dedicated SQL Server Full-Text
catalog/index migration, bounded lexical query construction, metadata/date
filters, provider checks, and source-traceable result DTOs.

## Architecture And Contracts

The lexical retriever searches only `MaintenanceSearchDocument.SearchText` and
does not expose a public maintenance-review endpoint. SQL Server Full-Text
Search is paired with structured relational filters; the issue lexicon and
benchmark remain separate concerns.

## Important Files

- `server/Features/Retrieval/MaintenanceSearchDocumentProjector.cs`
- `server/Features/Retrieval/SqlServerLexicalMaintenanceRetriever.cs`
- `server/Features/Retrieval/LexicalMaintenanceQueryBuilder.cs`
- `server/Migrations/20260711120000_AddMaintenanceFullTextSearch.cs`
- `tests/UniPM.Api.Tests/Retrieval/SqlServerLexicalMaintenanceRetrieverTests.cs`

## Database Changes

The source includes a dedicated SQL Server Full-Text catalog and SearchText
index migration. No separate search platform or vector database is introduced.

## Tests Present

The source tree contains provider, query-builder, migration, filtering,
ordering, multilingual, change-tracking, and asynchronous Full-Text polling
tests. Historical execution is not claimed here.

## Verification Status

Source-inspected historical record. A current SQL Server execution is captured
in `TEST-001-current-backend-baseline.md` and the lexical experiment record.

## Known Limitations

Results depend on SQL Server Full-Text tokenization and the documented
vocabulary. Multilingual quality and production GSD performance are not proved
by implementation inspection or a synthetic benchmark.

## Related Evidence

- [ADR-001](../decisions/ADR-001-sql-server-full-text-retrieval.md)
- [IMP-002](IMP-002-maintenance-issue-lexicon.md)
- [EXP-001](../experiments/EXP-001-lexical-fts-baseline.md)
