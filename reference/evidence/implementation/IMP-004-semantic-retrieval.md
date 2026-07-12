---
id: IMP-004
type: implementation
title: Provider-neutral semantic retrieval
status: reviewed
recordedAtUtc: 2026-07-12T01:10:00Z
sourceBranch: main
evidenceLevel: source-inspected
---

# Provider-Neutral Semantic Retrieval

## Objective

Provide a provider-neutral semantic retrieval channel with persisted document
embeddings, transient query vectors, bounded candidate selection, and graceful
operational degradation when the provider is unavailable.

## Source Identity

- Source branch: `main` at merge commit `00e5401`.
- Relevant commits: `484db04`, `06ccdfc`, `5962640`, `44eb3eb`, `8049c65`,
  and `a018a2b`.
- Implementation date from Git history: 2026-07-11.
- Source paths: `server/Features/Retrieval/EmbeddingContracts.cs`,
  `OpenAiCompatibleEmbeddingService.cs`, `MaintenanceSearchDocumentEmbeddingIndexer.cs`,
  `SqlServerSemanticMaintenanceRetriever.cs`, and semantic tests.

## Implementation Summary

The source includes `IEmbeddingService`, an OpenAI-compatible provider adapter,
embedding profile and dimension validation, SQL Server embedding persistence,
batch rebuild/invalidation behavior, and application-layer cosine similarity.

## Architecture And Contracts

Semantic retrieval is a required target channel but is operationally optional.
Provider failures must not block core preventive-maintenance workflows. Query
embeddings are transient, and no separate vector database is used.

## Important Files

- `server/Features/Retrieval/EmbeddingContracts.cs`
- `server/Features/Retrieval/OpenAiCompatibleEmbeddingService.cs`
- `server/Features/Retrieval/MaintenanceSearchDocumentEmbeddingIndexer.cs`
- `server/Features/Retrieval/SqlServerSemanticMaintenanceRetriever.cs`
- `server/Migrations/20260711123548_AddMaintenanceSearchDocumentEmbeddings.cs`
- `tests/UniPM.Api.Tests/Retrieval/SqlServerSemanticMaintenanceTests.cs`

## Database Changes

The source includes a SQL Server embedding cache linked to search documents.
Vectors are stored for document retrieval only; query vectors are not persisted.

## Tests Present

The source tree contains deterministic provider tests, SQL persistence and
staleness tests, provider-contract tests, candidate paging tests, and an
optional real-provider smoke test contract.

## Verification Status

Source-inspected historical record. Deterministic provider tests demonstrate
orchestration only. No real embedding provider was configured for TEST-001, so
no semantic model-quality evidence is claimed.

## Known Limitations

Provider availability, model quality, multilingual semantic quality, cost, and
privacy approval remain operational concerns. Semantic retrieval has no public
review endpoint or fusion layer.

## Related Evidence

- [ADR-002](../decisions/ADR-002-provider-neutral-embeddings.md)
- [TEST-001](../test-runs/TEST-001-current-backend-baseline.md)
