# UniPM

UniPM is a web and mobile preventive-maintenance system for the university
General Services Department. The repository contains an ASP.NET Core API and
multi-asset preventive-maintenance forms. The current validation baseline
focuses on the preventive-maintenance information system while further GSD
requirements are being validated: core PMIS workflows run without any AI
provider configuration. Maintenance-history RAG was previously implemented
and evaluated as controlled development work. Its implementation and storage
are now retired; historical evidence remains in this repository.

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

The database retains assets, schedules, inspection records, PM forms,
acknowledgements, and the separate ReferenceDocument foundation. The
`RetireMaintenanceHistoryRagStorage` migration removes the maintenance
search projection and embeddings, its Full-Text index, and the dedicated
`UniPMMaintenanceRetrieval` catalog. `UniPMReferenceRetrieval` and all
reference-document tables remain. SQL Server native vector features and a
separate vector database are not required.

## Current API Surface

The backend currently provides:

- asset creation, list, detail, and QR lookup;
- schedule creation, list, and detail;
- inspection list, detail, and acknowledged asset-history reads;
  inspection-row creation and editing occur through Draft preventive-
  maintenance forms;
- preventive-maintenance form drafting and inspection-row management;
- whole-form submission with provisional file-number allocation;
- field-work-driven schedule completion from `InspectionRecord.CompletedAt`;
- whole-form acknowledgement, which makes completed rows eligible for
  acknowledged-only official history;
- a GSD-only corrective-action handoff read model for acknowledged forms;
- JWT login, refresh, logout, and current-user routes under `/api/v1/auth`;
- policy-protected asset, schedule, and preventive-maintenance form operations;
- reference-data categories, validation/error contracts, health checks, tests,
  and backend CI.

## Confirmed Preventive-Maintenance Workflow

One digital preventive-maintenance form represents one existing one-page
institutional form and contains multiple asset inspection rows. The lifecycle
is `Draft -> Submitted -> Acknowledged`; one submitted form receives one
provisional file number, while each row keeps its own inspection ID.

The department head acknowledges the whole form through the skilled worker's
authenticated mobile session and does not require a UniPM account. Field-work
completion is recorded in each `InspectionRecord.CompletedAt` and completes the
linked schedule before form submission. Acknowledgement records receipt/noting,
does not alter execution or compliance timestamps, does not complete schedules,
and makes completed rows eligible for acknowledged-only official history. Draft
and Submitted rows are not official history or retrieval evidence. Retired
retrieval/RAG behavior is documented only as historical evidence
by the current runtime.
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
read-only list/detail review and compact asset history; field inspection
submission is handled by the mobile preventive-maintenance workflow.

## Mobile Application

The `mobile/` Flutter application is Android-first and currently provides
memory-only authentication, an authenticated Inspector/GSD shell, QR-based
asset entry, and the Draft preventive-maintenance form workflow. Mobile users
can create a Draft form, add multiple inspection rows, resume a Draft, update
or delete Draft rows, and submit the whole form through the API. Submission
assigns a provisional file number and makes the form read-only.

