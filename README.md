# UniPM

UniPM is a web and mobile preventive-maintenance system for the university
General Services Department. The repository contains an ASP.NET Core API and
multi-asset preventive-maintenance forms. The current validation baseline
focuses on the preventive-maintenance information system while further GSD
requirements are being validated: core PMIS workflows run without any AI
provider configuration. Maintenance-history RAG was previously implemented
and evaluated as controlled development work and is not exposed by this
validation baseline.

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

## Database Baseline

UniPM's minimum supported and default local database platform is native Windows
SQL Server 2019 with Full-Text Search installed and database compatibility level
`150`. The proposed target architecture is ASP.NET Core hosted through IIS
with a native Windows SQL Server instance; Docker is optional development
tooling and is not required by that architecture.

The capstone evaluates UniPM as a local development prototype. IIS deployment,
public HTTPS exposure, and production workload verification are outside the
evaluated scope.

The database contains the initial `Asset`, `PreventiveMaintenanceSchedule`, and
`InspectionRecord` schema plus the rebuildable `MaintenanceSearchDocument`
projection. SQL Server Full-Text Search retrieves lexical candidates from
`MaintenanceSearchDocument.SearchText`. Versioned serialized embedding vectors
are stored with relational document metadata; the backend filters a bounded SQL
candidate set and calculates cosine similarity in application memory. Semantic
retrieval is a required UniPM retrieval channel, while its embedding provider is
operationally optional. When embeddings are unavailable, the system explicitly
reports degradation and uses the lexical channel without labeling the result as
hybrid. Inspectable Reciprocal Rank Fusion combines eligible lexical and
semantic results. Native SQL Server vector features and a separate vector
database are not required.
These retrieval channels persist as inactive infrastructure in this
validation baseline and are not exposed through any public runtime path.

## Current API Surface

The backend currently provides:

- asset creation, list, detail, and QR lookup;
- schedule creation, list, and detail;
- inspection submission, list, detail, and asset-history lookup;
- preventive-maintenance form drafting and inspection-row management;
- whole-form submission with provisional file-number allocation;
- whole-form acknowledgement, schedule completion, and acknowledged-row
  publication to official history and retrieval;
- a GSD-only corrective-action handoff read model for acknowledged forms;
- JWT login, refresh, logout, and current-user routes under `/api/v1/auth`;
- policy-protected asset, schedule, and inspection writes;
- reference-data categories, validation/error contracts, health checks, tests,
  and backend CI.

## Confirmed Preventive-Maintenance Workflow

One digital preventive-maintenance form represents one existing one-page
institutional form and contains multiple asset inspection rows. The lifecycle
is `Draft -> Submitted -> Acknowledged`; one submitted form receives one
provisional file number, while each row keeps its own inspection ID.

The department head acknowledges the whole form through the skilled worker's
authenticated mobile session and does not require a UniPM account. Only
acknowledgement completes linked schedules. Draft and Submitted rows are not
official history or retrieval evidence; acknowledged rows are eligible.
Signatory names, positions, signatures, signature data, and signature checksums
never enter retrieval, embeddings, prompts, or the corrective-handoff response.

Corrective action ends at preparation of an acknowledged handoff for manual
encoding in the existing GSD Work Management System. UniPM does not create,
approve, process, monitor, or track RMRFs or corrective-maintenance work, and
does not integrate directly with that system. See
[`reference/planning/confirmed-gsd-workflow.md`](reference/planning/confirmed-gsd-workflow.md).

## Web Application

The `web/` React + TypeScript + Vite application provides browser login,
HttpOnly refresh-cookie session restoration, protected routes, current-user
display, and logout on top of the generated API client. Access tokens remain
memory-only and ordinary 401 responses receive at most one replay after a
single-flight refresh. Run it with Node 22:

```powershell
cd web
npm ci
npm run dev
```

See [web/README.md](web/README.md) for the authentication boundary, local setup,
committed OpenAPI generation flow, and source-inspected Figma alignment.
The authenticated web application now includes asset, schedule, and inspection
review modules. Assets provide list/detail views, GSD-only
provisional creation, QR-value copying, and reference-data category labels.
Schedules provide URL-owned filters, recorded-status summaries, detail views,
and GSD/Supervisor creation using only the current backend contract. Neither
module invents editing, recurrence, status transitions, assignment, audit,
condition, work-order, or device-specification workflows. Inspections provide
read-only list/detail review and compact asset history; web inspection
submission remains deferred to the planned mobile workflow.

## Mobile Application

The `mobile/` Flutter application is Android-first and currently provides
memory-only authentication, an authenticated Inspector/GSD shell, and the
initial Draft preventive-maintenance form workflow. Mobile users can create a
Draft form, add multiple inspection rows, resume a Draft, and update or delete
Draft rows through the API.

