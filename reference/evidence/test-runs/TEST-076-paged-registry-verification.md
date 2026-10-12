---
id: TEST-076
type: test-run
title: Paged registry verification status
status: executed
verificationOutcome: unavailable
reviewReadiness: BLOCKED
recordedAtUtc: 2026-10-11T07:57:12Z
testedCommit: 11885e50fef91dcd00b510c87868ee93098fc554
sourceBranch: feature/registry-search-filtering
evidenceLevel: locally-executed
---

# Paged registry verification status

## Objective

Record the available verification attempt for the web and API registry
changes. The backend command started, but the focused gate produced no test
result. `status: executed` records the attempt; `verificationOutcome:
unavailable` and `reviewReadiness: BLOCKED` make clear that no test pass is
established.

## Execution Identity

- The focused backend command was started at
  `11885e50fef91dcd00b510c87868ee93098fc554` on
  `feature/registry-search-filtering`.
- `npm run typecheck` passed at
  `12895619b6b24f451c7374fc817e758f1e6817b4`.
- The first captured `npm run typecheck` run was at
  `60850dad41e765a484a5ae46e2af060be2b6bc29` and exited 2. The matching
  retry after one scoped correction passed at the commit above.
- The generated API contract was committed at
  `10adc61744be8cf659793d4ca4fffa2079523c11` from the current branch API
  OpenAPI response and the established web generator.
- A missing closing parenthesis in the schedule-registry predicate was fixed
  at `9c103f1fe221d1fbda07bfd34e13015a42de68a0` before the generated contract
  was exported.

## Environment

- Windows PowerShell worktree; web tooling ran with installed Node.js 24.
  Node.js 22 was unavailable locally; the repository CI uses Node.js 22.
- The OpenAPI document was fetched from an owned local Development API process.
  The generated-client command used the checked-in `web/openapi/unipm-v1.json`
  contract and `npm run api:generate`; the generator was not bypassed and
  generated files were not hand-edited. `npm run api:contract:check` and
  `npm run api:generate` completed at `10adc61`.
- No SQL Server or external AI provider was used.

## Commands

- In `web/`: `npm run api:generate`.
- In `web/`: `npm run api:contract:check`.
- In `web/`: `npm run typecheck` at the two commit identities listed above.
- From the worktree root:
  `dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --filter "FullyQualifiedName~RegistryReadEndpointsTests|FullyQualifiedName~ScheduleQueryEndpointsTests"`
- A changed-file Prettier pass ran before the typecheck attempt. A full-repository
  formatting check was not run.

## Results

- The local API OpenAPI response was saved under ignored
  `artifacts/registry-tooling/openapi.json`, copied into the web contract
  snapshot, and processed by the established generator. The contract check and
  generator completed at `10adc61`; the generated contracts are committed.
- The first actual TypeScript check reported four diagnostics: one optional
  asset-fixture field mismatch, one unused inspection response import, and
  exact-optional-property errors at the inspection and schedule asset picker
  call sites. The scoped correction was committed as `1289561`; the same
  `npm run typecheck` command then exited 0.
- After that pass, the schedule year-option correction (`ec5b856`) and the
  `overallCount` strict-schema/mocks correction (`870b4b9`) were source-reviewed
  without another local typecheck, as required by the bounded verification
  instruction.
- The focused backend invocation emitted no test output or TRX result. Host
  inspection found that its root dotnet process had exited while child worker
  processes remained and the command stream stayed silent. The active shell was
  stopped with Ctrl+C; the tool returned executor exit code 1, which is not a
  dotnet test exit code. No test count or pass/fail result is available, and the
  command was not retried.
- The fixture source contains three focused endpoint tests for 1,000-asset
  pagination/search/options/summary and legacy array compatibility; schedule
  pagination, canonical quarter, and Inspector scope; and official inspection
  visibility and linked PM-cycle filtering. Fixture execution and results were
  not established by this attempt.

## Test Counts

- TypeScript compiler: one failed run at `60850da`, then one passing retry at
  `1289561` after the scoped correction.
- Focused backend tests: unavailable; no test host result, counts, or TRX was
  produced.
- Web unit tests, Playwright, and the full backend suite: not run locally for
  this change.
- CI: not run.

## SQL Server Verification

Not run. This record makes no SQL Server 2019 or database-performance claim.

## AI-Provider Verification

No provider was called. Provider behavior is outside this change.

## Generated Artifacts

- Ignored raw output: `artifacts/registry-tooling/web-typecheck-attempt-1.log`,
  `artifacts/registry-tooling/web-typecheck-attempt-2.log`, and
  `artifacts/registry-tooling/backend-registry-tests-11885e5.log`.
- Actual local API OpenAPI response: ignored
  `artifacts/registry-tooling/openapi.json`.
- No TRX file was produced for the backend attempt.

## Failures And Corrections

- An earlier output-capture wrapper failed and did not establish a typecheck
  result. The corrected capture produced the failed TypeScript run described
  above; its scoped correction and one retry passed.
- The first schedule-registry source export attempt failed to compile because
  its predicate was missing a closing `)`. Commit `9c103f1` corrected it. A
  Node fetch to the local API timed out (`ETIMEDOUT`); an owned local API was
  then queried successfully with `curl --noproxy '*'`, and its actual response
  was passed through the repository's contract check and generator. Node.js 22
  parity was not checked locally.
- The backend gate remains blocked because the command produced no test result.
  It was stopped after host process inspection, and no retry was made.

## Skipped Verification

- CI, browser/device behavior, native SQL Server, production load, and deployment
  checks were not run. The synthetic 1,000-asset fixture is source coverage only;
  its execution/result was not established, and it is not a measured load result.

## Limitations

Review readiness remains pending until the focused backend gate returns a real
result and the later source-reviewed web corrections receive CI verification.
This record does not claim that the new API endpoints, strict frontend response
parsers, or registry interactions passed their tests.
