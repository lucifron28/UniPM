---
id: TEST-042
type: test-run
title: Pre-acceptance integration hardening verification
status: executed
recordedAtUtc: 2026-09-30T01:49:49Z
testedCommit: e301a91cf4ffaa6bdc13812f52141b4891c657af
sourceBranch: feature/pre-acceptance-integration-hardening
evidenceLevel: locally-executed
---

# Pre-acceptance integration hardening verification

## Objective

Record execution evidence for the 12 pre-acceptance integration fixes. Backend
and mobile functional tests ran at `fabb49a`; final web and mobile static
checks ran at `e301a91`. Their individual execution SHAs are recorded below.

## Execution identity

- Final web tested commit: `e301a91cf4ffaa6bdc13812f52141b4891c657af`.
- Backend API tests, backend build, and mobile functional tests commit:
  `fabb49a05f8947c6eabb5836b0647e1bf46c2960`.
- Branch: `feature/pre-acceptance-integration-hardening`.
- Backend execution: 2026-09-30 UTC, using existing restore assets after the
  sandbox denied access to the user-level NuGet configuration during restore.
- Mobile execution: 2026-09-30 UTC, Flutter tests with fake repositories or
  mock HTTP; no live backend.
- Mobile format/analyzer checks ran at
  `e301a91cf4ffaa6bdc13812f52141b4891c657af`. Their recorded mobile tree
  `9d94627a519f650cd9e8a2909ffbb2c26fc60ef3` matches the tree at
  `fabb49a05f8947c6eabb5836b0647e1bf46c2960`.

## Environment

Windows PowerShell, .NET SDK, and local Flutter SDK. No real records,
credentials, or connection strings are included in evidence. The backend test
output contains local Data Protection key-decryption and Windows Event Log
access diagnostics; the selected tests passed.

## Commands

```powershell
dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --no-restore --filter "FullyQualifiedName~AssetReadEndpointsTests|FullyQualifiedName~ScheduleQueryEndpointsTests|FullyQualifiedName~PreventiveMaintenanceFormDraftEndpointsTests|FullyQualifiedName~InspectionLocationVerificationTests" --logger "console;verbosity=normal" --logger "trx;LogFileName=api-filter-no-restore.trx" --results-directory "artifacts/evidence/20260930T005645Z-fabb49a-final-backend"

dotnet build server/server.csproj --no-restore

flutter test test/batch_progress_calculation_test.dart test/inspection_location_test.dart test/preventive_maintenance_form_draft_test.dart --reporter expanded

npm run test:run -- src/features/assets/asset-contract.test.ts src/features/assets/asset-create.test.tsx src/features/schedules/schedule-contract.test.ts src/features/schedules/schedule-workflow.test.tsx src/features/reports/pm-period-dashboard.test.tsx src/features/preventive-maintenance-forms/preventive-maintenance-form-review.test.tsx

node node_modules/@playwright/test/cli.js test e2e/assets.spec.ts e2e/schedules.spec.ts e2e/pm-period-dashboard.spec.ts e2e/pm-acknowledgement-review.spec.ts --global-timeout=75000 --reporter=line,json

npm run api:contract:test
npm run lint
npm run typecheck
npm run build
npm run api:check
npm run format:check
git diff --check
```

Backend commands ran from the repository root. Flutter and mobile static
checks ran from `mobile/`; web commands ran from `web/`. The final changed-file
Prettier check covered all changed web files in the four relevant commits.
Mobile static commands were
`dart format --output=none --set-exit-if-changed` over nine changed Dart files
and `flutter analyze`.

## Results

- Affected backend API filter: exit 0, 70 passed, 0 failed, 0 skipped.
- Backend build: exit 0, 0 warnings, 0 errors.
- Focused mobile tests: exit 0, 73 passed.
- Mobile format check at `e301a91`: 9 files checked, 0 changed, exit 0.
- Flutter analyze at `e301a91`: exit 0, no issues. Its source tree hash
  `9d94627a519f650cd9e8a2909ffbb2c26fc60ef3` matches the mobile tree at
  `fabb49a`.
- Focused web Vitest at `e301a91`: 6 files passed, 54/54 tests passed,
  0 failed, 0 pending.
- Playwright at `e301a91`: 4 specs, 25/25 passed, 0 skipped, 0 unexpected,
  0 flaky; exit 0.
- Negative OpenAPI contract checks at `e301a91`: 7/7 passed, exit 0.
- Web lint, typecheck, production build, committed-state API drift check,
  changed-file Prettier check, and `git diff --check`: all exit 0.
- Full web `npm run format:check`: exit 1 with 39 warned files. Comparison
  against the 19 web paths changed from `fabb49a` to `e301a91` found no overlap;
  these are untouched baseline paths.
- Native SQL Server tests: skipped; `UNIPM_SQLSERVER_TEST_CONNECTION` was
  unset, so no connectivity probe or SQL test run occurred.
- Physical-device and iOS tests: NOT VERIFIED.

## Test counts

| Scope | Passed | Failed | Skipped | Tested commit |
|---|---:|---:|---:|---|
| Affected backend API filter | 70 | 0 | 0 | `fabb49a05f8947c6eabb5836b0647e1bf46c2960` |
| Backend build | n/a | 0 errors | n/a | `fabb49a05f8947c6eabb5836b0647e1bf46c2960` |
| Focused mobile tests | 73 | 0 | 0 | `fabb49a05f8947c6eabb5836b0647e1bf46c2960` |
| Dart format check | 9 files checked, 0 changed | 0 | n/a | `e301a91cf4ffaa6bdc13812f52141b4891c657af` |
| Flutter analyze | no issues | 0 | n/a | `e301a91cf4ffaa6bdc13812f52141b4891c657af` |
| Focused web Vitest | 54 | 0 | 0 | `e301a91cf4ffaa6bdc13812f52141b4891c657af` |
| Playwright, 4 specs | 25 | 0 | 0 | `e301a91cf4ffaa6bdc13812f52141b4891c657af` |
| Negative API contract checks | 7 | 0 | 0 | `e301a91cf4ffaa6bdc13812f52141b4891c657af` |

