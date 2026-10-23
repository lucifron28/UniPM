---
id: TEST-150
type: test-run
title: Saved reports Chromium workflow
status: executed
recordedAtUtc: 2026-10-23T13:49:29Z
testedCommit: 07915dfa3786cd134d649381067e7dceed579cd6
sourceBranch: feature/nla-saved-reports-browser-test
evidenceLevel: locally-executed
---

# Saved reports Chromium workflow

One targeted Playwright scenario passed in Chromium at a 390 by 844 viewport.
The successful invocation exited 0 and reported one passed test in 11.0
seconds. No production code changed for this check.

## Execution

From `web/`, using a dedicated Vite development server:

```powershell
$env:UNIPM_E2E_PORT='5191'
$env:VITE_API_BASE_URL='/'
$env:CI='true'
./node_modules/.bin/playwright.cmd test e2e/pm-analytics-v3.spec.ts --grep 'saved reports can be reused, edited, renamed and deleted without repeated interpretation' --project chromium --workers 1 --retries 0 --reporter line --output ../artifacts/saved-reports-browser-2026-10-23/results
```

The first invocation used the same title surrounded by `^` and `$` in its
grep expression. Playwright reported no tests found and exited 1. The
selector was corrected once to match the title within Playwright's full test
name. The second invocation selected exactly one test and passed. Browser
retries were disabled; no broader suite or third invocation ran.

## Observed behavior

- Prepare a report through one initial interpretation request and preview it.
  No execution occurs before confirmation.
- Display a controlled HTTP 400 name-validation error, then successfully save
  the reviewed plan with the relative `ThisYear` setting.
- Reload the page, retrieve the saved definition, and open its new preview.
  The preview displays the API-resolved year 2026. Interpretation remains at
  exactly one request from initial report preparation.
- Confirm execution and display 50 percent progress. Change the fixture's
  current result to 100 percent, open another preview, and confirm again.
  The second result displays 100 percent. Opening a preview does not execute.
- Open the saved definition in the editor, change its year to 2025, preview,
  select fixed dates, and replace the saved settings using its current
  revision.
- Rename the report, request deletion, and confirm deletion. The list becomes
  empty. Merely opening the deletion confirmation leaves the definition intact.
- Observe exactly two executions and two saved-preview requests, with no
  repeated interpretation. No unhandled analytics requests or browser page
  errors occur. The final document width is at most 390 pixels.

## Evidence boundary

This uses the actual React application and Chromium with controlled API
responses. A route fixture retains the saved definition across page reload
and returns changed results. It does not contact ASP.NET Core, SQL Server or
DeepSeek. It therefore verifies browser behavior, not database persistence,
backend date resolution or real-provider behavior.

TEST-148 separately records the passing backend HTTP/InMemory check of
ownership, CRUD, current-data execution, relative periods and validation.
Native SQL Server migration/execution, a combined browser-to-database run and
GSD acceptance remain pending. The migration was not applied in this run.

Raw local logs are retained in ignored
`artifacts/saved-reports-browser-2026-10-23/browser-test.log` and
`artifacts/saved-reports-browser-2026-10-23/browser-test-rerun.log`.
Successful log SHA-256:
`cede8bc068db600b2f1e8b7605f773702a01e13172bccc048c07dade71c774ab`.
