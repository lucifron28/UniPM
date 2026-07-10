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
schema. It does not yet contain a maintenance issue lexicon,
`MaintenanceSearchDocument`, FTS query logic, embeddings, semantic retrieval,
result fusion/RRF, source-bounded summaries, or a maintenance-review endpoint.

## Current API Surface

The backend currently provides:

- asset creation, list, detail, and QR lookup;
- schedule creation, list, and detail;
- inspection submission and asset-history lookup;
- reference-data categories, validation/error contracts, health checks, tests,
  and backend CI.

Inspection list and inspection detail endpoints are still pending.

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

The synthetic maintenance fixture and its development-only seed commands are
documented after they are implemented. The fixture is fictional, represents no
actual GSD maintenance history, and is not a final production import contract.
It is based only on visible Page 1 blank forms and will be revised after Page 2
forms and official completed samples become available.

## Project References

- [`AGENTS.md`](AGENTS.md)
- [`PROJECT.md`](PROJECT.md)
- [`reference/planning/current-priorities.md`](reference/planning/current-priorities.md)
