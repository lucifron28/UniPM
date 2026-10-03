# Run the local stack

Use this guide for a repeatable PMIS database setup and fictional maintenance data.
Maintenance-history RAG and both maintenance rebuild commands are retired.
The ReferenceDocument foundation and shared embedding components remain.

## Use the Supported Database Baseline

The primary path is a native Windows SQL Server 2019 instance with Database
Engine Services, Full-Text Search, and compatibility level `150`.

Set the application connection string in the current process:

```powershell
$env:ConnectionStrings__DefaultConnection =
  "Server=.;Database=UniPMDb;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;"
$env:ASPNETCORE_ENVIRONMENT = "Development"
```

Use `Server=localhost\INSTANCE_NAME` for a named instance. Confirm the platform
before migration:

```sql
SELECT
    SERVERPROPERTY('ProductMajorVersion') AS ProductMajorVersion,
    SERVERPROPERTY('IsFullTextInstalled') AS IsFullTextInstalled;

SELECT compatibility_level
FROM sys.databases
WHERE name = N'UniPMDb';
```

The expected values are `15`, `1`, and `150` respectively.

## Apply Migrations and Seed Fictional Records

Run these commands from the repository root:

```powershell
dotnet ef database update --project server
dotnet run --project server -- --seed-synthetic
dotnet run --project server -- --seed-development-users
```

The commands are explicit and do not run automatically during normal API
startup. Synthetic seeding is Development-only and owns only its deterministic
fixture records. It creates 20 assets, 34 schedules, and 30 inspections.

Seeding no longer creates maintenance search documents or embeddings.
Applying `RetireMaintenanceHistoryRagStorage` removes that derived storage
while preserving PM records and reference-document storage.

## Start and Check the API

Start the API in a separate terminal:

```powershell
dotnet run --project server
```

Check the health endpoint exposed by the current API configuration, then use
the authenticated API routes described in the [capability reference](../reference/system-capabilities.md).
The historical maintenance-review contract is not published by the current
runtime.

## Reset Fictional Records

To remove only the deterministic synthetic fixture:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project server -- --reset-synthetic-seed
```

Reset protects unrelated records and refuses to delete fixture data when
unrelated dependents would make the operation unsafe. Never use a destructive
database command to simulate a fixture reset.

## Optional Legacy Docker Experiment

The retained SQL Server 2025 Compose stack is optional historical development
tooling. It is not the verified database baseline, is not required to run
UniPM, and is not part of the IIS target deployment.

If that experiment is intentionally needed, copy the example environment file
and pass it explicitly:

```powershell
Copy-Item .env.sqlserver2025.example .env.sqlserver2025
docker compose --env-file .env.sqlserver2025 -f docker-compose.sqlserver2025.yml config
```

Do not reuse a SQL Server 2019 data volume with the SQL Server 2025 experiment,
or start, remove, or migrate a local volume that is not explicitly part of the
experiment.

## Common Boundaries

- Core form workflows do not depend on embeddings or an LLM.
- Draft and Submitted form rows are excluded from official history. Acknowledged
  rows are eligible for official history; maintenance RAG has been retired.
- Signature and signatory fields never enter retrieval documents, embeddings,
  prompts, or corrective-handoff responses.
- The RAG-assisted inspection-history analysis proposal is historical, was never
  implemented, and is no longer active. Schema-constrained natural-language
  analytics is a separate planned post-validation direction pending
  professor/adviser confirmation; it is not implemented or approved and
  requires a separate approved task after GSD validation. The historical
  maintenance-review contract is not published by the current runtime.
- IIS production deployment, final RBAC, audit persistence, institutional source
  authorization, and offline-sync architecture remain unverified or deferred.
