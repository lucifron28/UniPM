---
id: TEST-077
type: test-run
title: Paged registry re-audit verification
status: executed
verificationOutcome: partial
reviewReadiness: BLOCKED
recordedAtUtc: 2026-10-11T12:13:18Z
testedCommit: 8ef82073bac50391b8c0fea24691048100f82837
sourceBranch: feature/registry-search-filtering
evidenceLevel: locally-executed
---

# Paged registry re-audit verification

The focused backend gate passed. The final web unit retry had one failing
assertion, so overall verification is partial and review readiness remains
blocked.

## Executions

| Commit | Command | Result |
|---|---|---|
| `6d3525db23e3f687a4be086d537617d870f45ed1` | `dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --filter "FullyQualifiedName~RegistryReadEndpointsTests|FullyQualifiedName~ScheduleQueryEndpointsTests"` | 41 passed, 0 failed, 0 skipped. Host run used no-restore, disabled build servers, one worker, bounded timeout, and TRX output. |
| `1aeddf79134959b17b7d40a1153f4d91f6be7f1b` | `npm run lint` | Exit 0. |
| `3c587beebacca4b8490a9371600e8b73130158da` | `npm run typecheck` retry; `npm run api:check`; `npm run api:contract:test` | Typecheck and both contract commands exited 0; all seven negative contract cases passed. |
| `3c587beebacca4b8490a9371600e8b73130158da` | `npm run test:run` | 200 passed, 10 failed. |
| `8ef82073bac50391b8c0fea24691048100f82837` | `npm run test:run` retry | 209 passed, 1 failed across 28 files. |

The remaining unit failure is `asset-registry.test.tsx`: “restores URL search
filter parameters into form state and active filter message”. Its assertion
expects `fire-extinguisher` in the active-filter text; the interface displays
the readable category name `Fire extinguishers`. No further correction or test
retry was made under the bounded execution rule. The initial typecheck attempt
failed on a nullable asset fixture type; the test helper was corrected before
the successful retry. Later lint, typecheck, and contract results are not
claimed for the final unit-test commit.

The SQL test ran against native SQL Server 2019. It confirmed engine major 15,
Full-Text Search, and compatibility level 150. The focused filter covered all
three registry reads and the schedule canonical-quarter regression; its
synthetic 1,000-asset fixture exercised bounded paging and filters, not
production load. The test used and removed a disposable database; the
test-specific database count was zero before and after. No demo database or
external provider was used, and no credentials were recorded. The backend
test build emitted an existing nullable warning at
`PreventiveMaintenanceFormEndpoints.cs:559`.

An earlier focused backend attempt reported 39 passed, 1 failed, and 1 skipped.
The auto-enrollment test returned 409 under the test clock, and the SQL test
was skipped because its connection environment variable was unset. After
correcting the test clock/setup and configuring the authorized disposable SQL
connection, the focused run above passed. The initial attempt's exact commit
SHA was not retained in the available run summary.

Local web checks used Node.js `v24.15.0` and npm `11.12.1`. The repository's
web CI uses Node.js 22; local Node 22 parity is not established. The API
snapshot came from an actual local API OpenAPI response; `npm run api:check`
used the established generator and generated-client consistency check.
Changed-file Prettier initially flagged two web files; the corrected
changed-file formatting pass passed. The exact commit SHA for that formatting
run was not retained in the available run summary.

`TEST-076` remains unchanged and retains the earlier backend attempt whose
result was unavailable. `npm run build`, Playwright, exact-head CI, push, draft
PR, merge, deployment, device tests, and publication were not performed. The
failed unit retry stopped further verification under the cost boundary. No
full backend suite or production-load claim is included. Historical Web CI run
`38132363297` on prior remote head `73dcc4a` failed with 2 errors and 6
warnings; it does not verify this head. No new exact-head CI was run. Final
`git diff --check` was not verified.

Ignored output and TRX are under `artifacts/registry-tooling/registry-read-focused-6d3525d/`
and `artifacts/registry-tooling/web-checks/`.
