---
id: TEST-081
type: test-run
title: PM analytics V2 expansion verification
status: executed
recordedAtUtc: 2026-10-14T20:05:34Z
testedCommit: 5e0ab750a63bf9472493e3dc85f2f619299b2f37
sourceBranch: feature/nla-schema-expansion
evidenceLevel: ci-executed
---

# PM analytics V2 expansion verification

## Objective

Record the implementation checks for the additive typed-plan PM analytics V2
backend and web flow, keeping local working-tree checks distinct from exact-head
CI. The final exact-head Backend and Web CI runs passed on the source commit
identified below.

## Execution identity

The current source candidate is commit
`5e0ab750a63bf9472493e3dc85f2f619299b2f37` on
`feature/nla-schema-expansion`. The code commits are
`14321bad3b00c512460399fcd301af43e3aa111c` (generated API client),
`60f478c9bd3b78633b6a28262389978874b8a05a` (web panel), and
`5e0ab750a63bf9472493e3dc85f2f619299b2f37` (SQL test namespace imports). The
backend implementation milestone/head was
`1625d88e68797f9f92ea26646b955a4f8515f85a`, based on main commit
`7179227639b29913451ec81617e39e9e2ca9bd91`. The documentation commit is
separate and is not the source identity for code tests.

The local web unit test and typecheck ran from base commit
`1625d88e68797f9f92ea26646b955a4f8515f85a` with the implementation present as
a working-tree patch. The unit rerun preceded the final Button-variant mapping
correction; the typecheck rerun followed it. Neither local command was an exact
commit test.

## Environment

Windows local checkout and GitHub CI. No external AI provider was called, no
provider credentials were read, and no held-out evaluation content was opened.
The audited local database's active-API status remains unconfirmed. No local
native SQL Server test or database mutation was performed for this expansion.

## Commands

- `dotnet build server/UniPM.Api.csproj --configuration Release` — failed with
  MSB1009 because the project path was wrong; compilation did not start. Then
  `dotnet build server/server.csproj --configuration Release` — the corrected
  path first reported four in-scope compile errors. After one focused compile
  correction, the same command exited 0 with no errors and the pre-existing
  nullable warning at `PreventiveMaintenanceFormEndpoints.cs:559`. These build
  attempts ran before backend commits on main base
  `7179227639b29913451ec81617e39e9e2ca9bd91` plus the backend working-tree
  patch; they are not exact commit builds.
- `dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --configuration Release --filter FullyQualifiedName~PmAnalyticsV2 --logger trx --results-directory artifacts/web-qa-20261015/nla-v2/backend-focused`
  — attempted on backend milestone/head
  `1625d88e68797f9f92ea26646b955a4f8515f85a`; it exited 1 during dependency
  restore, before test discovery. No backend tests executed from this local
  invocation.
- `npm run test:run -- src/features/reports/pm-analytics.test.tsx` — first run
  and one focused rerun, both on the base plus web working-tree patch.
- `npm run typecheck` — initial run and one authorized rerun on the base plus
  web working-tree patch.
- Changed-file Prettier — passed for the edited web source and generated files.
- `git diff --check 7179227639b29913451ec81617e39e9e2ca9bd91..eeb8f5851b3b25234ab9a94a706196ebfcab798d`
  — exited 0 before the two-line test-import commit.

## Results

The focused web unit suite first reported 11 passed and 1 failed because its
expected title for Remaining + DistinctAssets did not match the panel's
asset-grain label. The expectation was corrected once; the rerun passed 12/12.
That rerun occurred before the final Button variant mapping change.

The first typecheck reported unsupported Button variants in the new panel. Those
values were mapped to the shared component's existing `primary` and `secondary`
variants. The authorized typecheck rerun passed after that correction.

