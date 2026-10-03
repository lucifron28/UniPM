# UniPM Project Memory

## Commands
- **Build**: `dotnet build .\UniPM.slnx`
- **Test**: `dotnet test .\UniPM.slnx --no-build` (or `dotnet test`)
- **Native database baseline**: SQL Server 2019, Full-Text Search, compatibility level `150`.
- **Migration Update**: set a process-only Windows Authentication
  `ConnectionStrings__DefaultConnection`, run
  `dotnet ef database update --project server`. Maintenance-history rebuild
  commands have been retired.
- **Optional legacy Docker 2025 experiment**: `docker compose --env-file .env.sqlserver2025 -f docker-compose.sqlserver2025.yml up --build -d`
- **Optional legacy Docker stop**: `docker compose --env-file .env.sqlserver2025 -f docker-compose.sqlserver2025.yml down`

## Active Context

Maintenance-history RAG has been retired. Its review endpoint, summary
provider, maintenance retrieval/fusion, projection, rebuild commands,
benchmark, and experiment runners are removed.

Historical migrations, API descriptions, ADRs, experiments, and verification
records remain as evidence of prior work. The separate fictional
ReferenceDocument foundation, Full-Text Search, section embeddings, and shared
provider-neutral embedding components remain.

Schema-constrained natural-language analytics remains a planned post-validation
direction, pending professor/adviser confirmation. It is not implemented or
approved for this branch; any implementation requires a separate approved task
and branch after GSD validation.

Older completed RAG entries below describe historical work only.

- **Active work**: `refactor/retire-maintenance-history-rag`. Retire the old
  maintenance-history RAG implementation and derived storage while preserving
  the confirmed PMIS validation workflows and ReferenceDocument foundation.
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
  - Historical maintenance retrieval work implemented a versioned issue
    lexicon, a source-traceable search projection, lexical and semantic
    retrieval, and fused ranking. Those runtime and derived-storage components
    were later retired; historical evidence remains.
  - Domain-contract catalogs, canonical code storage, SQL Server constraints,
    filtered QR uniqueness, and ordered migration preflight checks.
  - Reference-data categories, validation contracts, health checks, backend tests,
    and CI.
  - Fictional synthetic maintenance fixture and Development-only seed/reset
    commands.
  - Reset dependency protection, strict fixture-property loading, and
    case-insensitive uniqueness checks for the synthetic fixture.
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
    field-work-driven schedule completion from `InspectionRecord.CompletedAt`,
    and whole-form acknowledgement. Acknowledgement records receipt/noting,
    does not alter execution or compliance timestamps, does not complete
    schedules, and makes completed rows eligible for acknowledged-only official
    history. Preserved retrieval/RAG infrastructure is historical and inactive,
    not actively published. GSD-only corrective-action handoff preparation
    remains available.
  - Reference-document foundation is implemented and merged as a fictional,
    source-traceable metadata and sectioning foundation. Approved institutional
    source authorization and ingestion remain pending; OEM retrieval is
    excluded from the evaluated MVP.

## Synthetic Seed Commands

Run migration, seed, and reset commands only against a configured, reachable
database. Run seed/reset only with `ASPNETCORE_ENVIRONMENT=Development`:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project server -- --migrate-database
dotnet run --project server -- --seed-synthetic
dotnet run --project server -- --seed-development-users
dotnet run --project server -- --reset-synthetic-seed
```

Seeding deterministically upserts 20 synthetic assets, 34 schedules, and 30
inspections. Reset removes only fixture-owned IDs and preserves unrelated
records, refusing to proceed when unrelated dependent records would block safe
deletion. The fixture is fictional, provisional, and based only on visible
Page 1 blank forms; it is not a production import contract.

## Retired Maintenance-History Retrieval (Historical Work)

Lexical, semantic, and fused retrieval were implemented for the earlier
maintenance-history review feature. Their runtime and derived storage have
since been removed from this branch; historical evidence remains. Core
preventive-maintenance workflows do not require embeddings or an LLM.

The lexical channel used an internal SQL Server Full-Text Search service over
the `MaintenanceSearchDocument.SearchText` projection, with bounded prefix
queries and controlled filters. Fusion and the maintenance-review layer
consumed its ranked results separately.

Semantic retrieval was an internal channel of the evaluated review workflow.
It stored versioned serialized embeddings alongside relational document
metadata, filtered a bounded SQL candidate set, and calculated cosine
similarity in application code. Query vectors were transient. The maintenance
embedding cache and its rebuild command have been removed. This historical
implementation required neither native SQL Server vector features nor a
separate vector database.

The internal fused retriever combined lexical and semantic rankings with
Reciprocal Rank Fusion using K=60. It preserved component ranks and channel
values, deduplicated by inspection ID, applied deterministic tie-breaking, and
reported semantic degradation. It had bounded result and candidate limits and
no public endpoint.

## Maintenance Review (Retired Historical Work)

The authenticated maintenance-review endpoint and its supporting services were
implemented and evaluated as controlled development work. They were later
removed from this branch. The historical API description and evaluation records
remain available as evidence. The former review used at most two fused
retrieval passes, deterministic context tiers, request-scoped sanitization,
source records alongside summary status, and no persistence of review data,
prompts, summaries, or token maps. It made no autonomous maintenance decisions.

The retired MVP sanitizer used pattern-based token masking and pseudonymization
for email, supported Philippine mobile numbers, and labeled IDs. It did not
generally identify free-text personal names, and synthetic names did not prove
protection for real institutional text. The former endpoint returned original
source records to authorized callers for verification; that authorization
boundary did not make the response anonymous.

The former provider-neutral adapter supported an optional thinking-mode field.
A test-only 12-case English, Tagalog, and Taglish manifest and a secret-safe
runner were used for `deepseek-v4-flash` with thinking disabled. The associated
provider-contract and failure tests passed during that implementation phase.
EXP-002 recorded a real-provider run with fictional data, generated text, and
developer-reviewed ratings; it did not establish production readiness or real
institutional multilingual embedding quality. EXP-003 recorded a local offline
Granite baseline against the fictional maintenance retrieval fixture. These
experiments remain development evidence only; Granite is not a deployment
dependency.

## Validation Baseline Definition

The active boundary for this branch is documented in
[`reference/planning/mvp-definition.md`](reference/planning/mvp-definition.md):
the PMIS-only GSD validation baseline. It covers authentication, the asset
registry with QR lookup, schedules, multi-row preventive-maintenance forms,
the confirmed `Draft -> Submitted -> Acknowledged` lifecycle,
acknowledged-only official history, deterministic reports already implemented,
corrective-handoff preparation, and the web/mobile PMIS workflows. Core PMIS
workflows do not depend on AI. Maintenance-history RAG, its runtime, tooling,
tests, and derived storage have been retired; historical documentation, evidence,
and migrations remain. No replacement innovation is approved yet. The previous
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

Schema-constrained natural-language analytics remains a planned post-validation
direction, pending professor/adviser confirmation. It is not implemented or
approved for this branch; any implementation requires a separate approved task
and branch after GSD validation.

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
