---
id: TEST-154
type: test-run
title: Capstone remediation web regression recovery
status: executed
recordedAtUtc: 2026-10-25
testedCommit: bb3f127e714043bf1d8eddfbcd986164d528b420
additionalTestedCommit: 3e166f405ec3026dbc41ab57b21d1883df227d82
sourceBranch: feature/capstone-remediation
evidenceLevel: locally-executed
---

# Web regression recovery

TEST-153 retains the failed diagnostic and focused attempts. The repository
owner subsequently authorized remaining in-scope corrections and verification
for this task, overriding the default retry limit. No further permission was
inferred from elapsed time.

## Corrections and review

Commit `008f3231fb75973b27d6186a627c46701a344d4c` supplies the Department
filter in the new test's catalog fixture. Commit
`3e166f405ec3026dbc41ab57b21d1883df227d82` selects the datalist picker
by its existing placeholder. The picker has a linked label and a native
combobox role; the original textbox query was incorrect. The test still
asserts that the Department filter and selected value reach the preview plan.
No production accessibility defect is inferred from that selector mistake.

Commit `bb3f127e714043bf1d8eddfbcd986164d528b420` adds only
`maxWorkers: 2` to the existing Vitest configuration. Assertions, timeouts,
test inclusion and production workflow remain unchanged. The main chat
reviewed all three actual commit diffs.

## Executed results

Commands run from `web`, with Node 22.23.4 and source frozen during execution.

| Command | Exact source | Result |
|---|---|---|
| `npm run test:run -- src/features/reports/pm-analytics-v3.test.tsx -t "keeps a generic clarification filter in the preview plan" --maxWorkers=2` | `3e166f4` | Exit 0; 1 passed, 18 deselected |
| `npm run test:run -- --maxWorkers=2` | `3e166f4` | Exit 0; 31 files and 256 tests passed; 33.67 seconds |
| `npm run e2e -- --workers=2`, process `CI=true`, `UNIPM_E2E_PORT=5197` | `3e166f4` | Exit 0; 60 passed, 1 skipped; 59.4 seconds |
| `npm run test:run` | `bb3f127` | Exit 0; 31 files and 256 tests passed; 33.60 seconds |
| `npm run format:check` | `bb3f127` | Exit 0 |
| `npm run lint` | `bb3f127` | Exit 0 |
| `npm run typecheck` | `bb3f127` | Exit 0 |

The isolated browser port was free before startup, and the test runner
launched its own Vite child. All nine existing unit failures and seven browser
failures from TEST-153 passed under bounded runs. These observations support
execution-resource sensitivity; they do not establish a production defect in
each previously failing component. The newly added regression separately
needed the fixture and selector corrections described above.

The production build passed earlier at `9b143d1` and API contract checks
passed on the unchanged contract at `0bc5701`, as recorded in TEST-153.
Changes after that production build affect the new test and unit-worker
configuration only. No repeated production build is claimed here.

## Artifacts and limits

Reviewed raw logs are ignored under `artifacts/remediation/clients/`:
`phase1-focused-after-selector.log`, `phase1-full-vitest-workers2.log`,
`phase1-playwright-ci-port5197-workers2.log`,
`phase1-full-vitest-default-workers2.log`, `phase1-format-finalhead.log`,
`phase1-lint-finalhead.log`, and `phase1-typecheck-finalhead.log`.
Original copies under `web/artifacts/remediation/clients/` were copied without
overwriting prior logs. They describe the same runs, not additional executions.

One browser case remains skipped. This record establishes local web checks,
not remote GitHub Actions, native SQL, live-provider accuracy, physical-device
acceptance, institutional deployment or external approval.
