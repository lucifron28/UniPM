---
id: TEST-190
type: test-run
title: Clean printable report documents
status: executed
recordedAtUtc: 2026-10-29T12:25:00Z
testedCommit: d7031312a29588e51df20d22df34271b7af873b8
sourceBranch: feature/clean-print-reports
evidenceLevel: locally-executed
---

# Clean printable report documents

## Scope

Source inspection found four explicit print actions: NLA results, the PM
dashboard summary, dashboard records, and asset QR labels. No mobile print
action was found. The supplied PDF showed screen controls and repeated
information in an NLA printout. This record addresses report presentation;
it makes no claim about which deployed version generated that PDF.

NLA and dashboard summary printing now render dedicated documents through a
body-level portal. Browser print events and print-media changes activate the
document. Navigation, save fields, buttons, source links and screen charts
are excluded. NLA prints the result table even when the screen chart is
selected. It retains scope, generation time, selected result columns, rate
numerators and denominators, unavailable reasons and ranked-subset notes.
Internal record identifiers are omitted. Paginated results explicitly identify
the displayed result page and full-scope source count; printing does not fetch
all result pages. Wide tables use landscape paper.

The dashboard summary prints department counts and Inspector assignment
outcomes, with the 30-calendar-day rule separate from scheduled-month timing.
Detailed records retain individual results and deadlines while removing
duplicate summaries and some repeated per-row wording. Their existing print
layout includes all locally filtered rows, independently of screen paging.
QR label printing was inspected and retained without production changes.
No report query, backend calculation, seed record or provider behavior changed.

## Execution

Windows, Node.js 24, installed web dependencies, headless Chromium through
Playwright, and Poppler PDF rendering were used. Browser APIs were mocked with
synthetic fixtures derived from the committed web browser tests. No live
database or external model was contacted.

The final ignored browser script ran successfully against the tested commit
above. Earlier unit and static checks ran against the uncommitted print
implementation based on `c83f621bb6f27a9d3c3c54c7738a9f038157d803`.
The final source cleanup removed an unused dashboard print stylesheet and
its import. The final browser run includes that cleanup.

| Check | Result |
| --- | --- |
| `npm run test:run -- src/features/reports/pm-analytics-v3.test.tsx` | 25 passed after one correction for absent `matchMedia` in jsdom; no assertions changed |
| `npm run typecheck` | Passed |
| `npm run lint` | Passed with no warnings |
| Prettier check of the five changed TSX/CSS source files | Passed |
| `npm run build` | Passed, including TypeScript checking |
| `node artifacts/clean-print-qa.mjs` | 15 synthetic PDF scenarios passed on the tested commit |
| `git diff --check` | Passed |

The initial browser review exposed a malformed wide-report class and print
state being cleared while print media remained active. Both were corrected
before the final retained browser run. The final native-print sample also
exercised `beforeprint` without first emulating print media.

## Browser and PDF observations

NLA screen checks at 1440 and 1536 pixels found no horizontal overflow.
The print scenarios covered QR labels, dashboard summaries, short and
60-row detailed records, department aggregates, Letter paper with browser
headers, percentages, empty results, 25-row paginated NLA records,
unmeasurable rates, ranked results, chart-backed results in both screen
views, native browser printing and inventory totals. No JavaScript page
errors were captured. Printable documents contained no visible interactive
controls.

The nine-department aggregate occupies one A4 page. The dashboard summary
occupies one page. The 60-row detailed report spans six landscape pages;
the 25-row NLA record page spans three landscape pages. Repeated table
headers, wrapped values and page numbers were inspected in rendered samples.
The rate sample retains `50%` and `5 of 10`; an unmeasurable rate prints
its reason rather than zero. The QR sample stays on one page, and navigating
from it to a report does not retain QR-only print rules.

Raw browser observations, PDFs, screenshots and command logs are retained
locally under ignored `artifacts/clean-print-qa/` and
`artifacts/clean-print-*.log`. They contain synthetic fixtures only.

## Limits

These checks verify Chromium rendering with mocked API responses. They do
not verify live-backend numerical correctness, every possible selected-column
combination, Windows Print to PDF, other browser engines, or a physical
printer. Browser-added headers and footers remain a print-dialog setting.
No deployment was performed as part of this record.
