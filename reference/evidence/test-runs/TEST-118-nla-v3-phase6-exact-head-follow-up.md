---
id: TEST-118
type: test-run
title: NLA V3 Phase 6 exact-head backend and evaluation follow-up
status: executed
recordedAtUtc: 2026-10-20T01:43:24Z
testedCommit: a9331b2b2cdd04cbf2ecf27d3b08c85dbb6639f0
sourceBranch: feature/nla-v3-asset-inventory
evidenceLevel: locally-executed
---

# NLA V3 Phase 6 exact-head backend and evaluation follow-up

## Scope and boundaries

- Tested exact source head `a9331b2b2cdd04cbf2ecf27d3b08c85dbb6639f0` on
  `feature/nla-v3-asset-inventory` with .NET SDK `10.0.203`.
- This follow-up verifies the latest evidence/documentation head. It does not
  change Phase 6 behavior or implement Phase 7.
- The matching roadmap status is in the Phase 6 section of
  `reference/planning/nla-v3-composable-reporting-design.md`.
- No SQL Server, Azure/staging service, production data, deployment, migration,
  DeepSeek request, embedding-provider request, or held-out corpus was used.

## Backend and evaluation checks

| Command/check | Result |
|---|---|
| `dotnet build server/server.csproj --configuration Release --no-restore` | Passed: 0 warnings, 0 errors. |
| Full `UniPM.Api.Tests` Release suite | Passed: 555 passed, 50 skipped, 0 failed, 605 total. |
| Focused `FullyQualifiedName~PmAnalyticsV3` | Passed: 25 passed, 2 skipped, 0 failed, 27 total. Both skips require SQL Server 2019. |
| Focused `FullyQualifiedName~PmAnalyticsV2` | Passed: 44 passed, 1 skipped, 0 failed, 45 total. The skip requires SQL Server. |
| V3 evaluation `validate` | Passed structural validation: 18 cases, 18 labels pending, no score. |
| V3 evaluation `diagnose` | Completed deterministically without provider or database calls; found differences in V3D-008 and V3D-009. These are unresolved label/parser differences, not accuracy results. |

The first full-suite attempt inherited process-level `UNIPM_JWT_*` and
`UNIPM_WEB_ORIGIN` overrides. Two production startup tests then received a
different configuration than the test factory intended. The JWT authorization
tests passed on a focused rerun and the full suite passed after those five
configuration variables and the application database connection were unset
for the test process. No values were read or recorded.

## SQL Server and acceptance limits

The dedicated SQL Server 2019 test connection is not configured and there is no
local SQL Server 2019 image. Native SQL Server translation and execution remain
unverified; the EF InMemory tests do not substitute for that check. No Azure or
staging connection was attempted.

All 18 provisional interpretation labels remain pending independent human
review. In particular, V3D-008 needs a decision on preserving a safe partial
draft for an unresolved `or` condition, and V3D-009 needs a decision on the
meaning of “not late.” V3D-004, V3D-006, V3D-007, V3D-012, and V3D-016 also
retain the semantic questions listed in `review-notes.md`. No score,
held-out result, GSD acceptance, or provider quality claim is made.
