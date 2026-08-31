# UniPM Project Memory

## Commands
- **Build**: `dotnet build .\UniPM.slnx`
- **Test**: `dotnet test .\UniPM.slnx --no-build` (or `dotnet test`)
- **Native database baseline**: SQL Server 2019, Full-Text Search, compatibility level `150`.
- **Migration Update**: set a process-only Windows Authentication
  `ConnectionStrings__DefaultConnection`, run `dotnet ef database update --project server`. The
  `--rebuild-maintenance-search-documents` / `--rebuild-maintenance-embeddings`
  commands belong to the preserved inactive retrieval tooling and are not
  needed for ordinary PMIS operation.
- **Optional legacy Docker 2025 experiment**: `docker compose --env-file .env.sqlserver2025 -f docker-compose.sqlserver2025.yml up --build -d`
- **Optional legacy Docker stop**: `docker compose --env-file .env.sqlserver2025 -f docker-compose.sqlserver2025.yml down`

## Active Context
- **Proposed architecture**: ASP.NET Core API hosted on IIS + native Windows
  SQL Server 2019 with Full-Text Search. Docker is optional development
  tooling only. IIS deployment is not part of the evaluated capstone result.
- **Core Entities**: `Asset`, `PreventiveMaintenanceSchedule`,
  `InspectionRecord`, `PreventiveMaintenanceForm`, and
  `PreventiveMaintenanceAcknowledgement` are migrated.
- **Completed**:
  - Native SQL Server 2019 compatibility verification with Full-Text Search and
    compatibility level `150`; see TEST-022.
  - Initial `InitialDomainSchema` migration.
  - Asset create, list, detail, and QR lookup endpoints.
  - Schedule create, list, and detail endpoints.
  - Inspection list, detail, and acknowledged asset-history read endpoints.
    The standalone submission endpoint was removed by the official-inspection-
    boundary refactor; inspection-row creation and editing occur only through
    Draft preventive-maintenance forms.
  - Versioned maintenance issue lexicon with deterministic multilingual
    normalization and category-bounded matching.
  - Rebuildable `MaintenanceSearchDocument` projection with deterministic
    normalized issue keys, source traceability, and explicit refresh commands.
  - Domain-contract catalogs, canonical code storage, SQL Server constraints,
    filtered QR uniqueness, and ordered migration preflight checks.
  - Reference-data categories, validation contracts, health checks, backend tests,
    and CI.
  - Fictional synthetic maintenance fixture, retrieval evaluation manifest, and
    Development-only seed/reset commands.
  - Internal SQL Server Full-Text Search over `MaintenanceSearchDocument.SearchText`
    with bounded prefix-query construction, controlled filters, and source-
    traceable lexical results.
  - Semantic retrieval over a one-to-one SQL Server embedding cache for
    `MaintenanceSearchDocument`, with explicit batch rebuilds and bounded
    application-layer cosine similarity. The embedding provider is optional and
    degradable; query embeddings are never persisted.
  - Reset dependency protection, strict fixture-property loading, exact
    evaluation correspondence tests, case-insensitive uniqueness checks, and
    unambiguous maintenance-command handling.
  - IdentityCore persistence with Guid users and roles, JWT access tokens,
    refresh-session rotation, Development user seeding, and provisional
    operational authorization policies.
  - React web foundation and browser authentication with memory-only access
    tokens, refresh-cookie restoration, protected routes, current-user display,
    and logout.
  - Browser authentication integration is implemented and merged. The Flutter
    mobile foundation, Draft preventive-maintenance form workflow, and
    whole-form submission are implemented and merged in a separate
    partner-owned workstream; offline synchronization remains deferred and
    architecture-undecided.
  - The partner-owned mobile client currently supports authenticated
    GSD/Inspector access, QR-based asset entry, Draft form creation, Draft
    inspection-row add/update/delete operations, and whole-form submission.
    Web acknowledgement and signature capture are implemented separately;
    later mobile field workflows remain separately approved work.
  - Authenticated asset registry and preventive maintenance schedule modules
    with route-backed list/detail/create workflows, generated API contracts,
    runtime response validation, and backend-authoritative role policies.
  - Confirmed multi-asset preventive-maintenance form lifecycle:
    `Draft -> Submitted -> Acknowledged`, provisional form file numbers,
    whole-form acknowledgement, schedule completion after acknowledgement,
    acknowledged-only history/retrieval publication, and GSD-only
    corrective-action handoff preparation.
  - Reference-document foundation is implemented and merged as a fictional,
    source-traceable metadata and sectioning foundation. Approved institutional
    source authorization and ingestion remain pending; OEM retrieval is
    excluded from the evaluated MVP.

