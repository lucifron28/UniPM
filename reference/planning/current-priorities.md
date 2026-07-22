# Current Priorities - Active Work Items

Read `AGENTS.md` first. These tasks assume its architecture, privacy, scope,
and retrieval-safety rules.

The current strategy is risk-first:

1. keep the project runnable and tested;
2. preserve reproducible synthetic data and evidence;
3. preserve inspection-submission integrity;
4. preserve the organized retrieval and test layout without changing behavior;
5. preserve explicit documentation of known sanitizer limitations;
6. evaluate real multilingual embedding models;
7. preserve the bounded maintenance-history review contract;
8. keep final RBAC and institutional workflow rules deferred.

The RAG feature is not a chatbot and not an autonomous diagnostic tool. It is a
bounded maintenance-history review feature that retrieves source records and
helps a human verify them.

## Current Status

- Backend baseline: done. Restore, build, and the backend test suite pass.
- Initial migration: done for `Asset`, `PreventiveMaintenanceSchedule`, and
  `InspectionRecord`.
- Asset reads: done. Create, list, detail, and QR lookup are available.
- Schedule reads: done. Create, list, and detail are available.
- Inspection history: done.
- Inspection list/detail: done.
- Operational synthetic fixture: completed at version `1.1.0`.
- Development seed/reset commands: completed and Development-only.
- Retrieval evaluation manifest: completed at version `1.1.0` and test-only,
  with 24 bounded benchmark queries.
- Internal RRF fusion: complete as bounded, deterministic, inspectable
  orchestration; real fused quality evidence remains pending.
- Maintenance issue lexicon: done at version `1.0.0`.
- `MaintenanceSearchDocument`: done as a persisted, rebuildable projection.
- Domain contracts: done for stable categories, statuses, schedule codes, and
  seed-only actor tokens, with canonical storage and SQL Server migration checks.
- SQL Server FTS retrieval: complete as an internal service over
  `MaintenanceSearchDocument.SearchText`. It does not expose a standalone public
  search endpoint; fused retrieval feeds the bounded maintenance-review endpoint.
- Semantic retrieval: complete as an internal channel over cached
  `MaintenanceSearchDocument` embeddings; its provider is operationally
  optional and degradable. It remains internal while the authenticated review
  endpoint consumes fused retrieval when enabled.
- Retrieval benchmark: lexical baseline executed and preserved; semantic and
  fused orchestration are implemented and deterministically tested, while real
  semantic/fused model-quality evidence remains pending a configured provider.
- Observability metrics: complete with opt-in `/metrics`, bounded custom
  instruments, optional local Prometheus/Grafana provisioning, and TEST-002
  local Docker evidence. Production monitoring remains unclaimed.
- Engineering-evidence workflow: complete with source-inspected chronology,
  architecture decisions, a fresh backend test record, and an executed lexical
  baseline.
- Source-bounded maintenance review and summarization: complete as an
  authenticated, source-returning, provider-neutral MVP when explicitly
  enabled.
- Authentication scaffolding: complete with IdentityCore, JWT bearer access
  tokens, five provisional roles, Development user seeding, and policy-
  protected operational writes.
- Inspection-submission integrity: complete with schedule-level SQL Server
  uniqueness and conflict handling.
- Retrieval and API test layout: complete without behavior changes.
- MVP sanitizer free-text-name limitation: explicitly documented; pattern-based
  masking does not generally identify personal names in free text.
- Browser-ready refresh-session contract: complete with short-lived JWT access
  tokens, rotating hash-only refresh sessions, exact-origin credentialed CORS,
  bounded logout behavior, and focused SQL Server verification. Web integration
  remains deferred.

## Immediate Task Order

1. Completed: `fix/inspection-submission-integrity`.
2. Completed: retrieval and test folder organization refactor.
3. Completed: explicit documentation of the MVP sanitizer's free-text-name
   limitation.
4. Completed: `feat/auth-refresh-sessions`.
5. Completed: `feat/web-foundation`.
6. Completed: `feat/web-auth-integration`.
7. Completed: `feat/web-assets`.
8. Next: `feat/web-schedules`.

The multilingual embedding baseline remains deferred pending a configured real
provider.

The maintenance-review endpoint remains disabled by default and requires
authorization when enabled. Real semantic and fused model-quality evidence
remain pending; EXP-002 does not change those limits.

## Risk-First Order

1. Confirm the backend baseline.
2. Preserve engineering evidence for implementation and verification.
3. Keep the synthetic fixture and Development-only seeder verified.
4. Preserve the executed lexical baseline and keep semantic model-quality
   verification explicitly pending a configured real provider.
5. Preserve the completed observability evidence and its production limits.
6. Preserve inspectable RRF fusion and keep real fused quality evidence pending
   until a provider is configured.
7. Preserve the source-bounded review contract and its explicit limitations.
8. Harden the backend MVP without expanding provisional role or workflow rules.

## Task 0: Project Boot And Baseline Check

Goal: keep the current backend state known before each risky change.

Completed evidence:

