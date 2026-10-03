---
id: IMP-040
type: implementation
title: Maintenance-history RAG retirement in the PMIS validation branch
status: reviewed
recordedAtUtc: 2026-10-02T18:41:22Z
sourceCommit: f2a1eae2a80bbd5200102da613fb731210457a85
sourceBranch: refactor/retire-maintenance-history-rag
evidenceLevel: source-inspected
---

# Maintenance-history RAG retirement in the PMIS validation branch

## Objective

Keep the GSD validation branch focused on the confirmed preventive-maintenance
workflow while retiring the maintenance-history RAG capability from active
runtime code. Preserve the separate fictional ReferenceDocument foundation and
historical engineering evidence.

## Source identity

- Inspected source commit: `f2a1eae2a80bbd5200102da613fb731210457a85`.
- Relevant branch commits include `24248db` (retirement documentation),
  `601381a` (incorporate the merged CPMP baseline), `b10e3cc` (current-schema
  SQL test fixtures), and `f2a1eae` (acknowledgement fixture compile correction).
- Source paths include `server/Program.cs`,
  `server/Features/ApiEndpointRouteBuilderExtensions.cs`,
  `server/Data/ApplicationDbContext.cs`,
  `server/Features/ReferenceDocuments/`, `server/Models/ReferenceDocument*.cs`,
  `server/Retrieval/Lexical/`, `server/Retrieval/Semantic/`, and
  `server/Retrieval/Embeddings/`.

## Implementation summary

The active API route map contains authentication, reference data, assets,
schedules, inspections, preventive-maintenance forms, and the PM-period
dashboard. It does not register the retired maintenance-review route. Active
server source contains no maintenance-review configuration, summary integration,
maintenance-search projection, retrieval/fusion service, or rebuild command.
The maintenance-specific evaluation runners are not part of the active source
tree.

Historical migrations, API descriptions, ADRs, experiments, and verification
records remain as evidence of earlier work. Their presence does not make the
retired capability available in the current API or application model.

The separate ReferenceDocument foundation remains in the current model and
service graph. It retains fictional document metadata, applicability, ordered
sections, section embeddings, SQL Server Full-Text Search, a semantic reference
retriever, and the shared provider-neutral `IEmbeddingService`. This record does
not authorize institutional document ingestion or exposure.

Natural-language analytics is a planned post-validation direction. It is not
implemented or approved; any proposal requires a separate approved task after
GSD validation.

## Architecture and contracts

Core PMIS workflows do not require an AI provider. The retired maintenance-review
route is absent from the active route map, including when legacy feature flags
are set. The ReferenceDocument retrieval and embedding components are a separate
capability and are not maintenance-history analysis.

## Important files

- `server/Features/ApiEndpointRouteBuilderExtensions.cs` maps the current API
  groups and contains no maintenance-review mapping.
- `server/Program.cs` retains shared reference embedding registrations without
  registering a maintenance-summary service.
- `server/Data/ApplicationDbContext.cs` retains ReferenceDocument entities and
  section-embedding storage; active entities do not include maintenance-search
  projections.
- `server/Retrieval/Lexical/` and `server/Retrieval/Semantic/` retain retrieval
  for institutional reference sections.
- `reference/evidence/` preserves historical records, including earlier
  maintenance-history RAG implementation and evaluation evidence.

## Database changes

Migration 20260930123820_RetireMaintenanceHistoryRagStorage removes the
maintenance-only UniPMMaintenanceRetrieval Full-Text index and catalog, then
drops MaintenanceSearchDocumentEmbeddings before MaintenanceSearchDocuments.
Its Down path recreates the derived tables, indexes, and Full-Text catalog/index
with no rows. Earlier migration files remain unchanged. The populated-data
upgrade, rollback, and reapply preservation rehearsal was not completed; see
TEST-049.

## Tests present

ReferenceDocument foundation and SQL Server tests remain in the repository.
TEST-049 records the current backend and web test executions. Historical RAG
test results and evidence remain, but this record does not treat them as active
feature verification.

## Verification status

Source inspection at the exact commit above confirms the active route and model
boundary. Runtime HTTP health, route, and OpenAPI assertions were not completed
in the local smoke harness. See TEST-049 for execution results and limitations.

## Known limitations

- GSD validation and institutional requirements collection remain pending.
- Natural-language analytics has not been implemented or approved.
- Populated-data retirement migration up/down/reapply preservation remains
  unverified.
- No claim is made about real-provider quality, IIS deployment, or production
  readiness.

## Related evidence

- [TEST-049 — Maintenance-history RAG retirement and PMIS verification](../test-runs/TEST-049-maintenance-history-rag-retirement-verification.md)
- [Confirmed GSD workflow](../../planning/confirmed-gsd-workflow.md)
- [MVP definition](../../planning/mvp-definition.md)
