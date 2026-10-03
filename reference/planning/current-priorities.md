# Current Priorities - PMIS Validation Baseline

Read `AGENTS.md` first. These priorities apply to the
`refactor/retire-maintenance-history-rag` branch.

## Milestone status

M1 and M2 are finished and merged. [`TEST-040`](../evidence/test-runs/TEST-040-mobile-core-pm-physical-acceptance.md)
records the mobile/core PM physical-device acceptance for M2; it does not
verify the M2 web dashboard. M3 is current. This document tracks the PMIS-only
GSD validation work for M3.

The active priority order is now:

1. keep the branch runnable;
2. preserve the accepted core PM lifecycle and its evidence;
3. prepare the GSD workflow validation session;
4. collect missing exact form/report/workflow requirements;
5. record findings;
6. defer all optional capability and innovation work until separately approved.

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

The canonical PM batch is `Department + Asset Category + PmCycle`; building
does not split a batch. PM execution completion comes from
`InspectionRecord.CompletedAt`. Acknowledgement is a separate receipt/noting
step, does not alter execution or compliance timestamps, and does not complete
schedules.

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
- Maintenance-history RAG runtime, tooling, tests, and projection storage: retired.
- Historical RAG experiments and benchmarks: retained as evidence.

- Domain contracts: done for stable categories, statuses, schedule codes, and
  seed-only actor tokens, with canonical storage and SQL Server migration checks.
- ReferenceDocument Full-Text Search and shared embedding foundation: retained.
- Observability: opt-in HTTP/runtime metrics retained; maintenance RAG metrics retired.

- Engineering-evidence workflow: complete with source-inspected chronology,
  architecture decisions, a fresh backend test record, and an executed lexical
  baseline.
- Source-bounded maintenance review and summarization: runtime implementation
  retired; historical design, API descriptions, and evidence remain.
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
  bounded logout behavior, and focused SQL Server verification. Browser
  authentication integration is implemented and merged.
- Database platform baseline: native Windows SQL Server 2019 with Full-Text
  Search and compatibility level `150` is the minimum supported platform.
  SQL Server 2025 Docker tooling is optional and historical development support
  only; IIS deployment readiness remains unverified.
- Reference-document foundation: implemented and merged as a fictional,
  source-traceable metadata and sectioning foundation. Approved institutional
  source authorization and ingestion remain pending. OEM retrieval is excluded
  from the evaluated MVP.
- Confirmed preventive-maintenance workflow: complete in the backend. One form
  contains multiple inspection rows; its lifecycle is `Draft -> Submitted ->
  Acknowledged`. Field-work completion is recorded on each
  `InspectionRecord.CompletedAt` and completes linked schedules before form
  submission. Acknowledgement records separate receipt/noting, does not alter
  execution or compliance timestamps, and does not complete schedules. It makes
  completed rows eligible for acknowledged-only official history. It does not
  activate maintenance-history retrieval, which has been retired. Corrective-action
  handoff
  preparation ends before manual WMS encoding; UniPM does not process RMRFs or
  integrate with the WMS.
- RAG-assisted inspection-history analysis: planned in the previous phase,
  never implemented, and no longer an active direction. Its design record is
  preserved unchanged in
  [`rag-assisted-inspection-history-analysis.md`](rag-assisted-inspection-history-analysis.md).
- Schema-constrained natural-language analytics is a planned post-validation
  direction, pending professor/adviser confirmation. It is not implemented or
  approved; any implementation requires a separate approved task and branch
  after GSD validation.
- Flutter mobile field workflow: implemented and merged in the partner-owned
  workstream, including memory-only authentication, QR-based asset entry,
  acknowledged-only official asset history, the four supplied authoritative
  visible category-form structures, the multi-row Draft form workflow,
  whole-form submission, submitted-form review, mobile whole-form
  acknowledgement with signatory capture, UX hardening, and release-boundary
  checks. The core lifecycle passed physical-device acceptance against the
  live development backend. Production signing and distributable-release
  verification remain outside this milestone. Broader category-procedure,
  requiredness, and historical form-version decisions remain separately
  approved or GSD-validated work; attachments, alerts, offline synchronization,
  persistent session restoration, and AI/RAG remain deferred and are not
  blockers.

