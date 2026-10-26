---
id: TEST-153
type: test-run
title: Capstone remediation Phase 1 verification and blockers
status: executed
recordedAtUtc: 2026-10-25
testedCommit: ca81bf8a95b3b287f484807cd296e2466766dda2
additionalTestedCommit: 9b143d12e13864c351a0e2defe54ea8b8b65f122
sourceBranch: feature/capstone-remediation
evidenceLevel: locally-executed
---

# Phase 1 verification and blockers

## Corrections

Backend commit `ca81bf8a95b3b287f484807cd296e2466766dda2` adds an isolated
pending-review fixture and a frozen reviewed-corpus acceptance case. The real
corpus, approved labels, and manifest hashes remain unchanged. The fixture
changes one ledger decision while retaining a hash-valid frozen manifest, so
the review gate is tested independently of the freeze gate.

The same commit caches independent month-abbreviation regexes. Their timeout
and matching semantics remain unchanged. An overlapping-month regression
checks that `Jan 2026 Feb` remains ambiguous.

Web commit `87cb905c86f3aa80638cd5fd00d032d9ea4c25f9` moves generic-filter
derived values outside a render IIFE. Follow-up commit
`9b143d12e13864c351a0e2defe54ea8b8b65f122` guards the Back navigation's
array lookup and adds a catalog-option wait to the new UI regression.

## Commands and actual outcomes

Web commands run from `web` under Node 22.23.4. Commands ran separately.

| Command | Source identity | Actual result |
|---|---|---|
| `dotnet test UniPM.slnx --logger "trx;LogFileName=phase1.trx" --results-directory artifacts/remediation/backend` | `ca81bf8` | Exit 0; 647 total, 597 passed, 0 failed, 50 skipped; 2 minutes 35 seconds |
| `npm run format:check` | `87cb905` | Exit 0 |
| `npm run lint` | `87cb905` | Exit 0 |
| `npm run typecheck`, first attempt | `87cb905` | Exit 2; unchecked indexed navigation value |
| `npm run api:contract:check` | `0bc5701c377f6311ca3ed45eb5afb6ca555ef98d`, documentation-only descendant | Exit 0 |
| `npm run api:contract:test` | `0bc5701` | Exit 0; seven negative cases rejected as expected |
| `npm run test:run` | Diagnostic working source, see below | Exit 1; 31 files, 24 passed and 7 failed; 256 cases, 246 passed and 10 failed |
| `npm run typecheck`, focused correction rerun | `9b143d1` | Exit 0 |
| `npm run test:run -- src/features/reports/pm-analytics-v3.test.tsx -t "keeps a generic clarification filter in the preview plan" --maxWorkers=2` | `9b143d1` | Exit 1; 1 failed, 18 deselected |
| `npm run build` | `9b143d1` | Exit 0; includes TypeScript and Vite build |
| `npm run e2e` | `9b143d1` | Exit 1; 61 cases, 53 passed, 7 failed, 1 skipped |

The full Vitest attempt's start metadata identifies `0bc5701`, and an
uncommitted callback guard was present during that run. End-of-run source and
test SHA-256 values are retained in its log. Those end hashes do not establish
that every module executed exactly those bytes. Treat this attempt as a
diagnostic result, not acceptance evidence for an exact committed source tree.
The guard was reverted before the later committed correction.

Playwright's default port was 5173 and `CI` was unset. The executor checked
the `[WebServer]` child and listener identities, which matched the spawned
worktree Vite process. No reused unrelated server is asserted. Use an isolated
port explicitly for a future run.

## Unresolved failures

The new generic-filter test uses `makeCatalog()`, whose `filterFields` is
empty. Neither immediate lookup nor waiting can produce the Department
option. The test fixture needs an explicit Department filter definition;
its preview-plan assertions should remain unchanged. The single focused
rerun failed, so the repository retry limit was reached. A request for one
additional fixture correction and focused run is pending in the main chat.

The other nine full-unit failures concern existing shell access, searchable
picker, asset filtering, inspection filtering, login and form-review tests.
Some timed out; others reported missing or mismatched elements. Host memory
pressure was observed, but this does not establish a common cause for all
nine failures. They remain unresolved. Browser failures also remain unresolved.

## Artifacts and limits

Ignored raw files are `artifacts/remediation/backend/phase1-test.log`,
`phase1.trx`, and `artifacts/remediation/clients/phase1-web-*.log`.
Historical baseline TEST-152 is preserved separately.

No remote CI run, native SQL Server verification, real-provider request,
physical-device check, deployment, or institutional acceptance occurred.
Backend verification passed; frontend CI acceptance is not established.
Later-phase audit notes are source inspection only, not completed remediation.
