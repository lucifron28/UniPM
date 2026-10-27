---
id: TEST-169
type: test-run
title: Dashboard chart wording and timing availability
status: executed
recordedAtUtc: 2026-10-27T06:13:26Z
testedCommit: 4a7e72306a4dfe406673b8527be74ba107304399
sourceBranch: feature/dashboard-chart-overview
evidenceLevel: locally-executed
---

# Dashboard chart wording and timing availability

The owner requested clearer chart meaning at first glance. This follow-up to
TEST-168 adds visible measure descriptions, names the PM deadline, removes the
repeated inspection breakdown, uses outcome-specific colours and singular units,
and labels department tables explicitly. Missing assignment timing displays an
unavailable explanation instead of zero outcome bars. Partial timing displays
included and excluded schedule counts.

## Executed checks

Verification ran against the final working source subsequently committed as
`4a7e72306a4dfe406673b8527be74ba107304399`, in the original checkout.
Node 22 and the existing installed web dependencies were used.

- `node node_modules/vitest/vitest.mjs run src/features/reports/pm-period-dashboard.test.tsx --maxWorkers=2`
  passed all 10 tests, exit 0, duration 9.01 seconds. The first run passed 9 and
  failed 1 because an existing test still expected the removed breakdown heading.
  That stale assertion was corrected, then this same test file was rerun once.
- `node node_modules/typescript/bin/tsc -b --pretty false` passed, exit 0.
- Browser inspection rendered the actual `PmPeriodDashboardPresentation` using
  an ignored preview harness and fictional department/count data. The initial
  harness used duplicate React module imports and failed to render. Switching
  its imports to Vite-resolved React imports fixed the harness. No application
  runtime fix was needed for that issue.
- The rendered overview showed both descriptive headings, visible explanations,
  the PM deadline, 50-percent bars occupying half their tracks, green on-time
  outcomes, neutral unfinished outcomes, and collapsed exact-value disclosures.
- Selecting Inspector assignment turnaround with zero usable histories showed
  the unavailable explanation and count of four excluded schedules, without a
  zero-bar chart. A second fictional scope with two usable and two unavailable
  histories showed the chart and an explicit inclusion/exclusion message.
- Expanding the PM-outcome data showed Department, On time, Late and Unfinished
  headers, with values such as `1 schedule` and `0 schedules`.
- Scoped formatting and `git diff --check` passed.

Screenshot: `artifacts/dashboard-chart-clarity/overview.png`.

Existing component and browser test expectations were updated for the removed
strip and renamed chart labels. No new test cases were added. The browser E2E
suite was not executed. Browser checks used fictional presentation data, not a
live API/database or institutional acceptance session. No full suite, print/PDF
check, physical-device test, backend, migration or AI-provider run was performed.
The prior untracked `tmp/` and preserved old-edit stash were not changed. All
work remained on the existing feature branch in the original checkout.
