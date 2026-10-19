---
id: TEST-081
type: test-run
title: Searchable web filters verification
status: executed
recordedAtUtc: 2026-10-19T03:32:53Z
testedCommit: 548e74dec8f1600facfb89e03febdb3b016544c7
sourceBranch: feature/searchable-web-filters
evidenceLevel: locally-executed
---

# Searchable web filters verification

## Objective

Check the searchable-filter implementation and its long-list browser scenario.
The tested source snapshot is the implementation committed as
`548e74dec8f1600facfb89e03febdb3b016544c7`; `npm run typecheck` and API
generation ran after the edits and before that commit, with no source changes
between those runs and the commit.

## Execution Identity

- Source branch: `feature/searchable-web-filters`.
- Tested source commit: `548e74dec8f1600facfb89e03febdb3b016544c7`.
- Local execution date: 2026-10-19 UTC.
- No external AI provider, real institutional data, or deployment was involved.

## Environment

- Windows desktop development environment.
- Web commands ran from `web/`.
- Playwright's configured Vite web server uses port 5173.

## Commands

```powershell
npm run typecheck
npm run api:generate
npm run e2e -- e2e/schedules.spec.ts --grep "searches long filters" --workers=1
```

The first two commands completed successfully. The focused Playwright command
was attempted twice, including once outside the sandbox; both attempts stopped
before test startup because port 5173 was already occupied and Playwright could
not start its configured web server.

## Results

| Check | Result |
| --- | --- |
| Web TypeScript typecheck | Passed. |
| API client generation and contract sanity check | Passed; generated OpenAPI contract sanity output reported success. |
| Focused long-list Playwright scenario | Not executed. The configured web server could not bind/reuse port 5173. |
| Desktop screenshots at 1280×720, 1440×900, and 1920×1080 | Not captured; browser scenario did not start. |

## Test Counts

- Focused Playwright cases started: 0.
- Focused Playwright cases passed, failed, or skipped: none; startup failed before collection.

## Backend And Database Verification

The earlier statement that database verification was not applicable was
inaccurate: this feature changes backend registry query behavior for assets
and personnel. TEST-081 itself did not run the backend registry test. The
focused backend result is recorded in TEST-082. Native SQL Server-specific
execution was not run.

## AI-Provider Verification

Not applicable. No AI provider calls were made.

## Generated Artifacts

- `artifacts/searchable-filters/verification-observations.txt` records the
  observed command outcomes. It is a transcription of tool results, not a raw
  console log; original stdout for the successful commands was not retained.
- No screenshots or Playwright report were produced.

## Failures And Corrections

Both focused Playwright startup attempts reported that port 5173 was already in
use. A local listener could not be identified through the available Windows
process inspection, and the browser could not connect to a usable local page.
The occupied process was left undisturbed. No application or test failure was
observed because no test began.

## Skipped Verification

- Browser interaction, long-list scrolling, keyboard navigation, combined
  filter behavior, popup viewport bounds, and requested desktop screenshots
  were not verified during TEST-081. Their follow-up is recorded in TEST-082.
- Backend registry query behavior was not tested during TEST-081; see TEST-082
  for the focused asset registry test result.

## Limitations

This record's original execution confirms typecheck and generated API contract
checks only. It does not establish browser behavior or screenshot acceptance.
TEST-082 records the successful follow-up browser and backend verification.