The mobile client starts signed out after restart, does not persist access
tokens or cookies, and does not implement refresh/replay or offline
synchronization. Offline sync is deferred; its persistence and synchronization
architecture remain undecided pending a separate approved decision.
Acknowledgement and signature capture are available in the web review workflow;
later mobile field actions remain outside the current mobile scope. See
[mobile/README.md](mobile/README.md).

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
```

For a named SQL Server instance, replace `Server=.` with
`Server=localhost\INSTANCE_NAME`.

The PMIS runtime needs no AI provider configuration. Maintenance-history
projection and embedding rebuild commands have been removed.

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

After database setup, start each app in its own PowerShell window from the
repository root:

```powershell
.\scripts\start-server.ps1
.\scripts\start-web.ps1
.\scripts\start-mobile.ps1
```

The server launcher reads the database connection and JWT settings from the
current process or the ignored root `.env`, then listens on port `5254`. The
web launcher uses that API and serves the app on port `5173`. The mobile
launcher uses the same API address and selects a connected Android device;
for a physical phone it sets up `adb reverse`. Install web and mobile
dependencies with `npm ci` in `web/` and `flutter pub get` in `mobile/` first.
Each launcher remains in the foreground until you stop it with Ctrl+C.

Check the API and open the web app:

```powershell
Invoke-WebRequest -UseBasicParsing http://localhost:5254/health/live
Invoke-WebRequest -UseBasicParsing http://localhost:5254/health/ready
Start-Process http://localhost:5173/login
```

The optional legacy SQL Server 2025 Docker Compose experiment is documented in
[`docker-compose.sqlserver2025.yml`](docker-compose.sqlserver2025.yml). It is
not the local baseline, is not required for IIS deployment, and must not reuse a
SQL Server 2019 data volume.

## Maintenance history RAG retirement

Maintenance-history RAG has been retired. Its review endpoint, summary
provider, maintenance retrieval/fusion, projection, rebuild commands,
benchmark, and experiment runners are removed.

Historical migrations, API descriptions, ADRs, experiments, and verification
records remain as evidence of prior work. The separate fictional
ReferenceDocument foundation, Full-Text Search, section embeddings, and shared
provider-neutral embedding components remain.

Schema-constrained natural-language analytics remains a planned post-validation
direction, pending professor/adviser confirmation. It is not implemented or
approved for this branch; any implementation requires a separate approved task
and branch after GSD validation.

See the historical [API description](reference/api/maintenance-review-v0.1.md)
and [engineering evidence](reference/evidence/INDEX.md) for prior results.

## Historical Planning Record: Inspection-History Analysis (Not Active)

The RAG-assisted inspection-history analysis capability was a planning
direction that was never implemented. Its design record is preserved unchanged
in [`reference/planning/rag-assisted-inspection-history-analysis.md`](reference/planning/rag-assisted-inspection-history-analysis.md).
It described analysis of acknowledged preventive-maintenance inspection records
for recurring findings, condition frequencies, time comparisons and recurrence
intervals, cross-asset patterns, distributions, and single-asset timelines,
with deterministic fact computation preceding any RAG-assisted interpretation.
It is not an active roadmap item on this branch.

## Validation Baseline Definition

The active boundary for this branch is documented in
[`reference/planning/mvp-definition.md`](reference/planning/mvp-definition.md):
the PMIS-only GSD validation baseline. It defines the acknowledged-history,
form-lifecycle, corrective-handoff, web, and mobile scope demonstrated without
AI, preserves the previous RAG-inclusive evaluated-MVP definition as history,
and claims no replacement innovation. Mobile remains part of UniPM but is
owned by a separate partner workstream.

A concise GSD validation note with the prepared validation questions is at
[`reference/planning/gsd-validation-note.md`](reference/planning/gsd-validation-note.md).
The repeatable local demonstration setup and walkthrough are documented in
[`reference/planning/gsd-pmis-demo-runbook.md`](reference/planning/gsd-pmis-demo-runbook.md).

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

Historical migrations remain unchanged. Apply the new retirement migration
through the normal EF migration path. It removes only maintenance RAG
projection storage. Rollback recreates the schema, but does not recover
retired projection rows or vectors. Core PM and reference-document data
are preserved. Full-Text Search remains required by the reference foundation.

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
```

`--seed-synthetic` deterministically upserts 20 fixture assets, 34 schedules,
and 30 inspections. `--reset-synthetic-seed` removes only records whose IDs
belong to the fixture, in inspection, schedule, then asset order. Reset refuses
to continue if unrelated records depend on fixture-owned assets or schedules.
Seed/reset neither runs during normal API startup nor succeeds outside
Development.

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

The operational synthetic fixture remains version `1.1.0`. PM seed/reset
commands preserve their fixture ownership and dependency protection. They
no longer build or delete a maintenance RAG projection. Inspection reads,
acknowledged-only official history, and corrective handoff remain PMIS
features.

Maintenance-history RAG has been retired. Its review endpoint, summary
provider, maintenance retrieval/fusion, projection, rebuild commands,
benchmark, and experiment runners are removed.

Historical migrations, API descriptions, ADRs, experiments, and verification
records remain as evidence of prior work. The separate fictional
ReferenceDocument foundation, Full-Text Search, section embeddings, and shared
provider-neutral embedding components remain.

Schema-constrained natural-language analytics remains a planned post-validation
direction, pending professor/adviser confirmation. It is not implemented or
approved for this branch; any implementation requires a separate approved task
and branch after GSD validation.

Reference embeddings remain disabled by default. Remote providers require
explicit configuration and a separate privacy review; ordinary PM workflows
make no embedding or summary calls.

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
changed. The dashboard covers HTTP and runtime technical health. Maintenance
RAG instruments and panels are removed. It does not measure maintenance KPIs.

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
```

The script writes timestamped, ignored artifacts with safe environment metadata,
logs, TRX results, a machine-readable summary, and SHA-256 hashes. SQL Server
verification is opt-in and fails clearly when its configuration is unavailable. Run the local observability evidence capture
with:

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\scripts\evidence\Invoke-ObservabilityVerification.ps1
```

The reviewed local result is recorded in TEST-002. It does not claim IIS
deployment, production uptime, alert effectiveness, long-term retention, real
traffic, or retrieval-quality improvement.

The retired summary experiments and benchmark results remain under
`reference/evidence/`. Their runners are no longer available.

## Project References

- [`AGENTS.md`](AGENTS.md)
- [`PROJECT.md`](PROJECT.md)
- [`reference/planning/current-priorities.md`](reference/planning/current-priorities.md)
- [Run UniPM locally](reference/guides/tutorials/getting-started.md)
- [Run the local stack](reference/guides/how-to/run-local-stack.md)
- [System capabilities reference](reference/guides/reference/system-capabilities.md)
- [Architecture and RAG boundaries](reference/guides/explanation/architecture-and-rag-boundaries.md)