- `AGENTS.md`, project memory, reference forms, and existing code were read.
- The current entity, endpoint, service, DbContext, migration, and test styles
  were inspected.
- The solution restores, builds, and tests successfully.
- The current route prefix, DTO conventions, and test conventions are known.

Maintain this baseline after meaningful backend changes with:

```powershell
dotnet restore .\UniPM.slnx
dotnet build .\UniPM.slnx
dotnet test .\UniPM.slnx --no-build
```

Do not change the stack or introduce competing endpoint patterns while doing
baseline work.

## Task 1: Synthetic Fixture And Development Seeder

Goal: provide reproducible fictional records for API, retrieval, and frontend
development without importing real institutional records.

Completed scope:

- 20 assets, 34 schedules, and 30 inspections across the four selected
  categories.
- Recurring same-asset findings, similar-asset context, building context,
  English, Tagalog, Taglish, and distractor records.
- Four cold-start assets with Due schedules and no inspection history.
- Deterministic IDs, timestamps, seed keys, and helper-derived asset QR values.
- Development-only explicit seed and reset commands.
- Deterministic fixture-owned upsert behavior and scoped reset behavior.
- Preflight validation before writes, including references, statuses, counts,
  QR values, actor roles, synthetic labels, and sensitive-data patterns.
- Reset dependency protection for non-fixture schedules and inspections.
- Strict rejection of unmapped JSON properties.

The operational fixture retains operational metadata, actors, assets, schedules,
inspections, category details, form data, remarks, and recommendations. It does
not contain evaluation labels. The evaluation manifest is test-only and must not
be loaded by runtime code, persisted, indexed, embedded, placed in prompts, or
returned through ordinary API DTOs.

Limitations remain explicit: the source forms are blank visible Page 1
references. Page 2, completed samples, acknowledgement workflow, RMRF rules,
and final institutional reference lists remain provisional pending GSD/adviser
clarification.

## Task 2: List/GET Endpoints For Existing Entities

Goal: give the web and mobile clients stable read-side contracts.

Completed:

- asset create, list, detail, and QR lookup;
- schedule create, list, and detail;
- inspection submission and asset-history lookup.

Completed implementation:

- `GET /api/v1/inspections`
- `GET /api/v1/inspections/{id}`
- preserve the existing asset-history route and conventions;
- add pagination or filtering only where it matches the existing API style;
- add happy-path and meaningful failure-case tests.

Do not finalize acknowledgement, handoff, RMRF, or other deferred workflow rules
as part of inspection list/detail reads.

## Task 3: Maintenance Issue Lexicon And Search Document

Goal: normalize maintenance language before retrieval work becomes provider- or
model-dependent.

With inspection list/detail and lexicon v1.0 complete:

Completed scope includes a small versioned JSON lexicon from the synthetic
fixture and visible form vocabulary, inspectable English/Tagalog/Taglish aliases,
required category-bounded matching, deterministic scoring, and narrow negation
handling. Evaluation labels remain outside the resource and runtime code.

The projection uses only approved operational source fields. Evaluation labels
remain outside the projection and all runtime search content.

The projection is now persisted one-per-inspection, derives issue keys from
remarks using lexicon v1.0, retains recommendations as raw searchable text,
tracks source and asset timestamps, and supports explicit transactional rebuild.
Lexical SQL Server FTS now searches only this projection through an internal
bounded retriever with controlled metadata filters and source-traceable results.
The semantic channel now caches one normalized embedding per document,
invalidates stale rows, and ranks bounded SQL Server candidates with
application-layer cosine similarity. Query embeddings are transient, and the
evaluation manifest remains outside runtime code.

Do not treat the lexicon as a diagnosis system or invent official GSD wording.

## Task 4: Thin Retrieval MVP

Goal: preserve and validate the implemented bounded retrieval and maintenance-
review pipeline while improving integrity, organization, and multilingual
model-quality evidence.

Required shape:

`current finding -> retrieval -> source selection -> sanitization -> source-bounded summary -> source display -> human verification`

Do:

- retrieve related records before generation;
- use same-asset, same-category, and available location/building context;
- implement lexical retrieval separately from semantic retrieval;
- keep embeddings behind `IEmbeddingService` and summaries behind
  `ISummaryService` or equivalent interfaces;
- use the completed internal SQL Server FTS channel over
  `MaintenanceSearchDocument.SearchText`;
- use the completed semantic channel separately and through the completed
  internal RRF orchestration, with semantic degradation reported explicitly;
- return the source records used and limitations beside any summary;
- keep source selection and prompt construction inspectable;
- add sanitizer tests before any external provider call.

The `POST /api/v1/maintenance-review` endpoint now implements this bounded
loop with a maximum of two fused passes, four deterministic context tiers,
explicit evidence and summary statuses, request-scoped token masking, and
source records returned for human verification. It remains disabled by default
and requires `CanReviewMaintenanceHistory` whenever enabled.

