---
id: TEST-078
type: test-run
title: Asset registry active-filter chip regression
status: executed
verificationOutcome: passed
reviewReadiness: PENDING
recordedAtUtc: 2026-10-11T12:45:40Z
testedCommit: ac942c8b0a94127c13b4f6688eb073018a818745
sourceBranch: feature/registry-search-filtering
evidenceLevel: locally-executed
---

# Asset registry active-filter chip regression

## Local execution

The focused test ran at HEAD `ac942c8b0a94127c13b4f6688eb073018a818745`
with a working-tree change to `web/src/features/assets/asset-registry.test.tsx`.
That test change was subsequently committed as `b1222cbe76407a6dcfc4b42508e4b0400fa1fca0`;
the commit itself was not rerun. The test checks all five readable filter
chips, removes the Search chip, and verifies the input clears while remaining
filters are preserved and pagination resets to page 1.

| Commit context | Command | Result |
|---|---|---|
| `ac942c8` plus working-tree test change | `npm run test:run -- src/features/assets/asset-registry.test.tsx` | Exit 0; 1 file passed, 11 tests passed. |
| Same working-tree test change | `npx prettier --write src/features/assets/asset-registry.test.tsx` | Completed. |
| Same working-tree test change | `git diff --check` | Exit 0. |

The local runtime was Node.js `v24.15.0`. Raw Vitest output is at
`artifacts/registry-tooling/asset-registry-chip-test-ac942c8/vitest.log`.

## CI context

Web CI run `38138570471` on the prior exact commit `ac942c8` reported 209
passed and 1 failed. It predates this test correction; the failure was the
obsolete assertion for the raw category code in active-filter text. TEST-077
records earlier local formatting, lint, typecheck, and API contract checks as
passed; those checks were not rerun here. Exact-head Web and Backend CI for
`b1222cbe76407a6dcfc4b42508e4b0400fa1fca0` were pending as of this record.
No backend tests were run for this test-only correction.
