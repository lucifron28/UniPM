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

SQL Server 2025 is used so the bounded retrieval feature can use Full-Text
Search plus semantic similarity. The database contains the initial `Asset`,
`PreventiveMaintenanceSchedule`, and `InspectionRecord` schema plus the
rebuildable `MaintenanceSearchDocument` projection. The backend contains a
versioned deterministic maintenance issue lexicon and an internal lexical
retriever that searches only `MaintenanceSearchDocument.SearchText` through
SQL Server Full-Text Search. It also contains semantic retrieval
channel using cached document embeddings and bounded application-layer cosine
similarity. Semantic retrieval is a required UniPM retrieval channel, but its
embedding provider is operationally optional and degradable. Result fusion/RRF,
source-bounded summaries, and a maintenance-review endpoint remain separate
future work.

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

## Database Migration

EF database commands use the configured `ConnectionStrings__DefaultConnection`
value and fail when it is missing; they do not fall back to LocalDB:

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=localhost,1433;Database=UniPMDb;User Id=sa;Password=<local-password>;Encrypt=True;TrustServerCertificate=True;"
dotnet ef database update --project server
```

The lexical retrieval migration creates the dedicated SQL Server Full-Text
catalog and `SearchText` index. Full-Text Search must be installed; migration
failure is explicit when it is unavailable. After applying a migration that
changes source or projection data, rebuild the searchable projection:

```powershell
dotnet run --project server -- --rebuild-maintenance-search-documents
```

When embeddings are explicitly enabled and configured, rebuild them after the
search-document projection:

```powershell
dotnet run --project server -- --rebuild-maintenance-embeddings
```

The domain-contract migration canonicalizes copied metadata in existing
`MaintenanceSearchDocument` rows but does not regenerate `SearchText`; use the
rebuild command above after applying it.

## Synthetic Development Data

The fixture is entirely fictional, represents no actual GSD maintenance history,
and is not a final production import contract. It is based only on visible Page
1 blank forms and will be revised after Page 2 forms and official completed
samples become available.

With a reachable configured database, run seed/reset only in Development:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project server -- --seed-synthetic
dotnet run --project server -- --reset-synthetic-seed
dotnet run --project server -- --rebuild-maintenance-search-documents
```

`--seed-synthetic` deterministically upserts 20 fixture assets, 34 schedules,
and 30 inspections. `--reset-synthetic-seed` removes only records whose IDs
belong to the fixture, in inspection, schedule, then asset order. Reset refuses
to continue if unrelated records depend on fixture-owned assets or schedules.
Seed/reset neither runs during normal API startup nor succeeds outside
Development. The rebuild command is explicit, transactional on SQL Server,
idempotent, and does not start HTTP hosting. Supplying more than one
maintenance command flag is rejected without executing an operation.

The fixture uses five deterministic synthetic actor IDs for assignee and
inspector references. When development users are introduced later, reuse those
IDs rather than creating a temporary production user table solely for seeding.

The operational fixture is version `1.1.0`. The retrieval evaluation manifest
is version `1.1.0`, is copied only to test output, and remains test-only: it is
not loaded by the API, persisted, indexed, embedded, included in prompts, or
returned by ordinary DTOs. Both files are fictional and based only on visible
Page 1 blank-form fields; Page 2, completed samples, acknowledgement, and RMRF
rules remain provisional.

Inspection list/detail reads, maintenance issue normalization, and internal
lexical FTS retrieval are complete. Lexical retrieval searches only the
rebuildable `MaintenanceSearchDocument.SearchText` projection and returns
source-traceable inspection metadata; it has no public review endpoint. Domain-
contract hardening is complete: stable persisted codes have feature-owned
catalogs, canonical API/storage values, SQL Server constraints, and migration
preflight checks. Semantic retrieval is now an internal channel required by the
target maintenance-history review workflow: it stores only document embeddings,
never query vectors, and does not affect core or lexical workflows when its
provider is disabled. The retrieval benchmark is complete as a reproducible
lexical/semantic evaluation tool. The next backend task is
`feat/observability-metrics`, with `feat/retrieval-fusion` following
observability.

Embeddings are disabled by default. Remote providers are rejected unless
`Embeddings:AllowRemoteProvider` is explicitly enabled after a separate
privacy review. The current semantic MVP uses a provider-neutral
OpenAI-compatible adapter and application-layer cosine similarity; it does not
introduce a separate vector database or claim model-quality results.

## Retrieval Benchmark

The test-only evaluation manifest is version `1.1.0` and contains 24 bounded
queries across the four synthetic asset categories, English, Tagalog, and
Taglish. It includes cold-start asset context and expected inspection IDs, but
it is never loaded by the API or included in operational seed, search,
embedding, prompt, or DTO paths.

Run the standalone benchmark against a reachable SQL Server using the required
connection-string environment variable:

```powershell
$env:UNIPM_SQLSERVER_TEST_CONNECTION = "Server=localhost,1433;Database=master;User Id=sa;Password=<local-password>;Encrypt=True;TrustServerCertificate=True;"
dotnet run --project .\tools\UniPM.RetrievalBenchmark -- --channels lexical
dotnet run --project .\tools\UniPM.RetrievalBenchmark -- --channels semantic
dotnet run --project .\tools\UniPM.RetrievalBenchmark -- --channels lexical,semantic --output artifacts\retrieval-benchmark
```

Each run creates a temporary database, applies migrations, loads the synthetic
fixture, rebuilds the projection, waits for SQL Server Full-Text population,
and writes deterministic JSON and Markdown reports. The temporary database is
dropped by default. Set `UNIPM_BENCHMARK_KEEP_DATABASE=true` only for local
inspection. Semantic runs additionally require the configured embedding
environment contract; provider failures are reported as benchmark failures,
not silently scored as empty retrieval.

Reports include Hit@1, Hit@5, Precision@5, Recall@5, Recall@10, reciprocal
rank, first relevant rank, macro averages, and language/category/scenario
slices. The benchmark does not implement score fusion, RRF, thresholds,
insufficient-evidence policy, summaries, or a public retrieval endpoint.

## Engineering Evidence

UniPM keeps raw local command output under ignored `artifacts/` and reviewed,
sanitized, traceable records under `reference/evidence/`. Evidence records name
the exact tested commit and distinguish source inspection, local execution,
deterministic-provider orchestration, and real-provider execution. Synthetic
benchmark results do not prove production GSD performance; deterministic
embeddings prove pipeline behavior only. No lexicon precision/recall/F1 claim
is made because an independent labeled lexicon evaluator does not exist.

Run the Windows-first backend capture workflow with PowerShell:

```powershell
.\scripts\evidence\Invoke-BackendVerification.ps1
.\scripts\evidence\Invoke-BackendVerification.ps1 -Configuration Release -RunSqlServerTests
.\scripts\evidence\Invoke-BackendVerification.ps1 -RunSqlServerTests -BenchmarkChannels lexical
.\scripts\evidence\Invoke-BackendVerification.ps1 -RunSqlServerTests -BenchmarkChannels lexical,semantic
```

The script writes timestamped, ignored artifacts with safe environment metadata,
logs, TRX results, a machine-readable summary, and SHA-256 hashes. SQL Server
and real semantic-provider verification are opt-in and fail clearly when their
configuration is unavailable. Prometheus, Grafana, and production observability
are not implemented in this branch.

## Project References

- [`AGENTS.md`](AGENTS.md)
- [`PROJECT.md`](PROJECT.md)
- [`reference/planning/current-priorities.md`](reference/planning/current-priorities.md)
