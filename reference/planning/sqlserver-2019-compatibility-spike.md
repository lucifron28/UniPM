# SQL Server 2019 Compatibility Spike

## Scope

This spike evaluates a side-by-side SQL Server 2019 (major version 15)
environment without changing the default SQL Server 2025 Compose stack or its
volume. It does not retarget the production baseline.

## Environment

The Linux Full-Text Search container experiment was removed from this proposed
baseline after its APT package-install layer stalled locally. It remains
documented in TEST-021 as a blocked environment experiment; it is not the
deployment or acceptance path.

The acceptance path is a native Windows SQL Server 2019 Developer instance
with Database Engine Services and Full-Text and Semantic Extractions for Search
installed. The explicit runner accepts only process-scoped connection strings
and does not modify the SQL Server 2025 Docker environment.

## EF And Migration Boundary

All UniPM runtime, design-time, benchmark, and SQL Server integration-test
contexts use `SqlServerCompatibility.UseUniPmSqlServer`. It explicitly selects
EF SQL Server compatibility level `150`.

The historical domain-contract migration no longer uses the SQL Server 2022
three-argument `STRING_SPLIT`. It normalizes CRLF and CR to LF, uses a bounded
SQL tally set to locate each line start in source order, trims nonempty
fragments, and joins them with one space using ordered `STRING_AGG`.

The remaining SQL-specific surface uses SQL Server 2019-supported features:
`STRING_AGG`, Full-Text catalog/index DDL, and `CONTAINSTABLE`. No other SQL
Server 2022/2025-only T-SQL syntax was found in migrations.

## Required Verification

Run only after the isolated image has built and the bootstrap service has
completed:

```powershell
$env:UNIPM_SQLSERVER_TEST_CONNECTION = "<local SQL Server 2019 connection>"
$env:ConnectionStrings__DefaultConnection = "<local SQL Server 2019 connection to UniPMDb>"
$env:ASPNETCORE_ENVIRONMENT = "Development"

dotnet run --project server -- --migrate-database
dotnet run --project server -- --seed-synthetic
dotnet run --project server -- --seed-development-users
dotnet run --project server -- --rebuild-maintenance-search-documents
dotnet test .\UniPM.slnx -c Release --no-build
```

Then confirm major version 15, compatibility level 150, Full-Text installation,
the `UniPMMaintenanceRetrieval` catalog, and the enabled search-document index.
Run the deterministic semantic and fused SQL suites to prove persistence,
stale-embedding rejection, app-layer cosine, and degradation orchestration.

## Current Result

**EXECUTED (development compatibility evidence)** on a native Windows SQL
Server 2019 Developer instance with Full-Text Search. Major version 15,
compatibility level 150, migration, seed, projection rebuild, Full-Text
catalog/index, `CONTAINSTABLE`, and the 310-pass SQL-enabled solution suite
completed successfully. See TEST-022 for the exact tested commit and bounded
claims.

This does not retarget production, establish workload fitness, or replace SQL
Server 2025 as the project baseline.
