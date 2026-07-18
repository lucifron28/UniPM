# AGENTS.md - UniPM Backend and Platform

## Project Identity

UniPM is a web and mobile preventive maintenance system for a university General Services Department, with a bounded RAG-based maintenance-history review feature.

The RAG feature is not the product. It is an assistive, source-bounded support feature that retrieves related past records and summarizes them for human review. It does not diagnose, decide, approve, or act autonomously. Do not build chatbot-style open-ended AI behavior anywhere in this codebase.

The main product identity remains:

- Web application for administration, monitoring, reporting, review, and source verification.
- Mobile application for field-side inspection, QR-based lookup, checklist completion, and inspection submission.
- Backend/API and database as the controlled source of truth.
- RAG as an advanced support feature inside the maintenance workflow.

## Current Stack

Do not change the stack without discussion.

- Backend: ASP.NET Core Web API (C#)
- Database: SQL Server 2025 Developer Edition
  - Chosen for relational records, Full-Text Search, and future/native vector-search support.
  - Do not introduce a separate vector database.
- Local dev: Docker Compose (`unipm-api`, `unipm-db`, `unipm-db-init`)
- Target production: IIS on Windows Server, not Docker
  - Avoid Docker-only assumptions in application code.
- Web frontend: React + TypeScript + Vite
- Mobile: Flutter
  - Offline-first sync is planned later through SQLite/Hive/Isar or an approved equivalent.
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
The `web/` and `mobile/` directories may exist as placeholder directories until scaffolding starts; keep them empty except for required Git placeholder files.

## Hard Architecture Rules

These are non-negotiable.

1. Web and mobile clients never call the database, embedding provider, or LLM provider directly. Everything goes through the backend API.
2. AI provider credentials live only in backend environment variables. They must never appear in frontend code, mobile code, committed `appsettings`, seed files, documentation examples with real keys, or logs.
3. Any prompt sent to an external AI provider must be built from sanitized/masked data. Raw prompts and token maps must never be persisted.
4. No AI call may bypass the sanitizer path. This applies even during MVP work.
5. No autonomous decision-making anywhere. Do not auto-approve corrective actions, auto-change asset status, auto-file RMRF records, or auto-generate official maintenance decisions from AI output.
6. AI output is always assistive and must be returned or displayed with the source records used.
7. Source records remain the evidence. The generated summary is only a review shortcut.

## MVP Safety Rule For RAG Work

The full privacy masking/token-map pipeline is deferred, but basic MVP sanitization is not optional if an external provider is called.

For the thin RAG MVP, implement or use a `PrivacySanitizerService` with at least:

- email masking, for example `user@example.com` -> `[EMAIL_1]`
- Philippine-style phone/mobile number masking where practical, for example `0917-123-4567` -> `[PHONE_1]`
- obvious employee/student/staff ID masking, for example `Employee ID 2024-001` -> `[EMPLOYEE_ID_1]`
- synthetic names only in seed/demo data
- no raw prompt logging
- no token-map persistence
- no full AI provider payload logging

The current sanitizer is pattern-based token masking and pseudonymization, not
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

## Vector Search MVP Rule

SQL Server 2025 is the target database and no separate vector database should be introduced.

For the first RAG MVP, app-layer cosine similarity over embeddings stored in SQL Server or in a reproducible local fixture is allowed if SQL Server native vector search slows down setup. Native SQL Server vector search can be added after the end-to-end loop works.

Allowed for MVP:

- store embedding vectors in SQL Server as JSON/string/binary or another simple temporary format
- compute cosine similarity in backend code
- keep the embedding provider behind `IEmbeddingService`
- cache embeddings and never regenerate unchanged record embeddings unnecessarily

Not allowed:

- adding Pinecone, Qdrant, Weaviate, Chroma, Milvus, or another vector DB without discussion
- frontend/mobile embedding calls
- hardcoding one provider so it cannot be swapped later

Semantic retrieval is a required target channel of the maintenance-history
review feature, but it is operationally degradable. If embeddings are
unavailable, maintenance review may use SQL, lexicon normalization, and FTS
fallback while explicitly reporting lexical fallback. Core
preventive-maintenance workflows must never depend on embeddings or an LLM.

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

This is a student-budget project. Avoid open-ended API usage.

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

## Scope Boundaries

Deferred pending GSD/adviser clarifications:

- final acknowledgement workflow rules
- final corrective-maintenance handoff/RMRF business process
- official building/department/location list
- who has authority to adjust schedules
- final audit-log persistence rules
- final full privacy masking/token-map implementation
- approved CPMP/checklist/SOP/OEM reference knowledge base

Do not invent final schema or business logic for these unless explicitly told the clarification arrived. If a task seems to require finalizing any deferred item, stop and flag it.

Acceptable temporary/MVP work:

- scaffold interfaces and placeholders
- use synthetic/demo data
- use clearly named temporary DTOs
- implement minimal sanitizer required for safe MVP AI calls
- implement read-side endpoints that do not finalize deferred workflow semantics

## Current Unblocked Work

Priority should move risk-first:

1. Confirm the backend runs and tests pass.
2. Preserve engineering evidence for implementation and verification.
3. Benchmark the completed lexical and semantic channels separately; the
   semantic provider remains operationally optional and degradable.
4. Fuse retrieval results with inspectable RRF and explicit degradation.
5. Preserve the completed source-bounded maintenance-review path.
6. Preserve coarse authentication and authorization while final RBAC remains
   provisional.

The deterministic synthetic fixture, test-only retrieval evaluation manifest,
Development-only seed/reset commands, reset dependency protection, inspection
list/detail reads, the v1.0 maintenance issue lexicon, the rebuildable
`MaintenanceSearchDocument` projection, lexical and semantic channels, the
separate retrieval benchmark, internal RRF fusion, the committed
engineering-evidence workflow, and opt-in observability metrics are complete.
IdentityCore persistence, JWT login/current-user routes, Development user
seeding, and coarse policy protection are also complete. Inspection-submission
integrity, retrieval/test folder organization, explicit documentation of the
MVP sanitizer's free-text-name limitation, and the React web foundation are
also complete. The exact next branch is `feat/web-auth-integration`; the
multilingual embedding baseline remains pending a configured real provider.

Observability remains bounded infrastructure: `Observability:MetricsEnabled`
is false by default, `/metrics` is exposed only when explicitly enabled, and
the local Prometheus/Grafana services are available only through the Compose
`observability` profile. The dashboard is technical system health, not a
maintenance KPI dashboard. Do not add tracing, centralized logs, alerting, or
production monitoring claims to this scope.

Retrieval fusion is an internal RRF orchestration service using K=60, bounded
candidate/result limits, deterministic component-rank traceability, and
explicit semantic degradation. The completed maintenance-review layer adds
deterministic context tiers, request-scoped prompt sanitization, optional
provider-neutral summaries, and source-returning evidence contracts. It remains
authenticated whenever enabled. EXP-002 provides a fictional, developer-reviewed
summary-provider baseline only; it does not establish production readiness or
real semantic/fused model quality.

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
- Thin end-to-end RAG MVP:
  - Use synthetic `InspectionRecord` data.
  - No dependency on final GSD handoff/acknowledgement clarification.
  - Do not build chatbot behavior.
  - Return source records with the summary.

## RAG Feature Boundaries

The RAG feature should follow this shape:

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
  - `test(rag): cover seeded pressure retrieval`
  - `chore(seed): add synthetic maintenance records`
  - `feat(rag): add maintenance review MVP endpoint`
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
- provider-neutral AI/embedding services

Some old manuscript diagrams may still say Django/PostgreSQL or hard-lock a specific AI provider. Those are being corrected and are not the target stack.

If generating docs or diagrams to accompany code, use ASP.NET Core + SQL Server terminology.
