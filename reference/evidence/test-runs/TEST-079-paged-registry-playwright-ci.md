---
id: TEST-079
type: test-run
title: Paged registry Playwright fixture corrections and exact-head CI
status: executed
verificationOutcome: passed
reviewReadiness: REVIEWABLE
recordedAtUtc: 2026-10-11T15:13:59Z
testedCommit: 083ae228e25affac36a6d260ca0da0bce38d963d
sourceBranch: feature/registry-search-filtering
evidenceLevel: ci-executed
---

# Paged registry Playwright fixture corrections and exact-head CI

This record covers test-only fixture corrections across five commits:
`d3dfd404a709d82ba2b65fd44efeea6df7ec03b5`,
`bc45c3784b522f4bb9967d5511b25dc075279cfe`,
`41ec9f93eaddb100273fde5b2ddb2a0c508e8103`,
`b29ad4bf4fbe15b98389a2c163acadf96eecc25c`, and
`083ae228e25affac36a6d260ca0da0bce38d963d`. The fixtures now match query
routes with their query strings, use the strict inspection DTO, use Emergency
Light months 6 and 12 and Water Drinking Station months 2, 5, 8, and 11, select
the exact Calendar quarter label, exercise the asset picker combobox, and
distinguish the bounded asset list array from the selected asset detail object.
The final list mock applies optional status, department-eligibility, search,
and limit filters. Production code was not changed.

## Verification status

| Commit | Check | Result |
|---|---|---|
| `083ae228` | [Web CI run 38150020132](https://github.com/lucifron28/UniPM/actions/runs/38150020132) | Passed on Node.js 22. Format, lint, typecheck, contract, negative-contract, generated-client, and production-build stages passed. Unit tests: 210/210 across 28 files. All-files coverage: 80.77% statements, 72.35% branches, 78.61% functions, and 82.28% lines. Playwright: 47 passed, 1 skipped, 0 failed in 44.3 seconds. |
| `083ae228` | [Backend CI run 38150024540](https://github.com/lucifron28/UniPM/actions/runs/38150024540) | Passed Release build; 364 backend tests passed, 45 optional tests skipped, and all 409 infrastructure checks passed. |
| `41ec9f93` | [Earlier Playwright CI run 38149340218](https://github.com/lucifron28/UniPM/actions/runs/38149340218) | 45 passed, 2 failed, 1 skipped. The schedule failures were an ambiguous Calendar quarter locator and the asset list mock returning an array for the selected-asset detail request. The two follow-up test-only commits corrected them. |
| `b29ad4bf` and `083ae228` | Changed-file Prettier and `git diff --check` | Passed. |

Local Windows harness launches executed no Playwright cases. The wrong-CLI and
config-import attempts, followed by a stalled server-start attempt, were runner
setup failures. The final attempt was interrupted with exit code 1; this was
not a Playwright case failure. Raw launch logs were removed with the owned
temporary harness artifacts and are not retained. The temporary config and
owned server on port 5197 were cleaned up. The source worktree was clean at
`083ae228` before this record was added. No local E2E pass or local backend test
run is claimed for this follow-up.

The single Playwright skip is the existing opt-in live workflow in
`web/e2e/schedule-batch-assignment.live.spec.ts:113`, not a new skip. It
requires an owned disposable API on `localhost:5262` and
`UNIPM_DEV_USER_PASSWORD`; that live API workflow was not verified. Remaining
acceptance limitations include GSD/staging, physical-device, and deployment
checks. Photo-row deletion concurrency is handled separately in PR #89 and is
outside this task. TEST-077 and TEST-078 remain unchanged.
