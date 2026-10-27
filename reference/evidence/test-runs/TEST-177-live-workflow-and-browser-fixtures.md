---
id: TEST-177
type: test-run
title: Live PM workflow and current browser fixture corrections
status: executed
recordedAtUtc: 2026-10-27
testedCommit: 6e06873d50f5c540f2751396dfff08db83e5af77
sourceBranch: feature/demo-scenario-coverage
evidenceLevel: locally-executed
---

# Live workflow and browser fixtures

The live Playwright workflow passed, one test in 6.6 seconds, against an owned
disposable SQL Server database. The normal local demonstration database was not
modified. The test used a headless Chromium browser at 1440 by 1000 pixels. The
Supervisor and Inspector assignment screenshots were also inspected at desktop
width.

Execution used a8956a6d9d59ca614b1ccb8282595000d7471557 plus the live-fixture
patch subsequently committed unchanged as the tested commit above. Backend
source remained identical to 08381712811ed2676107ac780046a3fe9ed6231f. Other
browser-fixture edits were present but were not selected by the live test.

The test exercised GSD assignment of a Supervisor and Supervisor assignment of
an Inspector through the web UI. API checks then covered two inspection saves,
schedule completion before form submission, rejection of incomplete submission,
whole-form submission and acknowledgement, dashboard counts, corrective-handoff
eligibility, WMS referral recording and its audit entry. Role restrictions,
completed-schedule timestamps and unchanged inspection findings were asserted.
This is synthetic development verification. It does not establish physical
mobile-device acceptance or independent GSD evaluation.

The earlier approved final run failed while trying to click a native datalist
option as a visible page element. The corrected fixture waits for the option to
exist, fills the full account name and requires the assignment button to enable.
The next run passed both assignments but exposed a stale expectation that all
batch assignment would return 409 after one schedule completed. The current
endpoint operates on unfinished schedules. The final test requires only the
remaining schedule in that response and verifies the completed schedule did not
change. It also expects referral fields to update on exactly one dashboard row
while all other response fields remain unchanged. No application rules were
changed to satisfy these checks. The project owner explicitly instructed work
to continue after the earlier retry exception was exhausted.

The live command selected only
`web/e2e/schedule-batch-assignment.live.spec.ts`, with one worker and zero retries.
Local configuration pointed Playwright to the owned verification API and Vite
server. Existing synthetic credentials were supplied through process environment
variables and were not written to artifacts. No external AI calls were made.

Raw artifacts remain ignored under `artifacts/audit-remediation-web/`:

- `final-live-workflow.log` preserves the previous datalist failure.
- `corrected-live-workflow.log` preserves the stale batch-assignment assertion.
- `completed-live-workflow.log` records the successful complete execution.
- `completed-live-results/` contains the desktop assignment screenshots.

Web CI run 40514198541 on a8956a6 passed formatting, lint, types, API contract
checks, generated-client checks, all web unit tests, coverage and build. Its
browser stage reported 57 passed, four failed and two skipped. The four failures
concerned the retired dashboard heading and standalone percentage nodes,
comparison selection after a period refresh, an open batch panel intercepting
pagination, asset links now inside that panel, and an immediate heading read
before navigation rendered. The corresponding existing browser fixtures were
aligned with the approved UI. Production code was not changed in this follow-up.

The focused mocked-browser command selected the four failing test names across
`e2e/app-shell.spec.ts`, `e2e/pm-period-dashboard.spec.ts` and
`e2e/schedules.spec.ts`. Its first invocation used Windows backslash file filters
and selected no tests. The corrected invocation selected four tests. After
fixture corrections, the shell, dashboard and deep-link cases passed, three of
four. Pagination still used a duplicate responsive text locator, then compared
an open panel's content against background registry pagination. Those fixture
measurements were separated. The final pagination-only run passed, one test in
10.8 seconds. It retained exact asset checks, scroll-position preservation,
registry spacing and horizontal-containment checks across responsive widths.

These mocked-browser runs used 6e06873 plus the three-file patch subsequently
committed as 14ef0231c93aabfbade1e710ec11398fb7f384ab. No additional local full
suite was run. The focused logs are `focused-e2e.log`, `focused-pagination.log`
and `focused-pagination-final.log` in the same ignored artifact directory.
The full CI result for the corrective publication remains a separate GitHub
Actions execution; this record does not predeclare its outcome.
