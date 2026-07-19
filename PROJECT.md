# UniPM Project Memory

## Commands
- **Build**: `dotnet build .\UniPM.slnx`
- **Test**: `dotnet test .\UniPM.slnx --no-build` (or `dotnet test`)
- **Docker Up**: `docker compose up --build -d`
- **Start Database**: `docker compose up -d unipm-db`
- **Start API**: `docker compose up -d unipm-api`
- **Docker Down**: `docker compose down`
- **Migration Add**: `dotnet ef migrations add <Name> --project server`
- **Migration Update**: set `ConnectionStrings__DefaultConnection`, run `dotnet ef database update --project server`, then run `dotnet run --project server -- --rebuild-maintenance-search-documents` and, when embeddings are enabled, `dotnet run --project server -- --rebuild-maintenance-embeddings`.

## Active Context
- **Architecture**: ASP.NET Core API + SQL Server 2025 (Docker local / IIS prod).
- **Core Entities**: `Asset`, `PreventiveMaintenanceSchedule`, and `InspectionRecord` are migrated.
- **Completed**:
  - Docker environment with SQL Server 2025 + Full-Text Search.
  - Initial `InitialDomainSchema` migration.
  - Asset create, list, detail, and QR lookup endpoints.
  - Schedule create, list, and detail endpoints.
  - Inspection submission, list, detail, and asset-history endpoints.
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

## Synthetic Seed Commands

Run seed/reset only with `ASPNETCORE_ENVIRONMENT=Development`; the rebuild
command requires a configured, reachable database:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project server -- --migrate-database
dotnet run --project server -- --seed-synthetic
dotnet run --project server -- --seed-development-users
dotnet run --project server -- --reset-synthetic-seed
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

## Retrieval Architecture Rule

Semantic retrieval is a required target channel of the UniPM
maintenance-history review feature, but it is operationally degradable. If
embeddings are unavailable, maintenance review may use SQL, lexicon
normalization, and FTS fallback while explicitly reporting lexical fallback.
Core preventive-maintenance workflows must never depend on embeddings or an
LLM being available.

The lexical channel is implemented as an internal SQL Server Full-Text Search
service over the persisted `MaintenanceSearchDocument.SearchText` projection.
It does not search source entities independently and does not implement
embeddings or benchmark orchestration; fusion and the maintenance-review layer
consume its ranked results separately.

Semantic retrieval is implemented as an internal channel required by the target
maintenance-history review workflow. Document embeddings belong to
`MaintenanceSearchDocumentEmbeddings`, are invalidated when `SearchText`
changes, and are regenerated only by the explicit embedding rebuild command.
Query vectors are generated transiently and are never stored. The current MVP
uses application-layer cosine similarity and no separate vector database. The
embedding provider is disabled by default and remote providers require an
explicit configuration flag and privacy review.

Internal fused retrieval combines the lexical and semantic ranked outputs with
Reciprocal Rank Fusion using K=60. It preserves one-based component ranks and
raw channel values, deduplicates by inspection ID, applies deterministic
tie-breaking, and reports semantic degradation without exposing provider or
query details. Fused retrieval is bounded to a default output of 10 and a
default candidate depth of 20, with a maximum of 100. It has no public endpoint
and does not implement context boosts, thresholds, source selection,
sanitization, or summaries.

## Maintenance Review

The source-bounded maintenance-review loop is implemented as an explicitly
enabled, authenticated endpoint. It performs at most two fused retrieval passes,
uses deterministic context tiers, sanitizes provider-bound text in a
request-scoped session, and returns original source records beside every
summary status. It does not persist review data, prompts, summaries, or token
maps and does not make autonomous maintenance decisions.

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
and real multilingual embedding model-quality evidence remains pending.

## Next Steps

1. Completed: browser authentication integration without expanding provisional
   role or workflow rules.
2. Next: add the asset-management web module against the existing API contract.
3. Deferred: run the multilingual embedding baseline when a configured real
   provider is available, without changing established retrieval behavior.

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
organization, sanitizer-boundary documentation, the web foundation, and browser
authentication integration are complete. The exact next branch is
`feat/web-assets`.