The active boundary for this branch is documented in
[`mvp-definition.md`](mvp-definition.md): the PMIS-only GSD validation
baseline. The retired `POST /api/v1/maintenance-review` route is absent from the
runtime, including when a legacy enable flag is configured. Its historical
description and evidence remain in the repository.

## Immediate Task Order

The validation phase proceeds in this order:

1. Keep the branch stable: restore/build/test green after any change.
2. Preserve the completed AI-independent core PM lifecycle and acceptance
   evidence.
3. Prepare the GSD demonstration environment and demo script.
4. Run the GSD validation session and capture answers.
5. Record requirements and limitations in the GSD validation note.
6. Defer optional capability and innovation selection until GSD findings
   justify a separate approved decision.

Mobile remains a separate partner-owned workstream; later mobile field
capabilities are not a blocker for this sequence.

## Historical Evidence Roadmap (Completed Phase)

The records below document completed evidence phases. New evidence work is
deferred until a post-GSD decision.

1. Authorization and ingestion requirements for institutional procedures,
2. Institutional source persistence, applicability, sectioning, lifecycle, and
   provenance using only approved sources.
3. Separate institutional lexical and semantic retrieval after source approval.
4. Keep institutional and maintenance-history sources distinct. OEM retrieval,
   combined-source fusion, and combined synthesis are excluded from the
   evaluated MVP.

EXP-003 executed a local offline Granite multilingual embedding baseline on
the fictional 24-query maintenance fixture. It is controlled development
evidence only; it does not establish real institutional performance or make
Granite a required deployment dependency.

During the earlier implementation, the maintenance-review endpoint was
disabled by default and required authorization when enabled. Real semantic and
fused model-quality evidence remained pending; EXP-002 did not change those
limits. That endpoint was distinct from the RAG-assisted inspection-history
analysis proposal, which was never implemented and is no longer active.

## Historical Risk-First Order (Implementation Phase, Completed)

The implementation phase followed a risk-first order, retained here as a
record that the preserved infrastructure was built incrementally with
verification at each step. Detailed task records follow below.

## Completed Historical Work Records

Tasks 0-6 and the engineering-evidence workflow below record finished
implementation phases from earlier branches. They describe what was built,
not active work.

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
references. Page 2, completed samples, and final institutional reference lists
remain provisional. The confirmed digital acknowledgement and corrective-handoff
boundaries do not make unseen physical-form fields a final production contract.

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

Inspection list/detail reads must preserve the confirmed form lifecycle: Draft
and Submitted form rows remain outside official history. Acknowledged form rows
are eligible for official history. The former retrieval eligibility behavior is
historical and inactive.

## Task 3 (Historical, Retired): Maintenance Issue Lexicon And Search Document

The earlier task aimed to normalize maintenance language before retrieval work
became provider- or model-dependent. Its lexicon, projection, and retrieval
implementation have since been retired.

Earlier completed scope included:

A small versioned JSON lexicon from the synthetic fixture and visible form
vocabulary, inspectable English/Tagalog/Taglish aliases,
required category-bounded matching, deterministic scoring, and narrow negation
handling. Evaluation labels stayed outside the resource and runtime search
content.

The retired projection used approved operational source fields. Evaluation
labels remained outside the projection and runtime search content.

The former projection stored one document per inspection, derived issue keys
from remarks using lexicon v1.0, retained recommendations as searchable text,
tracked source and asset timestamps, and supported transactional rebuilds. The
lexical SQL Server FTS retriever searched this projection with bounded
controlled filters and source-traceable results. The semantic channel cached
one normalized embedding per document, invalidated stale rows, and ranked
bounded SQL candidates with application-layer cosine similarity. Query
embeddings were transient.

The lexicon was not a diagnosis system and did not establish official GSD
wording.

