# UniPM

UniPM is a web and mobile preventive-maintenance system for the university
General Services Department. The repository contains an ASP.NET Core API and
the initial preventive-maintenance domain, with a bounded maintenance-history
review feature planned for a later phase.

## Local Foundation

The local stack runs:

- `unipm-api`: ASP.NET Core API on `http://localhost:5000`
- `unipm-db`: SQL Server 2025 Developer Edition with Full-Text Search
- `unipm-db-init`: one-shot bootstrap that creates an empty `UniPMDb`

SQL Server 2025 is used so the later bounded retrieval feature can use
Full-Text Search plus semantic similarity. The database already contains the
initial `Asset`, `PreventiveMaintenanceSchedule`, and `InspectionRecord`
schema. The backend now contains a versioned deterministic maintenance issue
lexicon. It does not yet contain `MaintenanceSearchDocument`, FTS query logic,
embeddings, semantic retrieval, result fusion/RRF, source-bounded summaries, or
a maintenance-review endpoint.

## Current API Surface

The backend currently provides:

- asset creation, list, detail, and QR lookup;
- schedule creation, list, and detail;
- inspection submission, list, detail, and asset-history lookup;
- reference-data categories, validation/error contracts, health checks, tests,
  and backend CI.


## First Run

Create a local environment file:

```powershell
Copy-Item .env.example .env
```

Update the local password in `.env`, then start the stack:

```powershell
docker compose up --build -d
```

Check the API:

```powershell
Invoke-WebRequest -UseBasicParsing http://localhost:5000/
Invoke-WebRequest -UseBasicParsing http://localhost:5000/health/live
Invoke-WebRequest -UseBasicParsing http://localhost:5000/health/ready
Invoke-WebRequest -UseBasicParsing http://localhost:5000/openapi/v1.json
```

Stop containers while preserving the SQL Server volume:

```powershell
docker compose down
```

## Build And Test

```powershell
dotnet build .\UniPM.slnx
dotnet test .\UniPM.slnx --no-build
```

## Synthetic Development Data

The fixture is entirely fictional, represents no actual GSD maintenance history,
and is not a final production import contract. It is based only on visible Page
1 blank forms and will be revised after Page 2 forms and official completed
samples become available.

With `ASPNETCORE_ENVIRONMENT=Development` and a reachable configured database:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project server -- --seed-synthetic
dotnet run --project server -- --reset-synthetic-seed
```

`--seed-synthetic` deterministically upserts 20 fixture assets, 34 schedules,
and 30 inspections. `--reset-synthetic-seed` removes only records whose IDs
belong to the fixture, in inspection, schedule, then asset order. Reset refuses
to continue if unrelated records depend on fixture-owned assets or schedules.
Neither command runs during normal API startup, and both fail outside
Development. Supplying both command flags is rejected without starting HTTP
hosting or executing a seed operation.

The fixture uses five deterministic synthetic actor IDs for assignee and
inspector references. When development users are introduced later, reuse those
IDs rather than creating a temporary production user table solely for seeding.

The operational fixture is version `1.1.0`. The retrieval evaluation manifest
is version `1.0.0`, is copied only to test output, and remains test-only: it is
not loaded by the API, persisted, indexed, embedded, included in prompts, or
returned by ordinary DTOs. Both files are fictional and based only on visible
Page 1 blank-form fields; Page 2, completed samples, acknowledgement, and RMRF
rules remain provisional.

Inspection list/detail reads and maintenance issue normalization are complete.
The next backend task is the search-document projection, followed by separate
lexical and semantic retrieval, benchmark, fusion, and source-bounded review.

## Project References

- [`AGENTS.md`](AGENTS.md)
- [`PROJECT.md`](PROJECT.md)
- [`reference/planning/current-priorities.md`](reference/planning/current-priorities.md)
