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
  - Inspection submission and asset-history endpoint.
  - Reference-data categories, validation contracts, health checks, backend tests,
    and CI.

## Retrieval Architecture Rule

Semantic retrieval is a required target channel of the UniPM
maintenance-history review feature, but it is operationally degradable. If
embeddings are unavailable, maintenance review may use SQL, lexicon
normalization, and FTS fallback while explicitly reporting lexical fallback.
Core preventive-maintenance workflows must never depend on embeddings or an
LLM being available.

## Next Steps

1. Add the synthetic fixture and development-only seeder.
2. Complete inspection list/detail endpoints.
3. Implement the maintenance issue lexicon.
4. Add `MaintenanceSearchDocument` projection.
5. Implement the SQL Server FTS retriever.
6. Add a semantic retriever behind `IEmbeddingService`.
7. Build the retrieval benchmark.
8. Add result fusion.
9. Add sanitizer and source-bounded maintenance review.