## Task 4 (Historical, Completed): Thin Retrieval MVP

Historical, completed during the previous implementation phase; preserved
inactive on this branch. Do not extend or revive it without a separate
approved decision.

The implemented pipeline followed:

`current finding -> retrieval -> source selection -> sanitization -> source-bounded summary -> source display -> human verification`

Implemented behavior included lexical and semantic channels behind
`IEmbeddingService`/`ISummaryService`, the internal SQL Server FTS channel
over `MaintenanceSearchDocument.SearchText`, internal RRF orchestration with
explicit semantic degradation, returned source records with stated
limitations, inspectable source selection and prompt construction, and
sanitizer tests before any external provider call.

The `POST /api/v1/maintenance-review` endpoint implemented this bounded loop
with a maximum of two fused passes, four deterministic context tiers, explicit
evidence and summary statuses, request-scoped token masking, and source
records returned for human verification. It stayed disabled by default and
required `CanReviewMaintenanceHistory` whenever enabled. It was the
implemented review/summarization contract only; broader recurring-finding,
condition-frequency, time-comparison, cross-asset, distribution, and timeline
analysis remained planned and was never implemented.

Optional provider thinking mode and a strict 12-case DeepSeek V4 summary
experiment manifest were implemented. EXP-002 was executed with a real-provider
run, retained fictional outputs, developer-approved human ratings, and latency
evidence. It did not establish production readiness; Tagalog and Taglish
language fit remained weak, and five outputs violated the citation contract.

Core maintenance workflows always worked with AI disabled; no separate vector
database may be introduced. Chatbot behavior, autonomous decisions, automatic
corrective handoffs, raw prompt persistence, token-map persistence, and
unsupported claims about dates, causes, RMRF values, or personnel decisions
remain prohibited everywhere in the codebase.

## Task 5 (Historical, Completed): Retrieval Evaluation Benchmark

The task measured lexical, semantic, and fused retrieval on the fictional
dataset.

Completed scope included:

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

The benchmark project and runner have since been retired with maintenance
retrieval. Historical reports and approved evidence remain in the
[evidence archive](../evidence/INDEX.md); no current benchmark command is
available.

The benchmark evaluated retrieval separately from the former maintenance-review
context-selection, sanitization, and source-bounded summarization path. RRF did
not combine raw lexical and semantic scores.

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

- The four supplied forms are the authoritative visible field structure for
  this milestone. A `Page 1 of 2` notation is document-control context and was
  not treated as evidence of a missing content page.
- Official completed samples, official location lists, schedule authority,
  final audit rules, and authorized institutional-reference sources remain
  future validation topics. The digital form lifecycle, whole-form
  acknowledgement, and corrective-handoff boundary are confirmed.
- The operational fixture is fictional and provisional, not a production import
  contract.
- Evaluation annotations are test-only and never runtime operational data.
- Maintenance-history lexical/semantic retrieval, its degradation path, and
  derived storage were retired. The separate ReferenceDocument foundation and
  shared provider-neutral embedding components remain.
- SQL Server 2019 remains the supported relational and Full-Text Search store.
  Do not introduce a separate vector database or require native SQL vector
  features for the PMIS baseline.
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
8. Completed: `feat/web-schedules`.
9. Completed: `feat/web-inspections` read-only inspection review.
10. Completed: read-only preventive-maintenance form review.
11. Completed: `feat/mobile-foundation` authentication foundation.
12. Completed: `feat/mobile-pm-form-drafts` initial mobile Draft form workflow
    and whole-form submission.
13. Completed: user-facing web acknowledgement capture for Submitted forms.
14. The next mobile field workflow capability requires explicit approval.
15. Current: `feature/pm-acknowledgement-review` / M3 - the PMIS-only GSD
    validation baseline (this branch). Any innovation branch requires a
    separate approved decision after GSD validation.

EXP-003 completed a local offline Granite multilingual embedding baseline on
the fictional maintenance fixture. Its conditional result is development
evidence only and does not establish real institutional performance.
