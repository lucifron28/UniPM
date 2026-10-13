---
id: TEST-061
type: test-run
title: Operational navigation access and continued field-workflow verification
status: executed
recordedAtUtc: 2026-10-06T14:40:28Z
testedCommit: 439bf3132a4d29057ccac927f1cbc746acfa4291
sourceBranch: fix/field-testing-unblock
evidenceLevel: locally-executed
---

# Operational route access

This follow-up to TEST-060 aligns frontend navigation and direct routes with
existing backend policies. Backend authorization, role definitions, and account
privileges remain unchanged. No NLA V3, analytics implementation, RAG, manuscript,
or mobile source is changed by this follow-up.

## Policy audit

| Primary entry or route | Existing backend access | UI decision |
| --- | --- | --- |
| Dashboard | Authenticated reads; the separate analytics widget is already GSD-only | Retain primary entry |
| Assets list/detail | Authenticated reads | Retain primary entry |
| Asset creation | GSD, CanManageAssets | Guard direct creation route |
| Schedules list/detail | GSD, Inspector, Supervisor, CanReadSchedules | Hide entry from other roles; guard direct routes |
| Schedule creation | GSD, Supervisor, CanManageSchedules | Guard creation route separately |
| Inspections list/detail | Authenticated reads | Retain primary entry |
| Form review | GSD, Inspector, CanManagePreventiveMaintenanceForms | Retain existing navigation filter; guard direct routes |

Admin and DepartmentHead alone do not receive Schedules navigation. A user with
Admin plus GSD retains operational access through GSD. Authorization checks use
any matching allowed role; Admin never overrides another role or grants access.
Current-user loading and errors must not be treated as proof of role denial.
An unauthorized route must render Access denied before mounting operational data
queries, with no generic loading Retry.

## Web change and browser smoke

Commit `c72625776f25bc2e2362cb1e9fc98ac61c29febc` changes:

- `web/src/components/layout/app-shell.tsx`: filter desktop/mobile navigation
  and gate confirmed restricted routes before their operational queries mount.
- `web/src/features/auth/app-route-access.ts`: mirror existing route policies,
  including the stricter creation routes and users with multiple roles.
- `web/src/components/layout/app-shell-access.test.tsx`: exercise navigation,
  denied direct routes, pending/error role lookup, and permitted mixed roles.

The real GSD browser retained Schedules and Forms navigation after the patch.
The Due/Q4/2026 registry showed the two prepared schedules, with the summary
showing 45 total, 8 Due, and 37 Completed. Opening DEMO-LIB-FE-002 displayed
Library, fire-extinguisher, cycle 2026-11, Maintenance Inspector, and Maintenance
Supervisor. The detail was checked at the committed web source above. No new
database writes were performed by this follow-up browser check.

The earlier real Admin failure remains `GET /api/v1/schedules`, HTTP 403,
empty body and no problem details, as recorded in TEST-060. The cause was the
frontend exposing an operation excluded by the account's role, then presenting
authorization denial as a retryable load failure. No backend exception or
database/client/parser defect was demonstrated. The new Admin route behavior
is covered by automated browser-component tests; a fresh real Admin browser
session was not exercised in this follow-up.

## Continued workflow checks

The first focused access-test run at `c726257` completed with 8 passed and 2
failed. The direct schedule-detail and schedule-creation assertions expired
at the default one-second lookup timeout during router/auth initialization.
There was no demonstrated production redirect or role-policy defect. One
test-only correction waits for account identity and permits the denied heading
to settle within five seconds, committed as
`439bf3132a4d29057ccac927f1cbc746acfa4291`; production role logic is unchanged.

From `web/`, checks ran sequentially at that exact SHA:

| Command | Result |
| --- | --- |
| `npm run test:run -- src/components/layout/app-shell-access.test.tsx` | 10 passed, 0 failed; the single permitted rerun |
| `npm run typecheck` | Passed, exit 0 |
| `npx vite build` | Passed, exit 0 |

The passing access tests cover both navigation layouts, mixed Admin/GSD roles,
five unauthorized direct routes with zero operational GETs, pending account
lookup, account-lookup failure, and permitted schedule loading. The initial
failure log and final test/typecheck/build logs are preserved in ignored
`artifacts/field-testing-admin-ux/`. Test output includes jsdom's unimplemented
scrollTo notices; these did not fail tests.

Backend and Flutter tests ran at exact commit
`6452accebe37364baee3d86cda16ed2baee7845b`, before the navigation patch. Backend
and mobile sources remain unchanged by the patch.

```powershell
dotnet test tests/UniPM.Api.Tests/UniPM.Api.Tests.csproj --configuration Release --no-restore --filter "FullyQualifiedName~ScheduleQueryEndpointsTests|FullyQualifiedName~ScheduleBatchAssignmentEndpointsTests|FullyQualifiedName~PreventiveMaintenanceFormDraftEndpointsTests|FullyQualifiedName~InspectionLocationVerificationTests|FullyQualifiedName~AuthorizationPolicyTests" --logger 'trx;LogFileName=backend-focused.trx' --results-directory artifacts/field-testing-admin-ux
```

The single Release invocation passed 93 tests, failed none, and skipped none.
Release avoided the running Debug API's executable lock from TEST-060 without
stopping the API. The build emitted one nullable warning in the existing form
endpoint source. This does not claim SQL-backed test execution; the tests use
their existing test factories. The previous real SQL/API/browser smoke remains
recorded in TEST-060.

| Backend class | Passed |
| --- | --- |
| ScheduleQueryEndpointsTests | 33 |
| ScheduleBatchAssignmentEndpointsTests | 3 |
| PreventiveMaintenanceFormDraftEndpointsTests | 33 |
| InspectionLocationVerificationTests | 17 |
| AuthorizationPolicyTests | 7 |

From `mobile/`, the single focused command was:

```powershell
flutter test --no-pub test/inspection_location_test.dart
```

All 16 tests passed. They use fake platform location and fake repositories to
cover configured fresh capture, permission retry, inside/outside/uncertain
results, carrying a locationAttemptId into a new inspection row, and
unconfigured continuation without permission/GPS/server-attempt calls. They do
not establish real device GPS accuracy or physical QR scanning.

The matching Draft entry remains source-inspected: it resolves to resume and
bypasses fresh verification. This Flutter file has no Draft-resume test. The
backend's existing resume test permits another location attempt, so that test
does not prove the mobile no-new-attempt rule.

The executed schedule tests cover Active-asset, Department, and allowed-cycle
validation. The real smoke used active Inspector and Supervisor accounts.
Rejection of inactive batch assignees remains source-inspected; no dedicated
inactive-assignee regression was identified in the selected tests.

Raw logs, backend TRX, and exact command/commit metadata are ignored under
`artifacts/field-testing-admin-ux/`. Credentials, tokens, configuration,
coordinates, and institutional records are not included in this record.

## Verification limits

No Android device was attached during device discovery. No physical GPS,
camera, permission dialog, or real QR scan was executed. Local assets still
need a complete verification latitude/longitude/radius for a later field check.
No coordinates were invented or saved. The prepared Library/fire-extinguisher/
2026-11 batch from TEST-060 remains available, assigned to the fictional active
Inspector and Supervisor. No duplicate schedules or inspection rows were added
by this follow-up.

See TEST-060 for the exact web/Android field-test steps. Full suites, remote CI,
deployment, and institutional acceptance are not claimed. The branch remains
unmerged and unpushed.
