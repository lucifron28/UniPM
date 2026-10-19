---
id: IMP-052
type: implementation
title: PM analytics V3 reporting correctness and workflow hardening
status: reviewed
recordedAtUtc: 2026-10-19T10:15:03Z
sourceBranch: feature/nla-v3-composable-reporting
evidenceLevel: locally-executed
---

# PM analytics V3 reporting correctness and workflow hardening

## Objective

Address correctness and intent-preservation defects in the V3 PM reporting
workflow and improve the React path from question through reviewed results.
Scope stayed within reporting Phases 0–5. No deployment, production data,
Azure resource, database schema, or Phase 6–10 work was performed.

## Source Identity

- Relevant commit: `e2f90bba35707a7142b3dccb1afb8eeec6143ad4`.
- Implementation date: 2026-10-19.
- Source branch: `feature/nla-v3-composable-reporting`.
- The feature branch was current with its fetched origin branch before changes.

## Implementation Summary

- Compliance measurability now uses all selected category cycles for each
  non-category group, including cycles that have no persisted row in that
  group. Grouping by asset category still narrows to that category's cycles.
- Catalog suggestions deduplicate case-insensitively in the query before the
  result limit, so repeated early values do not hide later distinct values.
- Top/bottom selection is applied before honoring the requested output order.
- Deterministic question parsing now clarifies conflicting periods, OR filters,
  negated outcomes, missing ranking scope, multiple ranking measures and
  unresolved named population filters. It supports quoted department/building/
  location/asset filters, percentage words, and completed-record conditions.
  Unsupported exclusions and question-based sorting are returned explicitly
  instead of being silently omitted. Calculations remain backend-controlled.
- Records mode rejects grouping. Ranking requires a supported group, limit and
  unambiguous measure. DeepSeek remains gated to Development with its explicit
  feature flag and key; no provider request was made.
- The React flow now exposes catalog loading/retry, clarification, preview
  expiry recovery, stale-result clearing, empty results, metric definitions and
  numerator/denominator provenance. Supported measures can open a filtered
  records plan for review. Distinct-asset source drill-down is disabled because
  the records view cannot represent its non-additive numerator exactly.
- Record rows now include an optional inspection ID so results can link to the
  inspection when present or to the source asset otherwise. OpenAPI and the
  generated TypeScript model match this contract.
- Preview plans are carried in signed, stateless, user-bound tokens that expire
  after ten minutes. Any API instance using the same JWT signing key can verify
  a token; the preview store does not retain server-side in-memory state.

## Architecture And Contracts

The existing catalog, typed plan, validator, query service, preview/execute
endpoints and DeepSeek client are retained. No executable SQL is generated.
All denominators, rankings, filtering and source-record selection stay in the
backend. API authorization remains GSD-only; new endpoint tests cover the
catalog, filter values, interpret, preview and execute routes.

## Important Files

- `server/Features/Reports/PmAnalyticsV3QueryService.cs`
- `server/Features/Reports/PmAnalyticsV3QuestionInterpreter.cs`
- `server/Features/Reports/PmAnalyticsV3Contracts.cs`
- `tests/UniPM.Api.Tests/Reports/PmAnalyticsV3RegressionTests.cs`
- `tests/UniPM.Api.Tests/Reports/PmAnalyticsV3EndpointTests.cs`
- `web/src/features/reports/pm-analytics-v3.tsx`
- `web/src/features/reports/pm-analytics-v3.test.tsx`
- `web/e2e/pm-analytics-v3.spec.ts`
- `web/openapi/unipm-v1.json`
- `web/src/api/generated/models/pmAnalyticsV3RecordRow.ts`

## Database Changes

No schema or migration changes. Native SQL Server 2019 query translation and
execution were not verified in this environment.

## Tests Present

Added service-level tests for multi-category compliance, ranking output order,
catalog deduplication and question interpretation, plus an authorized API
interpret-preview-execute workflow and negative role checks. Added seven React
workflow tests and one narrow-viewport Playwright scenario. Backend and browser
test execution limits are detailed in [TEST-110](../test-runs/TEST-110-pm-analytics-v3-reporting-hardening.md).

## Verification Status

The Node 22.23.3 web build, lint, changed-file formatting, seven focused React
tests, OpenAPI generation/drift check and seven negative OpenAPI contract checks
passed at the implementation commit. Backend compilation/tests did not run
because the .NET SDK is absent. The Playwright scenario could not start because
Chromium is absent; the official browser download returned HTTP 403.

## Known Limitations

- Backend compilation, endpoint authorization execution and service regression
  tests remain unverified.
- Native SQL Server 2019 behavior remains unverified; no SQL test connection is
  configured.
- Browser behavior, responsive rendering and screen-reader behavior remain
  unverified in a real browser. A Playwright test is present but could not
  launch Chromium.
- Cross-instance token validation is covered by a repository regression test,
  but that test was not executed in TEST-110. No deployed API runtime was
  exercised; instances must share the JWT signing key to verify the tokens.
- No DeepSeek call or provider-cost test was made. Production provider calls
  remain disabled by the existing Development-only gate.

## Related Evidence

- [TEST-110: PM analytics V3 hardening verification](../test-runs/TEST-110-pm-analytics-v3-reporting-hardening.md)

## Architecture correction

This record originally described the preview mechanism as an in-memory store.
That wording was incorrect. The implementation in
`server/Features/Reports/PmAnalyticsV3PreviewStore.cs` creates and verifies
signed, stateless, user-bound tokens with a ten-minute lifetime. The historical
TEST-110 execution results remain unchanged; its backend test limitation is
preserved above.
