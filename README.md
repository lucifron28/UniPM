# UniPM

UniPM is a web and mobile preventive-maintenance system for the university
General Services Department. The repository contains an ASP.NET Core API, the
initial preventive-maintenance domain, and an implemented bounded maintenance-
history review MVP. The review endpoint is available only when explicitly
enabled and is disabled in committed configuration.

## License

UniPM is proprietary source-available software. All rights are reserved.

This repository is public for portfolio visibility, academic review,
demonstration, and assessment purposes only. Viewing or forking this repository
on GitHub does not grant permission to use, copy, modify, redistribute, deploy,
host, operate, commercialize, submit as another work, or create derivative works
from this software.

Academic submission, project defense, code review, repository viewing, or
demonstration access does not transfer ownership of the code, architecture,
database schema, documentation, retrieval pipeline, RAG workflow, benchmark
tooling, evidence records, or related materials.

Any production, institutional, departmental, administrative, internal
operational, commercial, or real maintenance-management use by a school,
university, office, department, employee, contractor, organization, company, or
third party requires a separate written software license agreement or service
contract with the copyright holder.

See `LICENSE.md` for details.

For operational licensing inquiries, contact the copyright holder.

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
embedding provider is operationally optional and degradable. Internal result
fusion uses inspectable Reciprocal Rank Fusion, followed by deterministic source
selection, prompt sanitization, and optional provider-neutral summarization.

## Current API Surface

The backend currently provides:

- asset creation, list, detail, and QR lookup;
- schedule creation, list, and detail;
- inspection submission, list, detail, and asset-history lookup;
- JWT login and current-user routes at `/api/v1/auth/login` and
  `/api/v1/auth/me`;
- policy-protected asset, schedule, inspection, and maintenance-review writes;
- `POST /api/v1/maintenance-review` for authenticated, source-bounded
  maintenance-history review when explicitly enabled;
- reference-data categories, validation/error contracts, health checks, tests,
  and backend CI.

## Web Foundation

The `web/` React + TypeScript + Vite application provides the initial route,
API-client, test, and CI foundation. Run it with Node 22:

```powershell
cd web
npm ci
npm run dev
```

See [web/README.md](web/README.md) for the committed OpenAPI generation flow.
Real browser authentication integration and all operational web modules remain
deferred.


## First Run

Create a local environment file:

```powershell
Copy-Item .env.example .env
```

Update the local SQL, JWT, Development-user, and Grafana passwords in `.env`,
then start the stack:

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

## Maintenance Review

The maintenance-review endpoint is disabled in committed configuration and is
available in any environment only when explicitly enabled. It requires the
`CanReviewMaintenanceHistory` policy (`GSD`, `Supervisor`, or
`DepartmentHead`) and performs at most two fused retrieval passes. For local
source-only review, set
`UNIPM_MAINTENANCE_REVIEW_ENABLED=true` and keep
`UNIPM_SUMMARY_ENABLED=false`. The endpoint returns selected original source
records when summaries are disabled, unavailable, or rejected by citation
validation. It never persists prompts, summaries, or sanitizer token maps.

Its MVP prompt sanitizer is limited to pattern-based masking of email,
supported Philippine mobile numbers, and labeled employee/student/staff/personnel
IDs. It does not generally detect free-text personal names. Keep external
provider use to fictional or separately reviewed, pre-sanitized data; see the
[maintenance-review API contract](reference/api/maintenance-review-v0.1.md) for
the provider and source-record boundary.

The provider-neutral summary adapter supports an optional `ThinkingMode` value:
empty omits the provider field, while `enabled` or `disabled` sends the
corresponding structured provider option. DeepSeek V4 experiment configuration
uses `deepseek-v4-flash` with `UNIPM_SUMMARY_THINKING_MODE=disabled`; committed
summary configuration remains disabled and no API key belongs in the repo.

See [`reference/api/maintenance-review-v0.1.md`](reference/api/maintenance-review-v0.1.md)
for the request, response, evidence-status, summary-status, source-selection,
and provider configuration contract.

## Authentication

UniPM uses ASP.NET Core IdentityCore with Guid keys, 15-minute configurable JWT
access tokens returned in JSON, and opaque rotating refresh tokens held only in
an HttpOnly `SameSite=Lax` cookie. Configure `UNIPM_JWT_ISSUER`,
`UNIPM_JWT_AUDIENCE`, `UNIPM_JWT_SIGNING_KEY`,
`UNIPM_JWT_ACCESS_TOKEN_MINUTES`, `UNIPM_AUTH_REFRESH_TOKEN_DAYS`, and the one
exact credentialed-CORS origin `UNIPM_WEB_ORIGIN`. HTTP startup outside
Development fails when JWT configuration is missing or invalid.

Refresh sessions have a non-sliding seven-day default absolute lifetime. Logout
revokes future refresh capability for its browser family but does not denylist
an already issued access token; clients must discard their in-memory token.
The React client and mobile refresh-token contracts remain deferred. See
[`reference/api/auth-v0.1.md`](reference/api/auth-v0.1.md) for cookie, replay,
origin, and limitation details.