The exact-head Web CI run `38627645393` passed on
`eeb8f5851b3b25234ab9a94a706196ebfcab798d`, before the SQL test import fix. The
exact-head Backend CI run `38627644991` on that same commit failed during build
with missing namespace imports in `PmAnalyticsV2SqlServerTests.cs`. The fix was
limited to imports for the asset-status and preventive-maintenance-form-status
catalogs in commit `5e0ab750a63bf9472493e3dc85f2f619299b2f37`.

For current commit `5e0ab750a63bf9472493e3dc85f2f619299b2f37`, Backend CI run
`38628014902` passed: 485 passed, 46 skipped, 0 failed, out of 531 total.
Twenty-two tests named under `PmAnalyticsV2` passed. The
`PmAnalyticsV2SqlServerTests` fact was explicitly skipped. The Release solution
build and configuration validation passed; the build retained the baseline
nullable warning at `PreventiveMaintenanceFormEndpoints.cs:559`. See [Backend CI run 38628014902](https://github.com/lucifron28/UniPM/actions/runs/38628014902).

Web CI run `38628014512` passed on the same exact commit. It passed 219/219
unit tests across 28 files, the 219-test coverage run, and Playwright with 50
passed and 1 skipped. Formatting, lint, typecheck, positive and negative
contract checks, generated-client drift, and the production build also passed.
See [Web CI run 38628014512](https://github.com/lucifron28/UniPM/actions/runs/38628014512).

## Test counts

- Focused web unit rerun: 12 passed, 0 failed, on base plus working-tree patch
  before the final Button variant mapping correction.
- Frontend typecheck: passed after the Button variant mapping correction.
- Backend focused local test: no test count; restore failed before discovery.
- Current exact-head Backend CI: 485 passed, 46 skipped, 0 failed; the
  SQL Server-tagged V2 fact was skipped.
- Current exact-head Web CI: 219/219 unit tests, 219/219 coverage run, and
  50 Playwright passed/1 skipped; formatting, lint, typecheck, contracts,
  generated-client drift, and production build passed.

## SQL Server verification

The local SQL Server-tagged V2 test did not execute. The current Backend CI
explicitly skipped the `PmAnalyticsV2SqlServerTests` fact. No native SQL Server
success is claimed in this record.

## AI-provider verification

No external provider calls were made. The deterministic V2 interpretation and
query flow was verified only through local web unit tests and CI; this does not
establish language quality against institutional questions.

## Generated artifacts

- `artifacts/web-qa-20261015/nla-v2/web/pm-analytics-vitest.log`
- `artifacts/web-qa-20261015/nla-v2/web/pm-analytics-vitest-rerun.log`
- `artifacts/web-qa-20261015/nla-v2/web/typecheck.log`
- `artifacts/web-qa-20261015/nla-v2/web/typecheck-rerun.log`
- `artifacts/web-qa-20261015/nla-v2/web/api-generate-v2-only.log`
- `artifacts/web-qa-20261015/nla-v2/backend-build.log`
- `artifacts/web-qa-20261015/nla-v2/backend-build-corrected.log`
- `artifacts/web-qa-20261015/nla-v2/backend-build-correction-rerun.log`
- `artifacts/web-qa-20261015/nla-v2/backend-focused.log`

## Failures and corrections

The local backend test invocation exited during restore and produced no test
result. The earlier Backend CI build failed because two test-only namespace
imports were missing; both imports were added in the current source candidate.
The first web unit run's stale title expectation was corrected once. The first
typecheck's Button variant mismatch was corrected once. No production
calculations, authentication rules, provider behavior, schemas, or migrations
were changed to address these verification failures.

## Skipped verification

Full local web tests and browser runs were not run. Exact-head Web CI covered
the full web unit suite and Playwright. Native SQL Server execution and
held-out or real-provider evaluation were not run in this task.

## Limitations

The local database aggregate audit is separate from test execution and its
active-API scope is unconfirmed. Existing web unit fixtures are deterministic
synthetic data, not institutional validation. Local test results remain
qualified to their working-tree source identities above.
