# AGENTS.md - UniPM Backend and Platform

## Project Identity

UniPM is a web and mobile preventive-maintenance information system for a
university General Services Department. This retirement branch preserves the PMIS-only GSD validation baseline: the confirmed preventive-maintenance
workflow is demonstrated without any AI feature so GSD can validate workflows,
forms, reports, and remaining operational needs before a replacement innovation
is selected.

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

The safety rules below still apply to any code path that could contact an
external AI provider, and chatbot-style open-ended AI behavior remains
prohibited anywhere in the codebase.

The main product identity remains:

- Web application for administration, monitoring, reporting, review, and source verification.
- Mobile application for field-side inspection, QR-based lookup, checklist completion, and inspection submission.
- Backend/API and database as the controlled source of truth.

## Current Stack

Do not change the stack without discussion.

- Backend: ASP.NET Core Web API (C#)
- Database: native Windows SQL Server 2019 (minimum supported platform)
  - Requires database compatibility level 150 and Full-Text Search.
  - Stores serialized embedding vectors; the backend calculates bounded cosine
    similarity in application memory. Do not introduce a separate vector
    database or require native SQL vector features.
- Local dev: native SQL Server 2019 is the default path. The retained SQL
  Server 2025 Docker Compose stack is optional development tooling only.
- Proposed target deployment: IIS on Windows Server, not Docker
  - IIS deployment is outside the evaluated capstone prototype. Do not describe
    it as completed verification.
  - Avoid Docker-only assumptions in application code.
- Web frontend: React + TypeScript + Vite
- Mobile: Flutter
  - Offline sync is deferred; its persistence and synchronization architecture
    remain undecided until a separate approved decision.
  - Do not assume constant connectivity in mobile-facing API contracts.
- Testing: xUnit

## Repository Structure Rule

UniPM uses a single monorepo for backend, web, mobile, database, tests, and reference docs.

Expected top-level structure:

- `server/` - ASP.NET Core Web API
- `web/` - React + TypeScript + Vite frontend
- `mobile/` - Flutter mobile app
- `database/` - SQL Server bootstrap/init scripts
- `tests/` - backend tests and future shared test projects
- `reference/` - planning, API contracts, project guidance, and non-private references
- `.github/` - CI workflows

Do not split the frontend or mobile app into separate repositories unless explicitly decided later.
The `web/` and `mobile/` directories may begin as placeholders, but preserve
their tracked application structure once implementation starts.

## Hard Architecture Rules

These are non-negotiable.

1. Web and mobile clients never call the database, embedding provider, or LLM provider directly. Everything goes through the backend API.
2. AI provider credentials live only in backend environment variables. They must never appear in frontend code, mobile code, committed `appsettings`, seed files, documentation examples with real keys, or logs.
3. Any prompt sent to an external AI provider must be built from sanitized/masked data. Raw prompts and token maps must never be persisted.
4. No AI call may bypass the sanitizer path. This applies even during MVP work.
5. No autonomous decision-making anywhere. Do not auto-approve corrective actions, auto-change asset status, auto-file RMRF records, or auto-generate official maintenance decisions from AI output.
6. AI output is always assistive and must be returned or displayed with the source records used.
7. Source records remain the evidence. The generated summary is only a review shortcut.

## Historical sanitizer boundary and future AI work

The full privacy masking/token-map pipeline was deferred during the evaluated
RAG work. If any AI path is introduced through a separately
approved post-GSD decision, basic sanitization is mandatory before any external
provider call. The retired `PrivacySanitizerService` provided:

- email masking, for example `user@example.com` -> `[EMAIL_1]`
- Philippine-style phone/mobile number masking where practical, for example `0917-123-4567` -> `[PHONE_1]`
- obvious employee/student/staff ID masking, for example `Employee ID 2024-001` -> `[EMPLOYEE_ID_1]`
- synthetic names only in seed/demo data
- no raw prompt logging
- no token-map persistence
- no full AI provider payload logging

The retired sanitizer used pattern-based token masking and pseudonymization, not
anonymization. It does not identify or mask arbitrary free-text personal names;
synthetic names in fixture data do not demonstrate protection for real names.
Do not send real or unscreened institutional text to a remote summary provider
under this MVP boundary. Stronger name handling requires a separate privacy
design and review.

Stronger privacy handling later may include:

- role-based token replacement
- known personnel-name matching from the database
- request-scoped token maps
- source rehydration if approved
- more complete audit metadata

Use the terms token masking, pseudonymization, and prompt sanitization. Do not
describe the MVP sanitizer as anonymization.

## Reference embedding storage and historical retrieval

SQL Server 2019 is the minimum supported target database and no separate vector
database should be introduced.

In the retained reference-document foundation, embeddings are stored as versioned
serialized values alongside relational reference-section metadata. The backend
filters a bounded candidate set in SQL Server and calculates cosine similarity
in application memory. Native SQL Server vector features are not required for UniPM.

Allowed for MVP:

- store embedding vectors in SQL Server as JSON/string/binary or another simple temporary format
- compute cosine similarity in backend code
- keep the embedding provider behind `IEmbeddingService`
- cache embeddings and never regenerate unchanged record embeddings unnecessarily

Not allowed:

- adding Pinecone, Qdrant, Weaviate, Chroma, Milvus, or another vector DB without discussion
- frontend/mobile embedding calls
- hardcoding one provider so it cannot be swapped later

Core preventive-maintenance workflows must never depend on embeddings or an
LLM. On this validation branch they never contact either one.

## Engineering Evidence Rules

For work that changes or verifies production behavior, architecture, database
schema, retrieval, AI providers, security/privacy, infrastructure, tests,
benchmarks, or deployment configuration, read
`reference/evidence/README.md` first.

- Keep raw command output in ignored `artifacts/`; commit only reviewed,
  sanitized records under `reference/evidence/`.
- Every executed record must identify the exact tested commit SHA. Source-
  inspected history is not executed verification.
- Distinguish real-provider evidence from deterministic fake-provider evidence;
  fake embeddings prove orchestration only, not semantic model quality.
- Treat approved experiments and baselines as immutable. Give new experiments
  new IDs instead of overwriting earlier results.
- State skipped or unavailable verification explicitly, and never copy secrets,
  credentials, endpoints, or sensitive configuration into evidence.

## AI Provider Cost Controls

This is a student-budget project. These controls apply whenever a preserved AI
integration is enabled through an approved future decision; ordinary PMIS work
makes no AI calls.

- Prefer local or free-tier embedding for MVP.
- DeepSeek prepaid may be used for LLM summary generation if configured by the developer.
- Keep LLM calls behind `ISummaryService` or an equivalent interface.
- Keep embedding calls behind `IEmbeddingService` or an equivalent interface.
- Cache embeddings.
- Add defensive limits where practical:
  - max embedding calls per request
  - max source records sent to summary generation
  - max prompt size
  - daily/dev environment switches to disable AI calls
- Core preventive-maintenance workflows must still work if AI is disabled or unavailable.

## Cost and execution limits

Optimize for minimal agent usage.

- Make the smallest change that satisfies the request.
- Do not create or modify tests unless explicitly requested.
- Run only the single most relevant targeted test.
- Run that test once after the initial implementation. If it exposes an
  actionable compile or test defect within the requested scope, make one
  focused correction and rerun the same targeted test once.
- Never run the complete test suite unless explicitly requested.
- Never repeatedly fix and rerun tests automatically.
- If the targeted test fails because of an actionable in-scope defect, fix it
  once and rerun it. If the rerun fails, or the failure is external,
  environment-related, unrelated, ambiguous, or requires a broader change,
  report the blocker and stop.
- Do not run lint, formatting, type checking, builds, and tests together.
- Do not perform unrelated refactoring.
- After the requested change and its permitted verification pass are
  complete, summarize the changes and stop.

## Confirmed Workflow Boundaries And Remaining Clarifications

The confirmed workflow baseline is
[`reference/planning/confirmed-gsd-workflow.md`](reference/planning/confirmed-gsd-workflow.md).

- One preventive-maintenance form mirrors one existing one-page form and may
  contain multiple inspection rows.
- The form lifecycle is `Draft -> Submitted -> Acknowledged`. Saving a completed
  inspection marks its linked schedule `Completed` while the form remains
  `Draft`. Submission and acknowledgement are separate whole-form transitions.
  Acknowledgement records the Department Head's receipt and signatory details;
  it does not complete schedules.
- The department head does not require a UniPM account. The skilled worker's
  authenticated mobile session captures the signatory name, position, and
  signature as form data.
- Draft and Submitted rows are excluded from official history and retrieval;
  acknowledged rows are eligible. Signature and signatory data are never
  retrieval, embedding, prompt, or corrective-handoff data.
- Corrective-action handoff preparation is confirmed; UniPM does not create,
  process, or monitor RMRFs or the external WMS lifecycle.

Remaining GSD/adviser clarifications:

- official building/department/location list
- who has authority to adjust schedules
- final audit-log persistence rules
- final full privacy masking/token-map implementation
- approved institutional CPMP/checklist/SOP sources remain pending
- OEM retrieval is excluded from the evaluated MVP

Do not invent final schema or business logic for these unless explicitly told the clarification arrived. If a task seems to require finalizing any deferred item, stop and flag it.

## Reference-Document Foundation

Approved institutional procedures, forms, checklists, and SOPs are a separate
future evidence group, not maintenance history. The current foundation may
preserve fictional source metadata, revision/lifecycle, applicability, ordered
sections, locators, checksums, synthetic provenance, deferred section
embeddings, and SQL Server Full-Text indexing. Do not ingest real documents,
add upload/OCR/extraction, or expose institutional retrieval until authorization
and ingestion are approved. OEM retrieval is excluded from the evaluated MVP;
do not add an OEM corpus, retrieval channel, fusion, or synthesis.

Acceptable temporary/MVP work:

- scaffold interfaces and placeholders
- use synthetic/demo data
- use clearly named temporary DTOs
- implement minimal sanitizer required for safe MVP AI calls
- implement read-side endpoints that do not finalize deferred workflow semantics

## Current Capability Status (Validation Branch)

The PMIS runtime exposes authentication, assets with QR lookup, schedules,
multi-row preventive-maintenance forms covering the full
`Draft -> Submitted -> Acknowledged` lifecycle, acknowledged-only official
history, and GSD-only corrective-action handoff preparation. No endpoint
requires an AI provider.

The retired `/api/v1/maintenance-review` route, configuration, services,
maintenance retrieval/fusion, and derived storage no longer exist. The
ReferenceDocument foundation and shared embedding components remain.
Historical evidence and migrations are preserved; older RAG sections below
describe prior work and safety rules for separately approved future work.

The RAG-assisted inspection-history analysis capability described in
[`reference/planning/rag-assisted-inspection-history-analysis.md`](reference/planning/rag-assisted-inspection-history-analysis.md)
was a planning direction that was never implemented; it is not an active
priority on this branch. The active boundary is defined in
[`reference/planning/mvp-definition.md`](reference/planning/mvp-definition.md).

Browser authentication integration, the reference-document foundation, and the
Flutter mobile foundation are implemented and merged in the separate,
partner-owned mobile workstream. The mobile foundation includes memory-only
authentication, QR-based asset entry, the Draft preventive-maintenance form
workflow, and whole-form submission. Web acknowledgement and signature capture
are implemented separately. Offline synchronization is deferred and its
persistence and synchronization architecture remain undecided; later mobile
field actions remain outside this workstream.

## Current Unblocked Work

Priority order on this branch is the GSD validation phase:

1. Keep the PMIS validation branch stable and runnable.
2. Verify the confirmed preventive-maintenance workflow end to end.
3. Verify AI-independent startup and operation.
4. Confirm acknowledged-only official history and corrective-handoff preparation.
5. Prepare and run the GSD demonstration.
6. Collect exact form, report, and process requirements from GSD.
7. Record findings and defer all innovation selection until a separate
   approved decision and branch.

The deterministic synthetic fixture, Development-only seed/reset commands,
reset dependency protection, inspection list/detail reads, the committed
engineering-evidence workflow, and opt-in observability metrics are complete.
IdentityCore persistence, JWT login/current-user routes, Development user
seeding, and coarse policy protection are also complete. Inspection-submission
integrity, explicit documentation of the historical MVP sanitizer's
free-text-name limitation, the React web foundation, browser
authentication integration, asset registry, preventive maintenance schedule
workflows, read-only inspection review, multi-asset form drafting, form
submission, whole-form acknowledgement, acknowledged-only history publication,
and GSD-only corrective-action handoff preparation are also complete. The
Flutter mobile foundation, initial Draft form workflow, and whole-form
submission are complete; later mobile field actions remain separately approved
work. The multilingual
embedding baseline is recorded as controlled development evidence; it does not
establish real institutional performance.

Observability remains bounded infrastructure: `Observability:MetricsEnabled`
is false by default, `/metrics` is exposed only when explicitly enabled, and
the local Prometheus/Grafana services are available only through the Compose
`observability` profile. The dashboard is technical system health, not a
maintenance KPI dashboard. Do not add tracing, centralized logs, alerting, or
production monitoring claims to this scope.

Retrieval fusion was implemented as an internal RRF orchestration service using
K=60, bounded candidate/result limits, deterministic component-rank
traceability, and explicit semantic degradation. The completed maintenance-
review layer added deterministic context tiers, request-scoped prompt
sanitization, optional provider-neutral summaries, and source-returning
evidence contracts. It remained authenticated whenever enabled. EXP-002
provides a fictional, developer-reviewed summary-provider baseline only; it
does not establish production readiness or real semantic/fused model quality.

Admin is a technical system-administration role, not an operational super-role.
Operational policies use GSD, Inspector, Supervisor, and DepartmentHead as
documented; final institutional RBAC remains deferred.

Unblocked areas:

- Authentication roles currently scaffolded:
  - Admin
  - GSD
  - Inspector
  - DepartmentHead
  - Supervisor
- List/GET endpoints for existing entities:
  - Assets
  - Schedules
  - Inspections
- Asset category detail tables for the four selected categories:
  - Fire extinguishers
  - Fire alarm systems
  - Emergency lights
  - Water drinking stations
- ReferenceDocument foundation:
  - fictional metadata, ordered sections, applicability, and provenance;
  - reference Full-Text Search, section embeddings, and shared embedding provider;
  - institutional ingestion and exposure remain separately approved work.
- Maintenance-history RAG:
  - runtime, tooling, tests, and derived storage retired;
  - historical evidence and migrations preserved;
  - replacement innovation requires a separately approved post-GSD decision.

## Historical RAG behavior boundaries

These boundaries governed the previously evaluated maintenance-review feature
and remain the required safety shape for any future approved AI-assisted work.
They are not active work items on this branch. The former feature followed this
shape:

`current finding -> retrieval -> source selection -> sanitization -> source-bounded summary -> source display -> human verification`

It should not follow this shape:

`user asks anything -> chatbot answers freely`

Required RAG behavior:

- retrieve related records before generation
- include source records used
- state limitations when evidence is weak
- do not claim recurring history if no same-asset history exists
- clearly label similar-asset or reference fallback context
- do not invent dates, causes, RMRF numbers, corrective actions, or personnel decisions

## Coding Conventions

- Before adding a new service/controller/pattern, inspect existing code and match the codebase style.
- Do not introduce a second competing endpoint/service pattern unless explicitly requested.
- One logical change per migration.
- Do not squash unrelated schema changes into one migration.
- Every new endpoint needs at least one test:
  - happy path minimum
  - add failure cases where meaningful
- Prefer extending existing services over creating parallel services with overlapping responsibility.
- Use DTOs for API contracts.
- Keep provider-specific code behind interfaces.
- Keep AI prompt construction centralized.
- Do not log raw sensitive data.

## Git And Commit Guidance

- Keep commits small and conventional.
- Prefer messages such as:
  - `feat(api): add asset list endpoint`
  - `test(api): cover inspection schedule completion`
  - `chore(seed): add synthetic maintenance records`
  - `feat(web): add corrective-handoff review page`
- Do not mix manuscript edits, backend schema changes, frontend UI, and AI provider changes in one commit.
- Do not commit:
  - `.env`
  - real API keys
  - raw AI prompts
  - token maps
  - real sensitive institutional records
  - local-only agent files unless explicitly intended

## Manuscript Alignment Note

This is a capstone project. The written manuscript should describe the target system as:

- ASP.NET Core Web API
- SQL Server
- React + TypeScript + Vite web frontend
- Flutter mobile app
- provider-neutral design for preserved AI/embedding services (historical evaluated component; inactive in the current PMIS validation baseline)

Some old manuscript diagrams may still say Django/PostgreSQL or hard-lock a specific AI provider. Those are being corrected and are not the target stack.

If generating docs or diagrams to accompany code, use ASP.NET Core + SQL Server terminology.