Create or repair the five fictional local users only through the explicit
Development command:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:UNIPM_DEV_USER_PASSWORD = "<local-development-password>"
dotnet run --project server -- --seed-development-users
```

The provisional roles are `Admin`, `GSD`, `Inspector`, `Supervisor`, and
`DepartmentHead`. `Admin` is a technical role and is intentionally excluded
from preventive-maintenance operational policies. See
[`reference/api/auth-v0.1.md`](reference/api/auth-v0.1.md) for the endpoint and
policy contract.

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
dotnet run --project server -- --migrate-database
dotnet run --project server -- --seed-synthetic
dotnet run --project server -- --seed-development-users
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
inspector references. Development user seeding reuses those IDs so the fixture
and authentication scaffold remain aligned.

The operational fixture is version `1.1.0`. The retrieval evaluation manifest
is version `1.1.0`, is copied only to test output, and remains test-only: it is
not loaded by the API, persisted, indexed, embedded, included in prompts, or
returned by ordinary DTOs. Both files are fictional and based only on visible
Page 1 blank-form fields; Page 2, completed samples, acknowledgement, and RMRF
rules remain provisional.

Inspection list/detail reads, maintenance issue normalization, and internal
lexical FTS retrieval are complete. Lexical retrieval searches only the
rebuildable `MaintenanceSearchDocument.SearchText` projection and returns
source-traceable inspection metadata. It is an internal retriever, not a
standalone public search endpoint; fused retrieval feeds the authenticated
`POST /api/v1/maintenance-review` endpoint. Domain-contract hardening is
complete: stable persisted codes have feature-owned
catalogs, canonical API/storage values, SQL Server constraints, and migration
preflight checks. Semantic retrieval is now an internal channel required by the
target maintenance-history review workflow: it stores only document embeddings,
never query vectors, and does not affect core or lexical workflows when its
provider is disabled. Internal fused retrieval uses RRF with K=60, candidate
depth 20, output limit 10, deterministic ordering, component-rank traceability,
and explicit semantic degradation. The retrieval benchmark supports lexical,
semantic, and fused channels, but real semantic and fused model-quality evidence
remain pending a configured provider. Opt-in observability metrics and the local
technical-health monitoring profile and coarse authentication scaffold are
complete. EXP-002 executed the DeepSeek V4 summary experiment on fictional data
with developer-reviewed ratings; it did not establish production readiness.
Tagalog and Taglish language fit was weak, and five outputs violated the citation
contract. Inspection-submission integrity, retrieval/test layout organization,
and explicit free-text-name sanitizer limitation documentation are complete;
the web foundation is implemented; real web authentication integration and
operational modules remain deferred. The multilingual embedding baseline remains
pending a configured real provider.

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
dotnet run --project .\tools\UniPM.RetrievalBenchmark -- --channels fused --output artifacts\retrieval-benchmark-fused
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
slices. Fused reports preserve RRF metadata, FusionScore, and component ranks.
Fused benchmarking requires both SQL Server Full-Text Search and real semantic
provider configuration; degraded fused responses fail evaluation. Context
selection, insufficient-evidence handling, sanitization, summaries, and the
public review endpoint are implemented separately from benchmark scoring.

## Local Observability

Metrics are disabled by default in committed configuration. The ordinary local
stack remains available with:

```powershell
docker compose up --build -d
```

Enable the optional technical monitoring profile with:

```powershell
$env:UNIPM_METRICS_ENABLED = "true"
docker compose --profile observability up --build -d
```

Then use:

- API metrics: `http://localhost:5000/metrics`
- Prometheus: `http://localhost:9090`
- Grafana: `http://localhost:3000`

Grafana provisions the `unipm-prometheus` datasource and the
`unipm-system-health` dashboard automatically. The sample credentials in
`.env.example` are for local development only and must be changed. The
dashboard covers API/runtime and retrieval technical health; it is not the
future React maintenance KPI dashboard. Projection and embedding rebuild
commands report their results through command output and evidence records until
durable job telemetry is designed.

For IIS, enable `Observability__MetricsEnabled` only when network or
reverse-proxy policy restricts access to `/metrics`. Prometheus and Grafana
are optional and must not be required for API, health, migration, seed/reset,
projection, or embedding operations.

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
configuration is unavailable. Run the local observability evidence capture
with:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\evidence\Invoke-ObservabilityVerification.ps1
```

The reviewed local result is recorded in TEST-002. It does not claim IIS
deployment, production uptime, alert effectiveness, long-term retention, real
traffic, or retrieval-quality improvement.

Run the DeepSeek summary experiment only from a clean implementation commit with
the API key supplied through the process environment:

```powershell
$env:UNIPM_SUMMARY_API_KEY = "<secret>"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\evidence\Invoke-DeepSeekSummaryExperiment.ps1
```

The runner fixes the provider to `deepseek`, model to `deepseek-v4-flash`, and
thinking mode to `disabled`, uses only fictional seeded data, and never retains
the API key, JWT, authorization header, connection string, raw prompt, token
map, or complete provider payload. EXP-002 is executed with retained fictional
outputs and developer-reviewed ratings; it remains an experimental baseline, not
a production-readiness result.

## Project References

- [`AGENTS.md`](AGENTS.md)
- [`PROJECT.md`](PROJECT.md)
- [`reference/planning/current-priorities.md`](reference/planning/current-priorities.md)
