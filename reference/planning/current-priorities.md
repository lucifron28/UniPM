# Current Priorities - Active Work Items

Read `AGENTS.md` first. These tasks assume those rules.

The current strategy is risk-first:

1. make the project run
2. seed realistic synthetic data
3. expose read endpoints for frontend work
4. prove the thin RAG loop early
5. add auth scaffolding
6. expand tests and refinement

The RAG feature is not a chatbot and not an autonomous diagnostic tool. It is a bounded, source-bounded maintenance-history review feature.

## Current Status

- Task 0: mostly done.
- Task 1: not done.
- Task 2: partially done.
  - Asset list/get/QR lookup: done.
  - Schedule list/get: done if `feat/api-schedule-query-endpoints` is merged.
  - Inspection detail/list/history: still pending.
- Task 3: blocked until synthetic seed data exists.
- Task 4: pending.
- Task 5: pending.
- Task 6: useful before frontend/mobile screen work.

## Task 0: Project Boot And Baseline Check

Goal: Confirm the current backend state before making changes.

Do:

- Read `AGENTS.md`.
- Run the backend locally using the existing documented workflow.
- Run the test suite.
- Inspect existing entity, endpoint, service, DbContext, and migration structure.
- Summarize what already exists for:
  - Assets
  - Schedules
  - Inspections
  - existing seed data
  - existing tests
  - existing authentication/authorization, if any
- Identify the current route prefix style, DTO style, and test style.
- Do not change architecture yet.

Do not:

- Do not add new patterns.
- Do not introduce a new folder structure unless clearly consistent with the current codebase.
- Do not change the stack.
- Do not touch AI providers yet.

Done when: the current project status is known, tests have been run, and the next task can be started without guessing the existing structure.

## Task 1: Seed Dataset For RAG And Frontend Development

Goal: Create realistic-enough synthetic maintenance history to support RAG MVP testing and frontend development.

Do:

- Write a reproducible seed script, migration seed, fixture, or dev-only seeding routine.
- Seed 20-30 synthetic `InspectionRecord` entries for the MVP.
- Cover the four asset categories:
  - fire extinguishers
  - fire alarm systems
  - emergency lights
  - water drinking stations
- Include:
  - recurring issues on the same asset
  - similar issues on similar assets
  - same-building/context records
  - pure-English remarks
  - pure-Tagalog remarks
  - mixed English/Tagalog remarks
  - at least 2 distractor records that should not be retrieved for unrelated findings
  - at least 1 cold-start scenario where an asset has no same-asset history
- Include issue phrases such as:
  - `mahina ang pressure`
  - `kulang ang pressure`
  - `pressure gauge below acceptable range`
  - `needs refill`
  - `hindi umiilaw`
  - `not lighting`
  - `sira battery`
  - `barado ang filter`
  - `clogged filter`
  - `may tagas`
  - `leaking`
- Use synthetic names only if names are needed.
- Avoid real personnel names, real phone numbers, real emails, or real institutional private data.
- Keep the seed data easy to reset and rerun.

Do not:

- Do not hand-insert rows manually in a way that cannot be reproduced.
- Do not use real GSD records unless explicitly approved.
- Do not add final acknowledgement/handoff/RMRF workflow schema in this task.

Done when: a fresh dev environment can load the seed data and Task 3 can run against it.

## Task 2: List/GET Endpoints For Existing Entities

Goal: Give the web/mobile frontends real read-side API contracts to build against.

Do:

- Add or confirm:
  - `GET /api/v1/assets`
  - `GET /api/v1/assets/{id}`
  - `GET /api/v1/schedules`
  - `GET /api/v1/schedules/{id}`
  - `GET /api/v1/inspections/{id}`
  - `GET /api/v1/inspections/history/{assetId}` or equivalent
- Add basic filtering where cheap and consistent:
  - assets by category
  - assets by status
  - schedules by date/status
  - inspections by asset
- Add pagination if list sizes could realistically exceed about 50 records.
- Match existing endpoint/service/DTO conventions.
- Add tests for list and get-by-id endpoints.

Do not:

- Do not add update/delete unless already planned.
- Do not finalize acknowledgement/handoff tables.
- Do not introduce a new API pattern if one already exists.

Done when: every existing entity needed by the frontend has list plus get-by-id/history reads, with tests.

## Task 3: Thin End-To-End RAG MVP

Goal: Prove the core retrieval-plus-summary loop works before investing in RRF, advanced rule scoring, full privacy masking, or UI polish.

Required input/output shape:

- Input:
  - `assetId`
  - current finding text / remarks
- Output:
  - source-bounded summary
  - list of source records used
  - limitations / evidence status where applicable

Do:

- Read `AGENTS.md`.
- Use the synthetic seeded data from Task 1.
- Add one maintenance-review endpoint, for example `POST /api/v1/maintenance-review`.
- Add request/response DTOs.
- Run a relational prefilter where applicable:
  - same asset
  - same category
  - same building/location if available
- Add basic SQL Server Full-Text Search or a temporary keyword search if FTS setup blocks the MVP.
- Add one embedding retrieval path behind `IEmbeddingService`.
- Pick the cheapest/fastest embedding candidate first:
  - local model
  - Gemini free tier
  - another approved free/prepaid option
