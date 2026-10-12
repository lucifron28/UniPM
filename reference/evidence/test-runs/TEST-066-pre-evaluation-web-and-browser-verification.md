---
id: TEST-066
type: test-run
title: Pre-evaluation web and browser verification
status: executed
recordedAtUtc: 2026-10-10T04:59:56Z
testedCommit: ecadf495fcfa482a6012f4fa378237596ed5fa35
sourceBranch: feat/pre-evaluation-ux-motion
evidenceLevel: locally-executed
---

# Pre-evaluation web and browser verification

## Objective

Verify the schedule coverage interface, updated OpenAPI client, browser
fixtures, responsive layouts, and existing PM workflows on the exact current
web source.

## Execution identity

Web source commit:
ecadf495fcfa482a6012f4fa378237596ed5fa35 on
feat/pre-evaluation-ux-motion. The backend and backend-test source did not
change between 5d68d8e39a884a085dc99dd448ac9b9d865ebc03 and this commit.
The live API used by the role-chain browser test was built from that backend
source. Environment: Windows, Node.js 24.15.0, npm 11.12.1. The package
declares Node.js >=22 <23, so local Node 24 results should be read alongside
the exact-head CI result.

## Commands and results

| Command | Result |
| --- | --- |
| npm run format:check | Passed. |
| npm run lint | Passed with zero ESLint warnings. |
| npm run typecheck | Passed. |
| npm run test:run | 28 files and 211 tests passed. |
| npm run api:contract:check | Passed. |
| npm run api:contract:test | Seven negative contract tests passed. |
| npm run api:check | OpenAPI contract and generated-client consistency passed. |
| npm run build | TypeScript check and production Vite build passed; 2,208 modules transformed. |
| npm run e2e | 46 Playwright tests passed in 40.2 seconds. |

The browser run covered desktop, tablet, and mobile viewport behavior, schedule
filters and pagination, keyboard focus, reduced-motion behavior, PM dashboard
and acknowledgement views, authentication, and the GSD/Supervisor/Inspector
assigned-batch workflow through WMS referral. The run included a live API
backed by the disposable SQL database recorded in the raw verification
metadata.

The disposable API process was stopped and the specifically named disposable
database was dropped after the run. SQL Server itself was left running.

## Failures and corrections

Earlier browser attempts exposed test-fixture issues: several schedules used
category/month pairs outside CPMP, mock pages omitted the enrollment-deferrals
response, one schedule mock returned the wrong coverage-review shape, and
static authentication fixtures had expired timestamps. The fixtures were
corrected to valid cycles and current session times, and the missing response
mocks were added. The repeated final full suite passed 46 of 46. These changes
were limited to E2E fixtures and test synchronization.

## Generated artifacts

Raw output is retained, ignored by Git, under:

artifacts/evidence/20261010-ecadf49-web/

This includes format, lint, typecheck, unit, contract, generation, build,
Playwright, and disposable-resource cleanup logs. Raw logs are not committed.

## Limitations

The browser checks are not a complete WCAG audit or screen-reader review. GSD
acceptance, a temporary deployment, physical-device acceptance, and Flutter
verification were not performed. No mobile source changed in this batch.
Exact-head GitHub CI later passed at f1fd7c798e61fdc4f54221fc1c7dfa32e62ceb6d; see TEST-067.
