---
id: TEST-175
type: test-run
title: Desktop report printing and live workflow fixture review
status: executed
recordedAtUtc: 2026-10-27
testedCommit: 8fd6e62d2dcd8c92e6011a4df854f4f151f0cfdf
sourceBranch: feature/demo-scenario-coverage
evidenceLevel: locally-executed
---

# Desktop report and workflow review

This record distinguishes successful report acceptance from a live workflow
test that did not finish. All API writes used an owned disposable SQL Server
copy restored from the clean synthetic baseline. The normal UniPMDb fixture
was not used for workflow mutations. No native desktop application or visible
external browser was used for the checks.

## Source identity

The backend API used the parser source committed as 0838171. Report changes
were checked as a working patch and committed unchanged as
6a8fd5aba800584207c97bf4cb426b59bb86260f. The first live workflow used the
older fixture on that source. The corrected fixture for the single permitted
rerun was committed as 8fd6e62. A subsequent exact-combobox selector correction
was committed as d037b94684c749850cccf8ffbe3499849205bac8, but had not been
rerun at the time of this record. Source publication and remote CI results
are separate from these local checks.

## Report acceptance

Targeted ESLint on pm-period-dashboard-records.tsx passed once. The unnecessary
useMemo dependency was replaced by deriving the generated-at value from the
query's dataUpdatedAt timestamp. The source and print views now explain why
original-month compliance is unavailable for an open or future month.

Headless Playwright used the repository's installed browser and Node 22,
without downloading a browser. The viewport was 1536 by 1100. The actual
synthetic report scope was all departments, fire extinguishers, August 2026.
The screen held 25 paginated rows while the print DOM held all 78 filtered rows.
The longest department label was STUDENT AFFAIRS OFFICE, 22 characters.

Visual review found printed developer-tools widgets and an extra blank final
page. Print CSS was corrected to hide those widgets, reset screen-only height
constraints, use consistent table typography, and apply the named page style
to the document rather than only the report section. Navigation-only Details
links remain available on screen and are omitted from paper output.

The final PDF contained seven A4 landscape pages. The first, second and final
pages were visually inspected. Table headers repeated; the final page held
the final five records. No widget or blank trailing page remained. A separate
read-only PDF text check confirmed that every one of the 78 expected asset
codes appeared in the PDF, with zero missing codes. The final page contained
1,840 extracted text characters. No browser page errors were recorded.

## Live workflow limit

The existing schedule-batch-assignment.live.spec.ts was selected to exercise
GSD Supervisor assignment, Supervisor Inspector assignment, Inspector findings
and form submission, Department Head acknowledgement recorded by the Inspector,
official history, dashboard updates and WMS referral.

The initial run failed at a stale suggestion selector. Filling an exact
Supervisor name had already selected the account and closed its suggestion
list. The screenshot showed the selected account and enabled assignment action.
The fixture was corrected to type a partial name before choosing the account,
use the current Inspector label, open Account menu before Sign out, and allow
120 seconds for the multi-role integration flow. Business/data/role assertions
and the 1440 by 1000 viewport were preserved.

The single permitted rerun failed earlier in the GSD stage because
getByLabel('Inspector') also matched the section named Inspector assignment
period. The snapshot showed the Supervisor combobox and no visible Inspector
picker. The final correction uses getByRole('combobox', { name: 'Inspector',
exact: true }) for the Inspector picker assertions. No additional live rerun
was executed under the repository's existing retry limit. The complete workflow
therefore remains unverified in this record. Approval was requested for one
additional run, citing AGENTS.md lines 204-207.

An early print driver also needed its wait changed from visible to attached
for deliberately hidden print-only DOM. That was a harness correction, not a
product behavior change. The optional QR API check and physical QR-camera/GPS
capture were not executed by the web executor.

## Artifacts

Ignored artifacts/audit-remediation-web contains desktop-1536.png,
detailed-report-a4-landscape.pdf, final-pdf-page-1.png, final-pdf-page-2.png,
final-pdf-page-7.png, print-review.log, print-review.mjs,
pdf-content-check.json, expected-print-codes.txt and the live workflow log.
Playwright failure context/screenshots are under ignored web/test-results.
Earlier failed print captures are retained separately from final captures.
No authentication values, raw provider prompts or private data were committed.
