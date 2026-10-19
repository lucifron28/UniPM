---
id: TEST-085
type: test-run
title: Offline Draft synchronization follow-up verification
status: executed
recordedAtUtc: 2026-10-19T17:24:16Z
testedCommit: 0ade68d4f30e9e006949f8b0155a612511c8f019
sourceBranch: feature/offline-inspection-workflow
evidenceLevel: ci-executed
---

# Offline Draft synchronization follow-up verification

## Objective

Record final local and exact-head CI verification for the encrypted offline
Draft workflow and its schedule/API contract fixtures. The feature remains a
branch prototype pending GSD/adviser acceptance; this record does not establish
that it belongs in the accepted validation baseline.

## Execution Identity

- Exact Web CI source commit: `0ade68d4f30e9e006949f8b0155a612511c8f019`.
- Branch: `feature/offline-inspection-workflow`.
- Exact-head Web CI: [run 39293257449](https://github.com/lucifron28/UniPM/actions/runs/39293257449), completed successfully.
- Backend CI: [run 39290878270](https://github.com/lucifron28/UniPM/actions/runs/39290878270), completed successfully at `1c8ca88951ceb8a572bfcb588d57a3fa81ca822f`; backend source was unchanged by later Web fixture commits.
- Local checks below ran at the individual commits listed with each result; they are not attributed to the final SHA unless explicitly stated.
- Platform: Windows PowerShell locally; GitHub Actions Ubuntu for Web CI.

## Environment

- .NET SDK: `10.0.300`.
- Flutter checks used the installed Flutter SDK with `--no-pub`.
- Local Web checks ran under Node.js `v24.15.0`, outside the package's declared `>=22 <23` range. The temporary dependency junction to the primary worktree was removed after the run; no dependency files were changed.
- CI used Node.js 22 through `actions/setup-node@v6` and installed dependencies with `npm ci`.
- `UNIPM_SQLSERVER_TEST_CONNECTION` was absent in Process, User, and Machine environment scopes. No SQL Server connection was attempted.
- No Azure, production, or AI-provider operation occurred.

## Commands

Commands executed locally across the identified source commits:

```powershell
dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --filter FullyQualifiedName~PreventiveMaintenanceFormDraftEndpointsTests

cd web
npm run test:run
npm run api:generate

cd ..\mobile
flutter analyze
flutter test --no-pub
flutter build apk --debug --no-pub
```

The exact-head Web CI ran these commands from `web/`:

```sh
npm ci
npm run format:check
npm run lint
npm run typecheck
npm run api:contract:check
npm run api:contract:test
npm run api:check
npm run test:run
npm run test:coverage
npm run build
npx playwright install --with-deps chromium
npm run e2e
```

## Results

- At `75c005da4f09d0b2b1f2b3a031c30bf03225d329`, focused backend Draft endpoint tests passed **36/36**. OpenAPI sanity and Orval generation passed. An Android debug APK build succeeded.
- At `02963c01ea9a21171ef753c781c5d09bdaee99c4`, Flutter analysis passed with no issues and the full Flutter suite passed **167/167**.
- At `d0791f1a83c7647d82499b71dfb5000ae4a95b96`, the full local Web unit suite passed **226/226 across 29 files**. Focused schedule and app-shell tests passed **33/33** after correcting test response mocks.
- At exact-head Web CI commit `0ade68d4f30e9e006949f8b0155a612511c8f019`, formatting, lint, TypeScript, API contract and generated-client checks, unit tests, coverage, and production build all passed. Playwright completed **51 passed, 1 skipped, 0 failed** out of 52.
- Backend CI run 39290878270 succeeded. The locally focused backend result is separately identified above; no SQL Server-tagged test result is claimed.
- `git diff --check` passed for the final evidence update.

## Test Counts

| Check | Source identity | Result |
|---|---|---:|
| Focused backend Draft endpoint tests | `75c005d` | 36 passed, 0 failed, 0 skipped |
| Flutter analyzer | `02963c0` | 0 issues |
| Full Flutter suite | `02963c0` | 167 passed, 0 failed |
| Full local Web unit suite | `d0791f1` | 226 passed across 29 files |
| Focused Web app-shell and schedule tests | `d0791f1` | 33 passed |
| Exact-head Web Playwright | `0ade68d` | 51 passed, 1 skipped, 0 failed |
| Exact-head Web format, lint, typecheck, contracts, coverage, build | `0ade68d` | Passed |
| Backend CI | `1c8ca88951ceb8a572bfcb588d57a3fa81ca822f` | Passed |
| SQL Server 2019 integration tests | Not run | No dedicated connection configured |

## SQL Server Verification

SQL Server 2019 integration tests were not run because no dedicated test
connection was configured. No database connection, migration, or database
write was attempted.

## AI-Provider Verification

No AI provider was called. The offline Draft workflow does not require an AI
provider.

## Generated Artifacts

- Android debug APK was built at `75c005d`; it was not distributed.
- Sanitized local output is retained under ignored
  `artifacts/offline-inspection-workflow/`, including the full Web test log,
  focused Web test log, and exact-head CI log.
- Exact-head CI logs: [39293257449](https://github.com/lucifron28/UniPM/actions/runs/39293257449).

## Failures And Corrections

- An initial backend test compilation attempt failed because a local variable
  name collided within the test method. The scoped test correction was made;
  the focused endpoint run then passed 36/36.
- Clean local Web dependency restoration did not complete. The full local unit
  suite was run with the existing dependency installation through a temporary
  junction; exact-head CI independently completed `npm ci` and all Web checks.
- The first exact-head Playwright run at `d0791f1` had four failures because
  full schedule-response fixtures omitted the required asset status field.
  The fixtures were corrected in `0ade68d`; exact-head CI then passed 51/52,
  with one existing skip and no failures.

## Skipped Verification

- SQL Server 2019 integration tests: not run; the dedicated connection was not configured.
- Local Playwright: not run because the local Playwright browser cache was absent; exact-head GitHub CI ran the suite.
- Physical Android device, iOS, real network loss/recovery, and deployed-backend verification: not performed.
- GSD/adviser acceptance: pending.

## Limitations

The encrypted local Draft prototype is not accepted into the GSD validation
baseline. Authentication remains memory-only, so users must sign in again after
an app restart before queued work can synchronize. Submission, acknowledgement,
photo evidence, and location verification remain online-only. Conflict handling
preserves both versions and blocks submission for user review. Automated tests
and a debug APK do not verify Android keystore behavior or field usability on a
physical device. Acceptance, physical-device checks, SQL Server 2019
integration, and deployment verification remain outstanding.
