# Current Priorities - Active Work Items

Read `AGENTS.md` first. These tasks assume its architecture, privacy, scope,
and retrieval-safety rules.

The current strategy is risk-first:

1. keep the project runnable and tested
2. keep realistic synthetic data available for development
3. finish the read-side contracts needed by the clients
4. prove retrieval channels separately before fusion
5. add the bounded maintenance-history review loop
6. add authentication scaffolding after the core evidence path is stable

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
- Inspection list/detail: pending.
- Operational synthetic fixture: completed at version `1.1.0`.
- Development seed/reset commands: completed and Development-only.
- Retrieval evaluation manifest: completed at version `1.0.0` and test-only.
- RRF placeholder: removed; RRF is not implemented yet.
- Maintenance issue lexicon: next after inspection list/detail.
- `MaintenanceSearchDocument`: pending.
- SQL Server FTS retrieval: pending.
- Semantic retrieval: pending, but required as a target channel.
- Retrieval benchmark and fusion: pending.
- Source-bounded maintenance review and summarization: pending.
- Authentication scaffolding: pending.

## Risk-First Order

1. Confirm the backend baseline.
2. Keep the synthetic fixture and Development-only seeder verified.
3. Complete inspection list/detail endpoints.
4. Implement the maintenance issue lexicon.
5. Add a `MaintenanceSearchDocument` projection.
6. Implement lexical and semantic retrieval separately.
7. Benchmark retrieval channels.
8. Add inspectable result fusion.
9. Add sanitization and source-bounded summarization.
10. Add authentication scaffolding.

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

Next implementation:

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

After inspection list/detail is complete:

- define a small, versioned issue lexicon from the synthetic fixture and visible
  form vocabulary;
- keep English, Tagalog, and mixed-language aliases inspectable;
- add a `MaintenanceSearchDocument` projection only for approved operational
  source fields;
- keep evaluation labels outside the projection and all runtime search content;
- test normalization, category boundaries, and no-history behavior.

Do not treat the lexicon as a diagnosis system or invent official GSD wording.

## Task 4: Thin Retrieval MVP

Goal: prove retrieval before investing in advanced fusion or UI polish.

Required shape:

`current finding -> retrieval -> source selection -> sanitization -> source-bounded summary -> source display -> human verification`

Do:

- retrieve related records before generation;
- use same-asset, same-category, and available location/building context;
- implement lexical retrieval separately from semantic retrieval;
- keep embeddings behind `IEmbeddingService` and summaries behind
  `ISummaryService` or equivalent interfaces;
- use SQL Server FTS when ready, with a clearly reported lexical fallback when
  semantic embeddings are unavailable;
- return the source records used and limitations beside any summary;
- keep source selection and prompt construction inspectable;
- add sanitizer tests before any external provider call.

Semantic retrieval is a required target channel, not an excuse to block core
maintenance workflows. Core workflows must work with AI disabled. No separate
vector database may be introduced.

Do not build chatbot behavior, autonomous decisions, automatic corrective
handoffs, raw prompt persistence, token-map persistence, or unsupported claims
about dates, causes, RMRF values, or personnel decisions.

## Task 5: Retrieval Evaluation Benchmark

Goal: measure whether lexical, semantic, and hybrid retrieval improve on the
fictional dataset.

The test-only evaluation manifest currently contains four cold-start asset
annotations and one exact annotation for every operational inspection. It keeps
benchmark queries empty until the retrieval contract and lexicon are ready.

When the retrieval channels exist:

- add 15-30 English, Tagalog, and mixed-language benchmark queries;
- define expected relevant record IDs in test-only data;
- compare lexical, semantic, and hybrid results;
- report simple Hit@5, Recall@10, or MRR where useful;
- keep cold-start assets due and clearly label similar-asset fallback context.

Do not claim synthetic benchmark performance proves production performance.

## Task 6: Authentication And Client Contract Notes

Authentication follows the core inspection and retrieval read contracts:

- scaffold the five approved development roles: Admin, GSD, Inspector,
  DepartmentHead, and Supervisor;
- keep JWT secrets out of committed configuration;
- protect writes first and keep reads usable for authenticated development;
- add tests for login, rejected unauthenticated writes, and allowed writes.

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

- `feat/api-inspection-detail-endpoints`
- `feat/retrieval-maintenance-issue-lexicon`
- `feat/retrieval-search-document`
- `feat/retrieval-lexical-fts`
- `feat/retrieval-semantic`
- `test(rag): add retrieval benchmark`
- `feat/rag: add inspectable result fusion`
- `feat/rag: add source-bounded maintenance review`
- `feat/auth: scaffold development roles`
