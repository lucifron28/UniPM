---
id: ADR-002
type: decision
title: Keep semantic embeddings behind a provider-neutral service
status: reviewed
recordedAtUtc: 2026-07-12T01:10:00Z
sourceBranch: main
evidenceLevel: source-inspected
---

# Provider-Neutral Embeddings

## Status

Reviewed architecture decision recorded from the implemented source and target
RAG constraints. No real-provider performance claim is made.

## Context

Semantic retrieval is a required target channel for bounded maintenance-history
review, but provider availability, cost, privacy, and model choice are not
stable enough to hardcode into core workflows.

## Decision

Keep embedding generation behind `IEmbeddingService`. Use the current
provider-neutral OpenAI-compatible adapter, persist document embeddings with
profile/source validation, generate query embeddings transiently, and rank
eligible SQL Server candidates with application-layer cosine similarity.

## Alternatives

A provider-specific application contract, native separate vector database, or
embedding calls from clients were not adopted. The decision records boundaries
and constraints, not a measured superiority result.

## Consequences

Providers and models can be changed behind one service contract. Embedding
rebuilds, dimension/profile checks, cache invalidation, and temporary query
vectors add operational work. Semantic retrieval can degrade operationally
without blocking core maintenance workflows.

## Security And Privacy

Provider credentials remain backend-only. Query vectors and external request
payloads are not persisted in the retrieval path. Any future external-provider
call still requires the project sanitization and privacy controls.

## Operational Impact

Real-provider configuration is required for model-quality evaluation. A
deterministic test provider can validate database/indexer/retriever orchestration
only. Cost limits, provider health, and production observability remain future
work.

## Implementation References

- `server/Features/Retrieval/EmbeddingContracts.cs`
- `server/Features/Retrieval/OpenAiCompatibleEmbeddingService.cs`
- `server/Features/Retrieval/MaintenanceSearchDocumentEmbeddingIndexer.cs`
- `server/Features/Retrieval/SqlServerSemanticMaintenanceRetriever.cs`
- `server/Migrations/20260711123548_AddMaintenanceSearchDocumentEmbeddings.cs`

## Evidence References

- [IMP-004](../implementation/IMP-004-semantic-retrieval.md)
- [TEST-001](../test-runs/TEST-001-current-backend-baseline.md)
