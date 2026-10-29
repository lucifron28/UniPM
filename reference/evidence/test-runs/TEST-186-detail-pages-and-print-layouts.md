---
id: TEST-186
type: test-run
title: Detail-page hierarchy and printable report layouts
status: executed
recordedAtUtc: 2026-10-29T07:22:00Z
testedCommit: feb0613be03154d73bb99b9202e6935f3345163b
sourceBranch: feature/detail-page-redesign
evidenceLevel: locally-executed
---

# Detail pages and printable reports

## Changes

Schedule, asset, inspection and PM form details use a shared record header,
grouped fields, visible back navigation and expandable supporting metadata.
Schedule details keep the original PM deadline separate from the Inspector's
30-calendar-day assignment period. Inspection findings precede context and
technical metadata. Form and acknowledgement review display lifecycle stages.
Assignment panels retain their existing eligibility and mutation logic.

Dashboard summaries, dashboard records, NLA results and asset QR labels have
scoped print layouts. Reports include scope and time information. Tables wrap
long values and repeat their headings across pages. NLA printing explicitly
identifies the displayed result page and full-scope matching-record count;
it does not claim to print every result page. The dedicated dashboard records
printout includes every locally filtered row, independently of screen paging.

QR print rules apply only while an asset label is present. Global print rules
remove screen-height minimums, navigation, developer tools and scroll clipping.
Dashboard summaries start their chart section on a separate page.

## Environment and execution

Windows, Node.js 24.15.0, installed npm dependencies, Playwright Chromium.
The repository's CI uses Node.js 22. Browser API responses were mocked with
synthetic fixtures. No SQL changes, provider calls, GSD acceptance or physical
printer verification are represented by these checks.

| Check | Result | Source |
| --- | --- | --- |
| `npm run test:coverage` | 278 tests passed in 32 files; statements 65.62%, branches 59.97%, functions 61.18%, lines 67.02% | `5c0e210f33ad40f8205bc5eed1f5e4f792327b59` |
| `npm run e2e` | 63 passed; two opt-in live-backend scenarios skipped | Final application commit identified above |
| `npm run build` | Passed, including TypeScript checking | Final application commit identified above |
| `npm run lint` | Passed with no warnings | Final application commit identified above |
| `npm run format:check` | Passed after formatting the shared form back link | `63d0fb20b9236a2aff68946717f72494c991c5f4` |

The final application commit removes one old class override so the PM form
uses the shared back-button appearance. The full browser run covers that
change. Coverage was captured before this final presentation-only correction.
The subsequent formatting commit only collapses that JSX element onto one line.

## Desktop and print review

An ignored Playwright script reused the committed fixtures in
`ux-context-navigation.spec.ts`, `pm-period-dashboard.spec.ts` and
`pm-analytics-v3.spec.ts`. It opened schedule, asset, inspection, form and
acknowledgement details at 1440 and 1536 pixels. All ten page/viewport checks
had one H1, no document or workspace horizontal overflow and no captured
JavaScript page errors. Long schedule identifiers also fit both widths;
keyboard activation expanded supporting metadata. Missing assignment timing
and absent photo evidence retained explicit unavailable states.

Chromium PDFs were rendered with Poppler and visually inspected. Samples:

| Output | Observed layout |
| --- | --- |
| Asset QR label | One A4 portrait page, readable asset context and QR image |
| Dashboard summary | Two A4 landscape pages, summary followed by charts and follow-up |
| Dashboard records | One A4 landscape page for three rows |
| Dashboard records, long synthetic data | Seven pages; all 60 rows included, long names wrapped, headings repeated |
| NLA summary | One A4 landscape page with question, scope, population and definitions |
| NLA records, long synthetic data | Four pages for the displayed 25 rows; header states result page 1 of 2 and 50 total matches |

The stress fixtures exercise layout, not maintenance calculations or
interpretation accuracy. Their counts are synthetic and are not study results.

Committed browser assertions cover QR visibility, NLA print scope, hidden
interactive controls, dashboard print headings and dashboard visibility after
client-side navigation through an asset detail page. Existing source-review,
assignment, acknowledgement and return-navigation assertions remain in place.
Heading assertions were updated for H1 detail titles and the retained Schedule
history heading. The form test now checks both the status badge and lifecycle
stage rather than assuming a status label appears only once.

## Failures encountered

The initial unit run had three presentation assertion failures caused by the
new H1 and lifecycle labels. Those expectations were corrected; all tests then
passed. The first browser run also encountered page reloads during editing and
an outdated schedule-heading assertion. A stable development-server run passed
all 63 runnable scenarios. A separate production-preview run passed 62 tests;
its authentication race test requires importing a source module available only
on the development server, so that run is not reported as a full pass.

Print review exposed leaking QR visibility rules, excessive screen-height
minimums and orphaned scope notes. These were corrected before the final PDF
review. Temporary layout scripts also required fixture and media-mode fixes;
these were verification-script corrections, not production interpretation
changes.

## Retained artifacts and limits

Raw logs remain ignored under `artifacts/redesign-*.log`. Desktop screenshots,
PDFs, rendered pages and `observations.json` are under
`artifacts/redesign-desktop/`. The ignored scripts are
`artifacts/redesign-browser.mjs` and `artifacts/redesign-stress.mjs`.

Historical evidence is unchanged. This record establishes browser behavior
with synthetic responses and Chromium PDF layout. It does not establish live
backend integration, physical printing, GSD acceptance or deployed behavior.