Optional provider thinking mode and a strict 12-case DeepSeek V4 summary
experiment manifest are implemented. EXP-002 is executed with a real-provider
run, retained fictional outputs, developer-approved human ratings, and latency
evidence. The result does not establish production readiness; Tagalog and
Taglish language fit remained weak, and five outputs violated the citation
contract.

Semantic retrieval is a required target channel, not an excuse to block core
maintenance workflows. Core workflows must work with AI disabled. No separate
vector database may be introduced.

Do not build chatbot behavior, autonomous decisions, automatic corrective
handoffs, raw prompt persistence, token-map persistence, or unsupported claims
about dates, causes, RMRF values, or personnel decisions.

## Task 5: Retrieval Evaluation Benchmark

Goal: measure lexical, semantic, and fused retrieval on the
fictional dataset.

Completed scope:

- versioned test-only manifest `1.1.0` with 24 bounded queries;
- English, Tagalog, and Taglish coverage across all four asset categories;
- expected relevant inspection IDs, filters, cold-start context, distractors,
  and scenario slices;
- strict loader validation against the operational `1.1.0` fixture;
- standalone SQL Server runner with temporary database, migration, seed,
  projection rebuild, Full-Text readiness polling, optional semantic indexing,
  and deterministic JSON/Markdown reports;
- Hit@1, Hit@5, Precision@5, Recall@5, Recall@10, reciprocal rank, first
  relevant rank, macro averages, and language/category/scenario slices.

Run lexical, semantic, fused, or valid channel combinations with
`tools/UniPM.RetrievalBenchmark`. Semantic and fused execution require the
configured embedding provider; it is not replaced with fake production scores.

The benchmark remains separate from the maintenance-review context-selection,
sanitization, and source-bounded summarization path. RRF does not combine raw
lexical and semantic scores.

Do not claim synthetic benchmark performance proves production performance.

## Engineering Evidence Workflow

Completed scope:

- root and nested evidence instructions;
- handbook, stable record IDs, front matter, templates, and index;
- source-inspected implementation chronology for the fixture, lexicon, lexical
  retrieval, semantic retrieval, and benchmark;
- source-inspected ADRs for SQL Server Full-Text Search and provider-neutral
  embeddings;
- Windows-first backend verification capture script with safe metadata, logs,
  TRX parsing, optional SQL/benchmark stages, summaries, and SHA-256 hashes;
- current local TEST-001 baseline, executed EXP-001 lexical baseline, and
  executed TEST-002 observability baseline;
- source-inspected IMP-006 and ADR-003 records for optional local monitoring;
- deterministic semantic orchestration tests are present, while a real semantic
  model-quality baseline remains pending a configured provider.

The current records do not claim real semantic-provider execution, semantic
model quality, independent lexicon accuracy, CI success, IIS deployment,
production monitoring, alert effectiveness, or long-term retention. New
experiments receive new IDs and approved baselines are not overwritten.

## Task 6: Authentication And Client Contract Notes

Authentication and the initial browser client contract are implemented:

- the five approved development roles are scaffolded: Admin, GSD, Inspector,
  DepartmentHead, and Supervisor;
- keep JWT secrets out of committed configuration;
- policy-protect operational writes and keep the implemented read contracts
  usable for authenticated development;
- preserve tests for login, refresh rotation, protected routes, rejected
  unauthenticated writes, and allowed writes;
- keep browser access tokens in memory, restore sessions through the backend's
  HttpOnly refresh cookie, and keep current-user server state in TanStack Query.

Final institutional RBAC decisions, MFA, SSO, registration, password recovery,
and operational client modules remain deferred.

Document only implemented routes in tracked API contract notes. Web handles
administration, monitoring, reporting, review, and source verification. Mobile
handles QR lookup, assigned schedules, checklist completion, and inspection
submission. Neither client calls SQL Server, an embedding provider, or an LLM
directly.

## Current Constraints

- The four physical forms are blank Page 1 references only.
- Page 2, acknowledgement, RMRF, official location lists, schedule authority,
  final audit rules, and the approved reference knowledge base are deferred.
- The operational fixture is fictional and provisional, not a production import
  contract.
- Evaluation annotations are test-only and never runtime operational data.
- Semantic retrieval is required but may degrade to an explicitly reported
  lexical fallback when embeddings are unavailable.
- SQL Server remains the relational, FTS, and future vector-search store. Do not
  introduce Pinecone, Qdrant, Weaviate, Chroma, Milvus, or another vector DB.
- No LLM output may approve, diagnose, change status, create a handoff, or make
  an official maintenance decision.

## Next Branches

1. Completed: `fix/inspection-submission-integrity`.
2. Completed: retrieval and test folder organization refactor.
3. Completed: explicit documentation of the MVP sanitizer's free-text-name
   limitation.
4. Completed: `feat/auth-refresh-sessions`.
5. Completed: `feat/web-foundation`.
6. Completed: `feat/web-auth-integration`.
7. Completed: `feat/web-assets`.
8. Next: `feat/web-schedules`.

The multilingual embedding baseline remains deferred pending a configured real
provider.