- Cache embeddings for seeded records.
- Store embeddings in SQL Server or a reproducible local fixture for MVP.
- If SQL Server native vector search slows setup, compute cosine similarity in backend code first.
- Combine FTS/keyword and semantic results with the simplest inspectable approach:
  - union plus dedupe is okay for MVP
  - RRF comes later
- Select the top few source records.
- Add a minimal `PrivacySanitizerService` before any external LLM call.
- Add an LLM summary call behind `ISummaryService`.
- Use a fixed prompt template:
  - summarize only the provided records
  - do not invent details not present
  - do not invent dates
  - do not invent RMRF numbers
  - do not invent causes
  - do not invent corrective actions
  - include limitations
  - include assistive-only disclaimer
- Return the summary beside the source records used.
- Add tests for:
  - endpoint happy path
  - no source records / insufficient evidence
  - source records returned with summary
  - sanitizer masks email/phone/employee ID
  - no raw prompt persisted, if persistence exists

Do not:

- Do not build a chat interface.
- Do not auto-diagnose.
- Do not auto-change asset status.
- Do not auto-create corrective handoffs.
- Do not implement full RRF yet.
- Do not implement full rule-based scoring yet.
- Do not implement full token-map rehydration yet.
- Do not persist raw prompts.
- Do not persist token maps.
- Do not introduce a separate vector database.
- Do not hard-lock one AI provider in a way that is hard to swap.

Done when: given a seeded finding such as `mahina ang pressure`, the endpoint returns a plausible source-bounded summary and the records it was grounded in. It does not need to be excellent yet. It needs to exist, be inspectable, and be safe enough for synthetic MVP testing.

## Task 4: Auth Stub And Role Scaffolding

Goal: Add enough authentication/authorization to unblock frontend work. This is not the final RBAC matrix.

Do:

- Add a minimal JWT-based auth flow:
  - login endpoint
  - token issuance
- Seed 5 roles:
  - Admin
  - GSD
  - Inspector
  - DepartmentHead
  - Supervisor
- Add a dev user per role for local testing.
- Protect existing write endpoints behind authentication at minimum:
  - POST assets
  - POST schedules
  - POST inspections
- Coarse role restrictions are okay for now:
  - Admin + GSD can write
  - authenticated users can read, unless an existing rule says otherwise
- Add tests:
  - seeded user can log in
  - unauthenticated write request is rejected
  - authenticated write request works for an allowed dev role

Do not:

- Do not build the full permission matrix yet.
- Do not touch acknowledgement/handoff tables.
- Do not finalize source-record visibility rules yet.
- Do not put JWT secrets in committed config.

Done when: a seeded user can log in, get a token, and existing write endpoints reject unauthenticated requests.

## Task 5: RAG Retrieval Benchmark Harness

Goal: Create a small repeatable benchmark so we can tell if retrieval is improving.

Do:

- Add a test fixture or small console/test harness for retrieval evaluation.
- Use seeded records from Task 1.
- Create 15-30 benchmark queries first.
- Include English, Tagalog, and mixed variants:
  - `low pressure`
  - `mahina ang pressure`
  - `kulang ang pressure`
  - `pressure gauge below acceptable range`
  - `hindi umiilaw`
  - `not lighting`
  - `sira battery`
  - `barado ang filter`
  - `clogged filter`
  - `may tagas`
  - `leaking`
- For each query, define expected relevant record IDs.
- Compare:
  - keyword/FTS only
  - semantic only
  - hybrid union/dedupe
- If easy, report:
  - Hit@5
  - Recall@10
  - MRR
- Keep output simple and readable.

Do not:

- Do not chase perfect benchmark tooling.
- Do not add public benchmark dependencies unless necessary.
- Do not claim public benchmark performance proves UniPM performance.

Done when: one command or test run shows whether FTS, semantic, or hybrid retrieval is performing better on the seeded UniPM data.

## Task 6: Web/Mobile API Contract Notes

Goal: Speed up frontend/mobile work by documenting the current API contracts.

Do:

- Create or update a tracked API notes file, for example `reference/api-contracts.md`.
- Document:
  - auth endpoint
  - asset list/get
  - schedule list/get
  - inspection list/get/history
  - maintenance-review endpoint
- For each endpoint include:
  - route
  - method
  - request body, if any
  - response shape
  - auth requirement
  - notes for web/mobile usage
- Include web/mobile function split:
  - web handles management, reporting, review, source verification
  - mobile handles QR lookup, assigned schedules, checklist completion, inspection submission

Do not:

- Do not document imaginary final endpoints as if implemented.
- Do not include real secrets, tokens, or private data.
- Do not treat API notes as final manuscript text.

Done when: frontend/mobile agents can start building screens without reading backend code for every endpoint.

## Recommended Next Branches

- `feat/api-inspection-detail-endpoints`
- `chore(seed): add synthetic maintenance records`
- `docs(api): document current backend contracts`
- `feat(auth): scaffold dev role authentication`
