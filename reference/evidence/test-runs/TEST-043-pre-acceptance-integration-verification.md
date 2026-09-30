---
id: TEST-043
type: test-run
title: Final pre-acceptance integration verification
status: executed
recordedAtUtc: 2026-09-30T09:26:08Z
testedCommit: 177b5e66f31567d343e8a84c551e9a6e7c6ca7d5
sourceBranch: feature/pre-acceptance-integration-hardening
evidenceLevel: locally-executed
---

# Final pre-acceptance integration verification

## Objective

Record schedule-contract and PM navigation results with their separate source
identities, including checks not rerun on later commits.

## Execution Identity

- Schedule tests used base commit
  `c2fe175fbc3a3a9317f09817a9dbdb5732ebf8b7` plus scoped worktree patch
  SHA-1 `abafc8748ccfc8c05c3d5ebc06dd9780cb3c5279`.
- Final navigation tests, lint, and typecheck used committed HEAD
  `177b5e66f31567d343e8a84c551e9a6e7c6ca7d5` plus navigation-only binary diff
  object ID `5d08c5393806c99114d840252279cb12f5df59ae`, scoped to seven web
  files. This was not yet a clean committed navigation tree.
- The backend and web schedule changes were later committed as
  `17847785e6568d17a287460010c4ece8e6d52629` and
  `177b5e66f31567d343e8a84c551e9a6e7c6ca7d5`. No tests were rerun at either
  commit for the schedule API/web scopes. The navigation changes were later
  committed as `67cdb44f47ab92e173e292a103aeccc2ade1dc5f`; no tests were rerun
  at that commit. The schedule commits were present before the final navigation
  run.
- The initial navigation run used HEAD `177b5e6` with a pre-correction
  worktree patch whose identity was not retained. Do not attribute that run to
  the final `5d08c539` patch.
- The final navigation Vitest run started at 16:50:09 Asia/Shanghai on
  2026-09-30. Lint started at 16:50:55 and typecheck at 16:51:30. C's
  navigation commit followed later; no verification rerun was requested.

## Environment

Local Windows workspace. The API log reports .NET 10.0.8. Navigation Vitest
used Vitest 4.1.10; no Node version is recorded here.

## Commands

```powershell
dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --no-restore --filter "FullyQualifiedName~ScheduleQueryEndpointsTests" --logger "console;verbosity=normal"

npm run test:run -- src/features/schedules/schedule-contract.test.ts src/features/schedules/schedule-workflow.test.tsx

npm run test:run -- src/features/preventive-maintenance-forms/preventive-maintenance-form-review.test.tsx -t "all departments|selected department|acknowledged batch detail|omitted dashboard scope|ordinary form-registry detail"

npm run typecheck
```

The navigation result report records touched-file ESLint with
`--max-warnings 0`, touched-file Prettier write, and `git diff --check` on the
seven scoped files; each exited 0. The exact lint and format command lines were
not retained.

## Results

The schedule API filter passed 11/11 tests, and the two focused web schedule
files passed 17/17 tests; both commands exited 0 on base `c2fe175` plus patch
`abafc874`.

The initial navigation run failed two inspection-return assertions (3 passed,
2 failed, 11 skipped) on HEAD `177b5e6` with a pre-correction patch whose
identity was not retained. The focused correction removed `reviewFormId` from
the inspection-detail return search while preserving the seven dashboard
fields. The same command then passed all 5 selected navigation tests, with 11
filtered tests skipped, exit 0, at HEAD `177b5e6` plus final diff
`5d08c539`. Vitest emitted non-fatal jsdom `window.scrollTo` warnings during
router navigation.

Final touched-file ESLint, `npm run typecheck`, touched-file Prettier write,
and `git diff --check` passed with exit 0. An earlier typecheck reported route
search union/optionality errors and an unused import; the final typecheck was
clean.

## Test Counts

| Scope | Passed | Failed | Skipped | Tested source |
|---|---:|---:|---:|---|
| `ScheduleQueryEndpointsTests` | 11 | 0 | 0 | base `c2fe175` + patch `abafc874` |
| Web schedule contract/workflow | 17 | 0 | 0 | base `c2fe175` + patch `abafc874` |
| PM navigation, initial focused run | 3 | 2 | 11 | HEAD `177b5e6` + pre-correction patch (identity not retained) |
| PM navigation, focused rerun | 5 | 0 | 11 | HEAD `177b5e6` + nav diff `5d08c539` |

## Skipped Verification

The 11 navigation tests outside the focused name filter were skipped. Native
SQL Server, full web suite, browser automation, and physical-device/iOS checks
were not part of these focused runs.

## SQL Server Verification

No native SQL Server run is claimed in these focused schedule tests.

## AI-Provider Verification

Not applicable; these schedule tests do not require an AI provider.

## Generated Artifacts

Ignored raw schedule logs are under
`artifacts/schedule-temporal-boundary-20260930-c2fe175/`. Navigation logs and
the result report are under `artifacts/web-executor/`. No raw output was copied
into committed evidence.

| Artifact | SHA-256 |
|---|---|
| `artifacts/schedule-temporal-boundary-20260930-c2fe175/patch-identities.log` | `4609ECA98A753FC8785BF5EF8A4717C9592C0FBC4FD1C50D106264534832B862` |
| `artifacts/schedule-temporal-boundary-20260930-c2fe175/schedule-api-filter.log` | `A49AEFA64257605E8D666BE3317197BC1CB7A86A0F001E53976F55BA2293D7F7` |
| `artifacts/schedule-temporal-boundary-20260930-c2fe175/schedule-web-focused.log` | `632B64920A88213150DC42DA422EA143750A810AAB002133F8E04F7441977DB9` |
| `artifacts/web-executor/pm-navigation-result-c2fe.md` | `4CF1C1E8DDF9936296DD9135253705B8CC9681D1F78842956262FC0F2075901E` |
| `artifacts/web-executor/pm-navigation-vitest-c2fe.log` (initial run) | `F27AB3503A2725C1942D576C792B102C5F23C998207828A0E7327D5B9ECE3DFF` |
| `artifacts/web-executor/pm-navigation-vitest-c2fe-rerun.log` (final run) | `71E5A6CCAC758C4C5580818B7B0AB329706E0378C6B7E2938E4213AF5EF36CE4` |
| `artifacts/web-executor/pm-navigation-eslint-c2fe.log` (empty) | `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855` |
| `artifacts/web-executor/pm-navigation-typecheck-c2fe.log` (initial run) | `26D9C2E004FDD278A2CCAEF65FAE63A7F1B8AE9FEE454CBC11925B1C65A6C9CF` |
| `artifacts/web-executor/pm-navigation-typecheck-final-c2fe.log` | `26A9A4880EEFEA7D7B9ACFF388DD61AEFE85979DEA3E7E7530F427C173E48EB5` |

## Limitations

The API log contains an unrelated CS8602 compile warning and non-fatal local
DPAPI/Event Log diagnostics; the filtered run still exited 0 with 11 passing
tests. The final navigation run is recorded at HEAD plus the identified patch.
The later navigation commit `67cdb44` was not rerun. Do not copy raw logs,
secrets, or unreviewed output into this record.