## Synthetic Seed Commands

Run seed/reset only with `ASPNETCORE_ENVIRONMENT=Development`; the rebuild
command requires a configured, reachable database:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project server -- --migrate-database
dotnet run --project server -- --seed-synthetic
dotnet run --project server -- --seed-development-users
dotnet run --project server -- --reset-synthetic-seed
# Historical/inactive retrieval tooling - not required for the PMIS validation build:
dotnet run --project server -- --rebuild-maintenance-search-documents
dotnet run --project server -- --rebuild-maintenance-embeddings
```

Seeding deterministically upserts 20 synthetic assets, 34 schedules, and 30
inspections. Reset removes only fixture-owned IDs and preserves unrelated
records, refusing to proceed when unrelated dependent records would block safe
deletion. The fixture is fictional, provisional, and based only on visible
Page 1 blank forms; it is not a production import contract.

The rebuild command refreshes one search document per persisted inspection from
approved operational fields. It is explicit, transactional on SQL Server, and
does not run during normal API startup.

## Retrieval Architecture (Historical/Inactive Infrastructure)

The preserved lexical, semantic, and fused retrieval channels served the
previously evaluated maintenance-history review feature. They remain in the
source tree for history and rollback, but the validation runtime does not
expose them. Core preventive-maintenance workflows must never depend on
embeddings or an LLM being available — on this branch they never contact them.

The lexical channel is implemented as an internal SQL Server Full-Text Search
service over the persisted `MaintenanceSearchDocument.SearchText` projection.
It does not search source entities independently and does not implement
embeddings or benchmark orchestration; fusion and the maintenance-review layer
consume its ranked results separately.

Semantic retrieval was implemented as an internal channel of the evaluated
maintenance-history review workflow. Document embeddings belong to
`MaintenanceSearchDocumentEmbeddings`, are invalidated when `SearchText`
changes, and are regenerated only by the explicit embedding rebuild command.
Query vectors are generated transiently and are never stored. The current MVP
stores versioned serialized embeddings alongside relational document metadata,
filters a bounded SQL candidate set, and uses application-layer cosine
similarity. It requires neither native SQL Server vector features nor a separate
vector database. The embedding provider is disabled by default and remote providers require an
explicit configuration flag and privacy review.

Internal fused retrieval combines the lexical and semantic ranked outputs with
Reciprocal Rank Fusion using K=60. It preserves one-based component ranks and
raw channel values, deduplicates by inspection ID, applies deterministic
tie-breaking, and reports semantic degradation without exposing provider or
query details. Fused retrieval is bounded to a default output of 10 and a
default candidate depth of 20, with a maximum of 100. It has no public endpoint
and does not implement context boosts, thresholds, source selection,
sanitization, or summaries.

## Maintenance Review (Historical/Inactive On This Branch)

The source-bounded maintenance-review loop was implemented as an explicitly
enabled, authenticated endpoint and evaluated as controlled development work.
In the current PMIS-only validation baseline the endpoint is mapped into the
runtime contract only when explicitly enabled, so ordinary GSD workflows
never contact retrieval, embeddings, or a summary provider. Where its sources
remain preserved, behavior is unchanged: at most two fused retrieval passes,
deterministic context tiers, request-scoped sanitization, original source
records beside every summary status, no persistence of review data, prompts,
summaries, or token maps, and no autonomous maintenance decisions.

MVP prompt sanitization is pattern-based token masking and pseudonymization for
email, supported Philippine mobile numbers, and labeled IDs. It does not
generally identify free-text personal names, and synthetic names do not prove
protection for real institutional text. Original source records are returned to
authorized callers for verification; that authorization boundary does not make
the response anonymous. Remote-provider use with real or unscreened
institutional text requires a separately approved privacy process or stronger
sanitization.

The provider-neutral adapter now supports an optional thinking-mode field. A
test-only 12-case English, Tagalog, and Taglish manifest and a secret-safe fresh-
stack runner exist for `deepseek-v4-flash` with thinking disabled. Automated
provider-contract and failure tests pass. EXP-002 executed a real-provider run
using fictional data, retained fictional generated text, and developer-reviewed
ratings. It is experimental only: it does not establish production readiness,
and does not establish real institutional multilingual embedding quality.
EXP-003 executed a local offline Granite baseline against the fictional
maintenance retrieval fixture; it is controlled development evidence only and
does not make Granite a required deployment dependency.

## Validation Baseline Definition

The active boundary for this branch is documented in
[`reference/planning/mvp-definition.md`](reference/planning/mvp-definition.md):
the PMIS-only GSD validation baseline. It covers authentication, the asset
registry with QR lookup, schedules, multi-row preventive-maintenance forms,
the confirmed `Draft -> Submitted -> Acknowledged` lifecycle,
acknowledged-only official history, deterministic reports already implemented,
corrective-handoff preparation, and the web/mobile PMIS workflows. Core PMIS
workflows do not depend on AI; maintenance-review is inactive by default;
retrieval/embedding/summary infrastructure is preserved temporarily for
history and rollback. No replacement innovation is approved yet. The previous
RAG-inclusive evaluated-MVP definition remains preserved in git history and is
summarized as a historical record inside that file.

## Historical Planning Record: RAG-Assisted Inspection-History Analysis

The analysis capability was planned but never implemented. Its design record is
preserved unchanged in
[`reference/planning/rag-assisted-inspection-history-analysis.md`](reference/planning/rag-assisted-inspection-history-analysis.md):
deterministic counts, percentages, recurrence intervals, distributions,
patterns, and timelines computed by SQL and application code, with RAG
retrieving supporting acknowledged records. It is not an active direction on
this branch.

## Next Steps

1. Verify the PMIS-only validation baseline end to end (backend suite, web
   checks, AI-independent startup).
2. Demonstrate the confirmed workflow to GSD.
3. Collect exact form, report, and process requirements from GSD.
4. Decide whether schema-driven protocols, AI report consolidation, analytics,
   or another innovation is justified by the collected requirements.
5. Only then create a separate implementation branch; keep institutional
   source authorization, final RBAC, audit rules, and other unresolved policy
   decisions deferred until then.

## Engineering Evidence

The repository now preserves a reviewed evidence hierarchy under
`reference/evidence/`. Raw local outputs remain ignored under `artifacts/`.
Historical implementation and architecture records are source-inspected, while
fresh test-run records identify exact tested commits and retained artifact
hashes. Retrieval baselines are preserved rather than overwritten. Synthetic
benchmark results do not prove production GSD performance, and deterministic
embedding providers prove orchestration only. This repository now includes
opt-in OpenTelemetry metrics, an optional local Prometheus/Grafana profile, and
TEST-002 evidence for the local technical-health path. Production monitoring,
IIS restriction, tracing, centralized logs, alerting, and maintenance KPI
dashboards remain out of scope. Inspection integrity, retrieval/test
organization, sanitizer-boundary documentation, the web foundation, browser
authentication, asset registry, schedule workflows, read-only inspection review,
and confirmed form workflow foundation are complete. The next client capability
requires explicit approval.

## Manuscript Platform Guidance

Use the repository-controlled wording in
[`reference/planning/manuscript-platform-baseline.md`](reference/planning/manuscript-platform-baseline.md)
when updating the capstone manuscript. It records the accepted SQL Server 2019,
Full-Text Search, serialized-embedding, proposed IIS architecture, and
optional-Docker boundary without claiming that IIS deployment was performed.
