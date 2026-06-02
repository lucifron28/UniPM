# UniPM

UniPM is a web and mobile preventive-maintenance system for the university
General Services Department. The current repository contains the verified
Docker-first API foundation only. Business tables and workflow migrations are
intentionally deferred until the remaining GSD clarifications are complete.

## Local Foundation

The local stack runs:

- `unipm-api`: ASP.NET Core API on `http://localhost:5000`
- `unipm-db`: SQL Server 2025 Developer Edition with Full-Text Search
- `unipm-db-init`: one-shot bootstrap that creates an empty `UniPMDb`

SQL Server 2025 is used so the later bounded RAG feature can use hybrid
retrieval: Full-Text Search plus native vector similarity. The current
foundation does not create embeddings, vector indexes, or RAG tables yet.

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

## Planning References

- [`docs/UniPM-Planning-Tracker.md`](docs/UniPM-Planning-Tracker.md)
- [`docs/context/UniPM-Codex-Backend-Context-DockerFirst.md`](docs/context/UniPM-Codex-Backend-Context-DockerFirst.md)
- [`docs/reference/README.md`](docs/reference/README.md)
