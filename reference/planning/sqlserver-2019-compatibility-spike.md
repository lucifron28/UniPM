# SQL Server 2019 Compatibility Spike

## Scope

This spike evaluates a side-by-side SQL Server 2019 (major version 15)
environment without changing the default SQL Server 2025 Compose stack or its
volume. It does not retarget the production baseline.

## Environment

`docker-compose.sqlserver2019.yml` defines the isolated services:

- `unipm-db-2019` on host port `14339` by default;
- `unipm-db-2019-init` to create `UniPMDb` and set compatibility level `150`;
- `unipm-sqlserver-2019-data`, separate from the 2025 data volume;
- `unipm-sqlserver-2019-network`, separate from the default network.

The 2019 Dockerfile uses the official `2019-latest` image and the Microsoft
Ubuntu 20.04 SQL Server 2019 repository to install `mssql-server-fts`. The
image build verifies that package installation and the service health check
requires `SERVERPROPERTY('IsFullTextInstalled') = 1`.

## EF And Migration Boundary

All UniPM runtime, design-time, benchmark, and SQL Server integration-test
contexts use `SqlServerCompatibility.UseUniPmSqlServer`. It explicitly selects
EF SQL Server compatibility level `150`.

The historical domain-contract migration no longer uses the SQL Server 2022
three-argument `STRING_SPLIT`. It normalizes CRLF and CR to LF, serializes
fragments as XML nodes in their source order, trims nonempty nodes, and joins
them with one space using ordered `STRING_AGG`. `FOR XML` escapes source text,
so the ordered transformation remains safe for ordinary identifier content.

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

Blocked pending a successful local pull/build of the official SQL Server 2019
image. This document does not claim SQL Server 2019 support. The default
SQL Server 2025 stack remains the project baseline.