## SQL Server verification

Skipped because `UNIPM_SQLSERVER_TEST_CONNECTION` was not configured. No
connection value was read or recorded.

## AI-provider verification

No AI provider was contacted. These fixes do not require an AI provider.

## Generated artifacts

- Backend captures, TRX, summary, dirty-path snapshots, and SHA256 manifest:
  `artifacts/evidence/20260930T005645Z-fabb49a-final-backend/`.
- Mobile audit, test log, and summary:
  `artifacts/evidence/mobile-audit-fabb49a-20260930/`.
- Mobile test log SHA256:
  `24B7A44F911CD8CA160B914F9168056AF6BAF7F9385ACC37742E5F7B38764289`.
- Mobile static-check summary records HEAD
  `e301a91cf4ffaa6bdc13812f52141b4891c657af`, matching mobile tree hashes at
  `fabb49a` and `e301a91`. Summary SHA256:
  `2BC5F6731335C2068592F2AC17D7760FC583B1AC34365E408F5547151C7FBA35`.
  Format log SHA256:
  `67C3D4DDF721DEA70D394D493A7244295CB1C5139CA16063FD6BA79B383FBD75`.
  Analyze log SHA256:
  `8A4D8A221F40182EECEDD67C3E9D4E72CBE5D15FBCC26FC98FF49DFC576DD7C6`.
- Backend artifact manifest contains 12 files; every recorded hash was
  checked against the corresponding artifact. The artifact directory is
  ignored by Git.
- Final web outputs are retained under `artifacts/web-executor/`. The roll-up
  `results.md` SHA256 is
  `CCFFC6FAFD02AD4491CF19F1F3D2CD86E8028BD82F0AEDE71961AAEDAA5863C3`.
  Core report hashes:

| Artifact | SHA256 |
|---|---|
| `final-e301a91-vitest.stdout.log` | `BD2299DD697CF83F17C16F5A45C1DC6F77FE075653074919C5509EBD50FADCD2` |
| `final-e301a91-vitest.stderr.log` | `EF142D2C9848ED682F5CA30844040D79EA7EABC121EA4795337F936C733B7D8F` |
| `final-e301a91-vitest.json` | `EC46CA8C62CDB519B0C85B53293BDBC7FEBC8BDBB0C950CCDF91F8F8F4B448D6` |
| `final-e301a91-playwright.stdout.log` | `C593182C92935F5EE47AC9526EABE51E5B4F208895B8AAEC7616B7714A34B07F` |
| `final-e301a91-playwright.stderr.log` | `A6B338890D26DDE163BB13F6DC592D9C838166C0673BBF7ACB833EA3D9EBDEB7` |
| `final-e301a91-playwright.json` | `16F29289E5D8468533CBC95F7352ED21B989D92054E1D1AFB13508502205AF74` |
| `api-contract-test-final-e301a91.log` | `A138BD101D130AE4F54436C1B11373C5CD6C1A6E011CF1848A247785E7BD8C4B` |
| `final-e301a91-lint.log` | `C18F3BD00DD4AE25DB331D4371B1F84BD4762D41596640964438413C0778AF0C` |
| `final-e301a91-typecheck.log` | `EEE165631199715DF25D3A4F867412E35D326B0DBA93D51D14DB95524A866D58` |
| `final-e301a91-build.log` | `A2C49E3C416FDE08E621B8AD4591CAB2A5A3AB53C4E1C66ABA42CAC1F8D6FC5C` |
| `final-e301a91-api-check.log` | `7B2607708C46A7CD4A659DBA1E4AE3621F5AA50B09564444F8313C48B3C43745` |
| `final-e301a91-format-touched.log` | `4CDB3001DF92FCD42A0E2AED7FC9DF0E51D3E66E5F18F8D254D57028E1F35D63` |
| `final-e301a91-format-full.log` | `780DB163E4BEB7A430BD40CAC2ABDAB5D8004515AC104AD9852BFCC9B3E7AF05` |
- No raw artifacts were copied into committed evidence.

## Failures and corrections

Initial backend build and test commands that attempted restore exited 1 before
build/test discovery because the sandbox denied access to the user-level
NuGet.Config. Retrying with `--no-restore` used existing assets and passed.
An earlier Vitest attempt had 20 failures and 34 passes because MSW requests
used an unmatched API base; its exact HEAD was not captured. A sandbox
Playwright attempt at the interim `fabb49a` snapshot passed all five selected
tests but exited 1 on plugin teardown. A process-access retry passed those
five with exit 0. Final focused Vitest and Playwright runs at `e301a91` passed.
Before commit, `api:check` returned 1 because generated changes were
uncommitted; the committed-state check at `e301a91` passed.

## Skipped verification

Native SQL coverage, physical-device and iOS checks were not run. The full web
format check failed on 39 unchanged baseline paths; changed-file formatting
passed.

## Limitations

The backend and mobile functional test results apply to
`fabb49a05f8947c6eabb5836b0647e1bf46c2960`. Web and mobile static checks apply
to `e301a91cf4ffaa6bdc13812f52141b4891c657af`. No claim is made that backend
or mobile test suites were rerun at `e301a91`. Native SQL and physical-device/
iOS limits remain as stated above. Historical evidence records were not
changed.
