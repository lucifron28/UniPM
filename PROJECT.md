# UniPM Project Memory

## Commands
- **Build**: `dotnet build .\UniPM.slnx`
- **Test**: `dotnet test .\UniPM.slnx --no-build` (or `dotnet test`)
- **Docker Up**: `docker compose up --build -d`
- **Start Database**: `docker compose up -d unipm-db`
- **Start API**: `docker compose up -d unipm-api`
- **Docker Down**: `docker compose down`
- **Migration Add**: `dotnet ef migrations add <Name> --project server`
- **Migration Update**: `$env:ConnectionStrings__DefaultConnection="<String>"; dotnet ef database update --project server`

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
  - Reset dependency protection, strict fixture-property loading, exact
    evaluation correspondence tests, case-insensitive uniqueness checks, and
    unambiguous maintenance-command handling.

## Synthetic Seed Commands

Run seed/reset only with `ASPNETCORE_ENVIRONMENT=Development`; the rebuild
command requires a configured, reachable database:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project server -- --seed-synthetic
dotnet run --project server -- --reset-synthetic-seed
dotnet run --project server -- --rebuild-maintenance-search-documents
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

## Next Steps

1. Implement lexical retrieval with SQL Server FTS.
2. Add a semantic retriever behind `IEmbeddingService`.
3. Build the retrieval benchmark.
4. Add result fusion.
5. Add sanitizer and source-bounded maintenance review.
6. Add authentication scaffolding.