The mobile client starts signed out after restart, does not persist access
tokens or cookies, and does not implement refresh/replay or offline
synchronization. Offline sync is deferred; its persistence and synchronization
architecture remain undecided pending a separate approved decision. Submission,
acknowledgement, signature capture, QR scanning, and later field actions remain
outside the current mobile scope. See [mobile/README.md](mobile/README.md).

## First Run

Install native Windows SQL Server 2019 with Database Engine Services and
Full-Text Search. Use Windows Authentication for local development and keep all
connection strings and passwords in the process environment, never in a
committed `.env` file:

```powershell
$env:ConnectionStrings__DefaultConnection =
  "Server=.;Database=UniPMDb;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;"

$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:UNIPM_DEV_USER_PASSWORD = "<temporary-development-password>"

dotnet ef database update --project server
dotnet run --project server -- --seed-synthetic
dotnet run --project server -- --seed-development-users
dotnet run --project server -- --rebuild-maintenance-search-documents
```

For a named SQL Server instance, replace `Server=.` with
`Server=localhost\INSTANCE_NAME`. Rebuild embeddings only when they are
explicitly enabled and configured:

```powershell
dotnet run --project server -- --rebuild-maintenance-embeddings
```

Verify the database installation and compatibility level:

```sql
SELECT
    SERVERPROPERTY('ProductMajorVersion') AS ProductMajorVersion,
    SERVERPROPERTY('IsFullTextInstalled') AS IsFullTextInstalled;

SELECT compatibility_level
FROM sys.databases
WHERE name = N'UniPMDb';
```

The expected development baseline is major version `15`,
`IsFullTextInstalled = 1`, and compatibility level `150`.

Start the API after the database setup:

```powershell
dotnet run --project server
```

Check the API:

```powershell
Invoke-WebRequest -UseBasicParsing http://localhost:5000/
Invoke-WebRequest -UseBasicParsing http://localhost:5000/health/live
Invoke-WebRequest -UseBasicParsing http://localhost:5000/health/ready
Invoke-WebRequest -UseBasicParsing http://localhost:5000/openapi/v1.json
```

The optional legacy SQL Server 2025 Docker Compose experiment is documented in
[`docker-compose.sqlserver2025.yml`](docker-compose.sqlserver2025.yml). It is
not the local baseline, is not required for IIS deployment, and must not reuse a
SQL Server 2019 data volume.

## Maintenance History Review Status

Maintenance-history RAG was previously implemented and evaluated as
controlled development work; see `reference/evidence/` and
[`reference/api/maintenance-review-v0.1.md`](reference/api/maintenance-review-v0.1.md)
for what was built at the time. In the current PMIS-only validation baseline,
the review endpoint is mapped into the runtime contract only when
`MaintenanceReview:Enabled` is explicitly true, and committed configuration
keeps it disabled, so the published OpenAPI contract and the generated web
client contain no maintenance-review operation. Retrieval, fusion, embedding,
and summary sources remain preserved in the repository pending cleanup
decisions after GSD validates the PMIS direction.

## Planned Inspection-History Analysis

The planned RAG-assisted inspection-history analysis capability is documented in
[`reference/planning/rag-assisted-inspection-history-analysis.md`](reference/planning/rag-assisted-inspection-history-analysis.md).
It is not implemented by `POST /api/v1/maintenance-review`. The planned
capability will analyze acknowledged preventive-maintenance inspection records
for recurring findings, condition frequencies, time comparisons and recurrence
intervals, cross-asset patterns, distributions, and single-asset timelines.

SQL and deterministic application code will calculate the authoritative facts;
RAG will retrieve the exact acknowledged records supporting them; and optional
generation will explain only the computed result model and displayed sources.
Every output will include scope/date range, computed facts, interpretation,
supporting acknowledged sources and locators, limitations, and no-diagnosis
wording. The language model will not calculate authoritative statistics,
diagnose equipment, infer causes, approve actions, or mutate records.

## Evaluated MVP Definition

The authoritative evaluated-MVP boundary is documented in
[`reference/planning/mvp-definition.md`](reference/planning/mvp-definition.md).
It keeps the implemented maintenance-review endpoint separate from the
planned inspection-history analysis capability and defines the acknowledged-
history, deterministic-analysis, source-retrieval, optional-interpretation,
web, and technical-observability scope. Mobile remains part of UniPM but is
owned by a separate partner workstream.

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
The React client implements this memory-only access-token and refresh-cookie
contract; the mobile refresh-token contract remains deferred. See
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
$env:ConnectionStrings__DefaultConnection =
  "Server=.;Database=UniPMDb;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;"
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

## SQL Server 2019 Compatibility Verification

The native compatibility runner is an explicit development verification step.
It uses only process-scoped settings and does not record connection strings,
passwords, or provider credentials:

