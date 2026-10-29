---
id: TEST-192
type: test-run
title: Compact dashboard attention navigation
status: executed
recordedAtUtc: 2026-10-29T14:12:59Z
testedCommit: c241d4de448ed014b218d52afe923524a28d23e1
sourceBranch: feature/nonoperational-attention
evidenceLevel: locally-executed
---

# Compact attention navigation

This revision replaces the dashboard's large yellow findings panel with two
white links under Needs attention. Counts cover all periods within the actor's
read scope. Nonzero counts use a small amber dot; links have hover and keyboard
focus states. Referred findings remain available through the Inspections filter.
No findings, photos or PM-number editor are expanded on the dashboard.

Awaiting acknowledgement opens Form review with a server-side filter selecting
unacknowledged forms containing non-operational findings. The filter survives
form-detail return navigation and applying additional filters. Clear filters
removes it. Awaiting PM number opens the existing Inspections registry with
non-operational and pending-referral filters. Both destinations expose a visible
Back to dashboard link. The inspection detail's existing form-entry guide is
also available to Supervisors, matching their approved referral access.

## Verification

Checks ran on Windows with installed .NET, Node and Playwright Chromium. They
ran against the working tree based on `24565a13df1c12c3dfa7bd997e7c09239ef12b13`
before creating the application commits identified above.

- Eight focused `InspectionWmsReferralEndpointsTests` passed. Added assertions
  verify that the form attention filter excludes acknowledged, operational-only
  and other-Supervisor forms.
- Web build, including TypeScript, passed. Lint and API contract generation
  passed. The OpenAPI/client change is limited to the new form-list parameter.
- An ignored synthetic browser probe exercised both GSD and Supervisor
  sessions at 1440 and 1536 pixels. It verified link destinations, API filter
  transmission, retained filters on form-detail return, destination back links,
  absence of dashboard finding rows/referred warning, and horizontal fit.
  The final zero-count check verified No pending findings.
- Desktop screenshots were rendered and inspected.

The first probe used an exact Back to dashboard text selector that omitted the
visible arrow. The second reached all navigation checks but restored the
Inspector fixture before its zero-count check, hiding the role-limited strip.
Correcting those probe assumptions produced a complete passing run. No product
access rule or assertion about the intended destinations was weakened.

Raw local artifacts are ignored under `artifacts/`: the reproducible
`compact-attention-qa.mjs`, `compact-attention-verified.log`,
`compact-attention-backend.log`, `compact-attention-build.log`,
`compact-attention-lint.log`, and `compact-dashboard-*.png` screenshots.

## Limits

The browser uses synthetic API fixtures. Live backend navigation, native SQL
translation, deployed role checks and physical devices were not exercised.
This record does not claim deployment or institutional acceptance. TEST-191
retains the prior layout and permission verification as historical evidence.