```powershell
$env:ConnectionStrings__DefaultConnection =
  "<native SQL Server 2019 application database>"
$env:UNIPM_SQLSERVER2019_TEST_CONNECTION =
  "<native SQL Server 2019 master/test connection>"
$env:UNIPM_DEV_USER_PASSWORD = "<temporary-generated-development-password>"

powershell.exe -NoProfile -ExecutionPolicy Bypass `
  -File .\scripts\evidence\Invoke-SqlServer2019CompatibilityVerification.ps1
```

The runner checks SQL Server major version `15`, compatibility level `150`,
Full-Text Search, migrations, synthetic and Development-user seeding,
projection rebuild, Full-Text catalog/index readiness, `CONTAINSTABLE`, and the
SQL-enabled backend suite. An optional real-provider smoke test may remain
skipped when no provider configuration is supplied.

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
dotnet run --project server -- --seed-reference-documents
dotnet run --project server -- --reset-reference-documents
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

`--seed-reference-documents` creates a separate, fictional development corpus
for future approved institutional-procedure evidence retrieval. It upserts only
synthetic reference-document metadata, applicability records, and ordered sections;
`--reset-reference-documents` removes only fixture-owned synthetic corpus
records. It does not
load real university procedures, PDFs, OCR text, or source files,
and it does not change maintenance-history retrieval or the review endpoint.
The reference foundation has its own SQL Server Full-Text catalog and preserves
document revision, lifecycle, locator, checksum, and synthetic provenance for
later source-traceable retrieval work.

The fixture uses five deterministic synthetic actor IDs for assignee and
inspector references. Development user seeding reuses those IDs so the fixture
and authentication scaffold remain aligned.

The operational fixture is version `1.1.0`. The retrieval evaluation manifest
is version `1.1.0`, is copied only to test output, and remains test-only: it is
not loaded by the API, persisted, indexed, embedded, included in prompts, or
returned by ordinary DTOs. Both files are fictional and based only on visible
Page 1 blank-form fields; Page 2, completed samples, and official institutional
reference lists remain provisional. The confirmed digital acknowledgement and
corrective-handoff boundaries do not make unobserved physical-form fields final
production contracts.

Inspection list/detail reads, maintenance issue normalization, and internal
lexical FTS retrieval are complete. Lexical retrieval searches only the
rebuildable `MaintenanceSearchDocument.SearchText` projection and returns
source-traceable inspection metadata. It is an internal retriever, not a
standalone public search endpoint; internal fused retrieval has no public
endpoint in the validation baseline, and the previously implemented
maintenance-review endpoint is not exposed by default. Domain-contract hardening is
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
and explicit free-text-name sanitizer limitation documentation are complete.
The web foundation and browser authentication integration are implemented and
merged; the Flutter mobile foundation and initial Draft form workflow are also
implemented and merged in a separate partner-owned workstream. This workstream
does not implement mobile features. The reference-document foundation is
implemented and merged as a fictional metadata and sectioning foundation; approved
institutional source authorization and ingestion remain pending, and OEM
retrieval is excluded from the evaluated MVP. EXP-003 executed a local offline
Granite multilingual embedding baseline on the fictional maintenance fixture;
it is controlled development evidence only and does not make Granite a
deployment dependency or establish real institutional performance.

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
$env:UNIPM_SQLSERVER_TEST_CONNECTION =
  "Server=.;Database=master;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;"
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

## Optional Docker Development Tooling

The retained SQL Server 2025 Compose stack is a legacy development experiment,
not the verified platform baseline or a deployment requirement. It retains its
existing named volume and must be invoked explicitly:

```powershell
Copy-Item .env.sqlserver2025.example .env.sqlserver2025
docker compose --env-file .env.sqlserver2025 -f docker-compose.sqlserver2025.yml up --build -d
```

Do not start, remove, or reuse that SQL Server 2025 volume for a SQL Server 2019
instance. Stop it with the same explicit file and environment arguments.

## Local Observability

Metrics are disabled by default in committed configuration. The optional legacy
Docker profile can provide local Prometheus and Grafana only when intentionally
using the retained SQL Server 2025 experiment:

```powershell
$env:UNIPM_METRICS_ENABLED = "true"
docker compose --env-file .env.sqlserver2025 -f docker-compose.sqlserver2025.yml `
  --profile observability up --build -d
```

Then use:

- API metrics: `http://localhost:5000/metrics`
- Prometheus: `http://localhost:9090`
- Grafana: `http://localhost:3000`

Grafana provisions the `unipm-prometheus` datasource and the
`unipm-system-health` dashboard automatically. The sample credentials in
`.env.sqlserver2025.example` are local-development placeholders and must be
changed. The dashboard covers API/runtime and retrieval technical health; it is
not the future React maintenance KPI dashboard. Projection and embedding rebuild
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
- [Run UniPM locally](reference/guides/tutorials/getting-started.md)
- [Run the local stack and rebuild retrieval data](reference/guides/how-to/run-local-stack.md)
- [System capabilities reference](reference/guides/reference/system-capabilities.md)
- [Architecture and RAG boundaries](reference/guides/explanation/architecture-and-rag-boundaries.md)
